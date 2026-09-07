using System.Globalization;
using System.Numerics;

namespace Calculator.Expressions;

/// <summary>
/// Works expressions out in <see cref="BigInteger"/>, which counts in whole numbers and has no
/// upper limit: a factorial or a power that a decimal has no room for is answered here in full,
/// to the last digit.
/// </summary>
/// <remarks>
/// The trade is that there are no fractions. A number written with a fractional part is refused,
/// and a division that does not come out even keeps only the whole part of its answer: in whole
/// numbers 5/2 is 2, not 2.5.
/// </remarks>
public sealed class BigIntegerExpressionEvaluator : NumericExpressionEvaluator
{
    /// <summary>
    /// Grouped for reading, with no decimals to show. A result here can run to a great many
    /// digits, and the grouping is the only thing that makes such a number countable by eye.
    /// </summary>
    private const string ResultFormat = "N0";

    public override string DisplayName => "Integer";

    public override ValueTask<string> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = ExpressionEvaluator.Evaluate<BigInteger>(expression);

        return ValueTask.FromResult(value.ToString(ResultFormat, CultureInfo.CurrentCulture));
    }
}
