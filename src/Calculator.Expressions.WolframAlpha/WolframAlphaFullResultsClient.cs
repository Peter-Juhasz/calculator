using Microsoft.Extensions.Options;

using System.Globalization;
using System.Net;

namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// Asks Wolfram Alpha's Full Results API a question and hands back what it answered.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="HttpClient"/> behind this is one the factory hands out, so the resilience the
/// client was registered with — the retries, the circuit breaker, the longer waits — is already
/// wrapped around every request made here. Nothing in this class retries anything itself.
/// </para>
/// <para>
/// Everything that can go wrong with the asking leaves here as a
/// <see cref="WolframAlphaException"/>, so a caller has one kind of failure to answer for rather
/// than the several shapes a failed HTTP call can take.
/// </para>
/// </remarks>
public sealed class WolframAlphaFullResultsClient
{
    /// <summary>
    /// The name the client is registered under, and the one to ask the factory for.
    /// </summary>
    public const string HttpClientName = "WolframAlpha";

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<WolframAlphaOptions> _options;

    public WolframAlphaFullResultsClient(HttpClient httpClient, IOptionsMonitor<WolframAlphaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
    }

    /// <summary>
    /// Asks <paramref name="input"/> and returns the answer.
    /// </summary>
    /// <param name="input">
    /// The question, written the way it would be typed into the site — <c>123+234</c>, and so on.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="input"/> is blank.</exception>
    /// <exception cref="WolframAlphaException">
    /// The service could not be reached, took too long, refused the app id, or answered with
    /// something that is not an answer.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public async Task<WolframAlphaQueryResult> QueryAsync(string input, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildQueryUri(input));
        using var response = await SendAsync(request, cancellationToken);

        await using var content = await ReadContentAsync(response, cancellationToken);

        return await WolframAlphaQueryResultReader.ReadAsync(content, cancellationToken);
    }

    /// <summary>
    /// Builds the address of one query.
    /// </summary>
    /// <remarks>
    /// Asking for plaintext alone is asking for the whole of what is read back here. The service
    /// otherwise renders every pod as an image and sends the addresses of them all, which is work
    /// done and bytes sent for a picture nothing will ever fetch.
    /// </remarks>
    private Uri BuildQueryUri(string input)
    {
        var options = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(options.AppId))
        {
            throw new WolframAlphaException(
                $"No Wolfram Alpha app id is configured. Set {WolframAlphaOptions.SectionName}:{nameof(WolframAlphaOptions.AppId)}.");
        }

        return new Uri(
            string.Create(
                CultureInfo.InvariantCulture,
                $"query?appid={Uri.EscapeDataString(options.AppId)}&input={Uri.EscapeDataString(input)}&format=plaintext&output=xml"),
            UriKind.Relative);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha could not be reached.", exception);
        }
        // Every wait the resilience pipeline enforces ends here, as does the client's own. The
        // caller's cancellation reads the same way and is not ours to answer for, so it is let by.
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WolframAlphaException("Wolfram Alpha took too long to answer.", exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw new WolframAlphaException(response.StatusCode switch
            {
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                    "Wolfram Alpha refused the app id.",
                HttpStatusCode.TooManyRequests =>
                    "Wolfram Alpha has been asked too much for now.",
                _ =>
                    $"Wolfram Alpha answered with {(int)response.StatusCode} {response.StatusCode}.",
            });
        }
    }

    private static async Task<Stream> ReadContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha's answer could not be read.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WolframAlphaException("Wolfram Alpha took too long to answer.", exception);
        }
    }
}
