namespace Calculator.Expressions;

/// <summary>
/// Says that an expression could not be read.
/// </summary>
/// <remarks>
/// <see cref="IsIncomplete"/> tells apart an expression that is wrong from one that has simply
/// not been finished yet. The second is the ordinary state of an expression halfway through
/// being typed, and is worth saying far more quietly than a genuine mistake.
/// </remarks>
public sealed class ExpressionFormatException : Exception
{
    public ExpressionFormatException()
    {
    }

    public ExpressionFormatException(string message)
        : base(message)
    {
    }

    public ExpressionFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ExpressionFormatException(string message, bool isIncomplete)
        : base(message) => IsIncomplete = isIncomplete;

    /// <summary>
    /// Whether the expression ran out rather than went wrong.
    /// </summary>
    public bool IsIncomplete { get; }
}
