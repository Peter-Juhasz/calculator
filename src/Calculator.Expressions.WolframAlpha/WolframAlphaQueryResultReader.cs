using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// Reads the JSON a query comes back as into a <see cref="WolframAlphaQueryResult"/>.
/// </summary>
/// <remarks>
/// The whole of an answer is wrapped in one <c>queryresult</c>. Anything that arrives without it
/// is something other than an answer — a gateway's own page, most often — and is turned away here
/// rather than left to look like an answer that says nothing.
/// </remarks>
public static class WolframAlphaQueryResultReader
{
    /// <summary>
    /// Reads <paramref name="json"/> as the answer to one query.
    /// </summary>
    /// <exception cref="WolframAlphaException">
    /// The JSON is malformed, or is not an answer to a query.
    /// </exception>
    public static WolframAlphaQueryResult Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            return Unwrap(JsonSerializer.Deserialize(json, WolframAlphaJsonContext.Default.WolframAlphaAnswer));
        }
        catch (JsonException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha sent back something unreadable.", exception);
        }
    }

    /// <summary>
    /// Reads the answer to one query out of <paramref name="stream"/>.
    /// </summary>
    /// <exception cref="WolframAlphaException">
    /// The JSON is malformed, or is not an answer to a query.
    /// </exception>
    public static async ValueTask<WolframAlphaQueryResult> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            return Unwrap(
                await JsonSerializer.DeserializeAsync(
                    stream,
                    WolframAlphaJsonContext.Default.WolframAlphaAnswer,
                    cancellationToken));
        }
        catch (JsonException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha sent back something unreadable.", exception);
        }
    }

    private static WolframAlphaQueryResult Unwrap(WolframAlphaAnswer? answer) =>
        answer?.QueryResult
        ?? throw new WolframAlphaException("Wolfram Alpha sent back something other than an answer.");

}

/// <summary>
/// The one thing an answer is wrapped in, and the only reason this exists.
/// </summary>
internal sealed record WolframAlphaAnswer(WolframAlphaQueryResult? QueryResult);

/// <summary>
/// The reading of an answer, worked out at compile time so that nothing has to be discovered by
/// reflection when the app is compiled ahead of time.
/// </summary>
/// <remarks>
/// The names on the wire are lower case throughout, which is what the web defaults read.
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(WolframAlphaAnswer))]
internal sealed partial class WolframAlphaJsonContext : JsonSerializerContext;
