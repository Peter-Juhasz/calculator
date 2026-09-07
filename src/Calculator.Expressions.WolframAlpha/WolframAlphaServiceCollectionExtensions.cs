using Calculator.Expressions;
using Calculator.Expressions.WolframAlpha;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Puts Wolfram Alpha among the ways an expression can be worked out.
/// </summary>
public static class WolframAlphaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="WolframAlphaFullResultsClient"/> and the evaluator that reads
    /// expressions through it, with settings bound to <paramref name="configuration"/>.
    /// </summary>
    /// <remarks>
    /// The evaluator joins the <see cref="IExpressionEvaluator"/> list in the order this is
    /// called, which is the order the reader is offered them in.
    /// </remarks>
    public static IServiceCollection AddWolframAlpha(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<WolframAlphaOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient<WolframAlphaFullResultsClient>(
                WolframAlphaFullResultsClient.HttpClientName,
                ConfigureHttpClient)
            .AddStandardResilienceHandler()
            .Configure(ConfigureResilience);

        services.AddSingleton<IExpressionEvaluator, WolframAlphaExpressionEvaluator>();

        return services;
    }

    /// <summary>
    /// The client itself is given no deadline of its own, because it would be the shorter of the
    /// two and would cut a query off before the resilience pipeline had said its piece — and it
    /// would do so as a bare cancellation, with none of the pipeline's account of what happened.
    /// The waiting is the pipeline's to enforce, and one place to set it is enough.
    /// </summary>
    private static void ConfigureHttpClient(IServiceProvider services, HttpClient client)
    {
        client.BaseAddress = services.GetRequiredService<IOptions<WolframAlphaOptions>>().Value.BaseAddress;
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <summary>
    /// Widens the standard timeouts to what asking Wolfram Alpha actually takes.
    /// </summary>
    /// <remarks>
    /// The defaults — ten seconds an attempt, thirty in all — suit a service that looks an answer
    /// up. This one computes it, and an expression that gives it something to think about can sit
    /// there well past ten seconds and still be coming. Cut off at the default, such a query
    /// would never once succeed, and the retries would only ask again for the same thing and be
    /// cut off again at the same place.
    /// </remarks>
    private static void ConfigureResilience(
        HttpStandardResilienceOptions options,
        IServiceProvider services)
    {
        var wolframAlpha = services.GetRequiredService<IOptions<WolframAlphaOptions>>().Value;

        options.AttemptTimeout.Timeout = wolframAlpha.AttemptTimeout;
        options.TotalRequestTimeout.Timeout = wolframAlpha.TotalTimeout;

        // The breaker judges by what it has seen lately, and "lately" has to be long enough to
        // hold more than a single attempt or it would trip on the first slow one.
        options.CircuitBreaker.SamplingDuration = wolframAlpha.AttemptTimeout * 2;
    }
}
