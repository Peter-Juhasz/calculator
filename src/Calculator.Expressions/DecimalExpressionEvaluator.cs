using System.Globalization;

namespace Calculator.Expressions;

/// <summary>
/// Works expressions out in <see cref="decimal"/>, which is what a calculator ordinarily wants:
/// the numbers people write are held as they wrote them, a tenth is a tenth, and 0.1 + 0.2 is
/// exactly 0.3. The cost is a narrower range, which a decimal announces by overflowing rather
/// than quietly drifting off.
/// </summary>
public sealed class DecimalExpressionEvaluator : NumericExpressionEvaluator
{
    /// <summary>
    /// Up to twelve decimals are shown, grouped for reading. Trailing zeros are dropped, so a
    /// whole number reads as one. A division that does not come out even carries far more digits
    /// than that, and is rounded to fit rather than run off the line.
    /// </summary>
    private const string ResultFormat = "#,##0.#################";

    public override string DisplayName => "Decimal";

    public override ValueTask<string> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var value = ExpressionEvaluator.Evaluate<decimal>(expression);

        return ValueTask.FromResult(value.ToString(ResultFormat, CultureInfo.CurrentCulture));
    }
}
