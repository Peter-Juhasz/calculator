namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// Hands the expression to Wolfram Alpha and puts back what it says the answer is.
/// </summary>
/// <remarks>
/// <para>
/// The other evaluators work an expression out here, in a number type chosen in advance, and are
/// bounded by what that type can say. This one does no arithmetic at all: it sends the line off
/// and reads back the answer, so it can be asked things the grammar in this app does not have —
/// a square root, a logarithm, a constant by name — and answers a plain sum with the same
/// sentence a person would write.
/// </para>
/// <para>
/// The cost is that every reading is a request over the network, which takes as long as it takes
/// and can simply fail.
/// </para>
/// </remarks>
public sealed class WolframAlphaExpressionEvaluator : IExpressionEvaluator
{
    private readonly WolframAlphaFullResultsClient _client;

    public WolframAlphaExpressionEvaluator(WolframAlphaFullResultsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    public string DisplayName => "Wolfram Alpha";

    /// <exception cref="ExpressionFormatException">
    /// Wolfram Alpha could not make sense of the expression.
    /// </exception>
    /// <exception cref="WolframAlphaException">
    /// Wolfram Alpha could not be asked, or did not answer with a result.
    /// </exception>
    public async ValueTask<string> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ExpressionFormatException("The expression is empty.", isIncomplete: true);
        }

        var result = await _client.QueryAsync(expression, cancellationToken);

        if (result.Error is { } error)
        {
            throw new WolframAlphaException(error.Message ?? "Wolfram Alpha reported an error.");
        }

        if (!result.Success)
        {
            // Half a typed expression looks exactly like a wrong one from here — the service says
            // only that it could not read it, not why. Said as a hint, because most of the time
            // it is the ordinary state of an expression that is still being typed.
            throw new ExpressionFormatException(
                "Wolfram Alpha could not make sense of the expression.",
                isIncomplete: true);
        }

        return result.PrimaryPlainText
            ?? throw new WolframAlphaException("Wolfram Alpha answered without a result to show.");
    }
}
