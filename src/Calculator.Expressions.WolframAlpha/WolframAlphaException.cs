namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// Says that Wolfram Alpha could not be asked, or answered with something other than an answer.
/// </summary>
/// <remarks>
/// This is about the asking rather than the expression: the service was unreachable, took too
/// long, refused the app id, or sent back something that could not be read. An expression the
/// service read and could not make sense of is an <see cref="ExpressionFormatException"/>
/// instead, because that is a fault in what was typed.
/// </remarks>
public sealed class WolframAlphaException : Exception
{
    public WolframAlphaException()
    {
    }

    public WolframAlphaException(string message)
        : base(message)
    {
    }

    public WolframAlphaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
