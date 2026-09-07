using System.Text.Json;

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
    /// The names on the wire are lower case throughout, which is what the web defaults read.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

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
            return Unwrap(JsonSerializer.Deserialize<WolframAlphaAnswer>(json, Options));
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
                await JsonSerializer.DeserializeAsync<WolframAlphaAnswer>(stream, Options, cancellationToken));
        }
        catch (JsonException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha sent back something unreadable.", exception);
        }
    }

    private static WolframAlphaQueryResult Unwrap(WolframAlphaAnswer? answer) =>
        answer?.QueryResult
        ?? throw new WolframAlphaException("Wolfram Alpha sent back something other than an answer.");

    /// <summary>
    /// The one thing an answer is wrapped in, and the only reason this exists.
    /// </summary>
    private sealed record WolframAlphaAnswer(WolframAlphaQueryResult? QueryResult);
}
