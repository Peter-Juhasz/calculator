using System.Xml.Linq;

namespace Calculator.Expressions;

/// <summary>
/// One way of working out an expression, from what was typed to the text of its answer.
/// </summary>
/// <remarks>
/// <para>
/// An expression arrives either as one line of text or as the MathML of what is on the screen.
/// The second is the one a caller with a math-mode edit box should hand over: what notation can
/// be made sense of differs from one evaluator to the next — a root or a name means nothing to
/// arithmetic done in a <see cref="decimal"/>, and a great deal to a service that does algebra —
/// so the reading of the markup belongs to the implementation, which is the only thing that knows
/// what it can read.
/// </para>
/// <para>
/// What a caller sees either way is a name to offer the reader and a string to put on the line:
/// what worked the expression out, and how its value is written, are the implementation's
/// business.
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

    /// <summary>
    /// Works out the expression written in <paramref name="mathML"/> and writes its value out for
    /// reading.
    /// </summary>
    /// <param name="mathML">
    /// The markup of one expression as it stands on the screen, with a <c>math</c> element at the
    /// root of it.
    /// </param>
    /// <exception cref="ExpressionFormatException">
    /// The expression could not be read, or is written in notation this evaluator does not
    /// support.
    /// </exception>
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
    ValueTask<string> EvaluateAsync(XDocument mathML, CancellationToken cancellationToken);
}
