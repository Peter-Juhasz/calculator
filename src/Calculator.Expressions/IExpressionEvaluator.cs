namespace Calculator.Expressions;

/// <summary>
/// One way of working out an expression, from the text that was typed to the text of its answer.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExpressionEvaluator"/> underneath reads an expression in whatever number type it is
/// given, and each implementation of this interface settles that choice and the spelling of the
/// result that follows from it. What a caller sees is a name to offer the reader and a string to
/// put on the line: which type did the arithmetic, and how its value is written out, are the
/// implementation's business.
/// </para>
/// </remarks>
public interface IExpressionEvaluator
{
    /// <summary>
    /// What this way of working things out is called where the reader chooses between them.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Works out <paramref name="expression"/> and writes its value out for reading.
    /// </summary>
    /// <exception cref="ExpressionFormatException">The expression could not be read.</exception>
    /// <exception cref="DivideByZeroException">The expression divides by zero.</exception>
    /// <exception cref="OverflowException">
    /// The value is larger than the number type behind this evaluator can hold.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// The expression asks for something with no answer, such as a factorial of a fraction.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    ValueTask<string> EvaluateAsync(string expression, CancellationToken cancellationToken);
}
