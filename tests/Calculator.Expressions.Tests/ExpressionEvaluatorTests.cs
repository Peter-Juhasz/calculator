namespace Calculator.Expressions.Tests;

/// <summary>
/// Working out what an expression written on one line comes to.
/// </summary>
[TestClass]
public sealed class ExpressionEvaluatorTests
{
    [TestMethod]
    public void Evaluate_ASingleNumber_IsThatNumber()
    {
        Assert.AreEqual(42m, ExpressionEvaluator.Evaluate("42"));
    }

    [TestMethod]
    public void Evaluate_Addition_AddsInOrder()
    {
        Assert.AreEqual(6m, ExpressionEvaluator.Evaluate("1+2+3"));
    }

    [TestMethod]
    public void Evaluate_Subtraction_TakesFromTheLeft()
    {
        Assert.AreEqual(5m, ExpressionEvaluator.Evaluate("10-3-2"));
    }

    [TestMethod]
    public void Evaluate_Division_DividesFromTheLeft()
    {
        Assert.AreEqual(10m, ExpressionEvaluator.Evaluate("100/5/2"));
    }

    [TestMethod]
    public void Evaluate_MultiplicationAndAddition_MultipliesFirst()
    {
        Assert.AreEqual(14m, ExpressionEvaluator.Evaluate("2+3*4"));
    }

    [TestMethod]
    public void Evaluate_DivisionAndSubtraction_DividesFirst()
    {
        Assert.AreEqual(8m, ExpressionEvaluator.Evaluate("10-4/2"));
    }

    [TestMethod]
    public void Evaluate_Parentheses_AreWorkedOutBeforeWhatSurroundsThem()
    {
        Assert.AreEqual(20m, ExpressionEvaluator.Evaluate("(2+3)*4"));
    }

    [TestMethod]
    public void Evaluate_NestedParentheses_AreWorkedOutFromTheInside()
    {
        Assert.AreEqual(27m, ExpressionEvaluator.Evaluate("3*((1+2)*(1+2))"));
    }

    [TestMethod]
    public void Evaluate_LeadingMinus_NegatesWhatFollows()
    {
        Assert.AreEqual(-5m, ExpressionEvaluator.Evaluate("-5"));
    }

    [TestMethod]
    public void Evaluate_MinusBeforeAGroup_NegatesTheWholeGroup()
    {
        Assert.AreEqual(-7m, ExpressionEvaluator.Evaluate("-(3+4)"));
    }

    [TestMethod]
    public void Evaluate_MinusAfterAnOperator_NegatesTheOperandOnly()
    {
        Assert.AreEqual(2m, ExpressionEvaluator.Evaluate("5+-3"));
    }

    [TestMethod]
    public void Evaluate_DecimalPoint_IsReadAsAFraction()
    {
        Assert.AreEqual(3.75m, ExpressionEvaluator.Evaluate("1.5+2.25"));
    }

    [TestMethod]
    public void Evaluate_DecimalComma_IsReadTheSameWayAsAPoint()
    {
        Assert.AreEqual(3.75m, ExpressionEvaluator.Evaluate("1,5+2,25"));
    }

    [TestMethod]
    public void Evaluate_APower_RaisesTheBaseToIt()
    {
        Assert.AreEqual(1024m, ExpressionEvaluator.Evaluate("2^10"));
    }

    [TestMethod]
    public void Evaluate_PowersInARow_AreWorkedOutFromTheRight()
    {
        // 2^(3^2), not (2^3)^2, which would be 64.
        Assert.AreEqual(512m, ExpressionEvaluator.Evaluate("2^3^2"));
    }

    [TestMethod]
    public void Evaluate_APowerAndAMultiplication_RaisesFirst()
    {
        Assert.AreEqual(18m, ExpressionEvaluator.Evaluate("2*3^2"));
    }

    [TestMethod]
    public void Evaluate_AMinusBeforeAPower_NegatesTheResultOfThePower()
    {
        Assert.AreEqual(-4m, ExpressionEvaluator.Evaluate("-2^2"));
    }

    [TestMethod]
    public void Evaluate_ANegativeExponent_IsTheReciprocal()
    {
        Assert.AreEqual(0.25m, ExpressionEvaluator.Evaluate("2^-2"));
    }

    [TestMethod]
    public void Evaluate_AnythingToThePowerOfZero_IsOne()
    {
        Assert.AreEqual(1m, ExpressionEvaluator.Evaluate("5^0"));
    }

    [TestMethod]
    public void Evaluate_AWholePowerOfADecimal_IsExact()
    {
        Assert.AreEqual(1.21m, ExpressionEvaluator.Evaluate("1.1^2"));
    }

    [TestMethod]
    public void Evaluate_APowerOfAGroup_RaisesTheWholeGroup()
    {
        Assert.AreEqual(27m, ExpressionEvaluator.Evaluate("(1+2)^3"));
    }

    [TestMethod]
    public void Evaluate_AFractionalPower_IsWorkedOutApproximately()
    {
        Assert.AreEqual(3m, System.Math.Round(ExpressionEvaluator.Evaluate("9^0.5"), 10));
    }

    [TestMethod]
    public void Evaluate_AFractionalPowerOfANegativeNumber_HasNoAnswer()
    {
        Assert.ThrowsExactly<ArithmeticException>(() => ExpressionEvaluator.Evaluate("(0-8)^0.5"));
    }

    [TestMethod]
    public void Evaluate_ZeroToANegativePower_HasNoAnswer()
    {
        Assert.ThrowsExactly<DivideByZeroException>(() => ExpressionEvaluator.Evaluate("0^-1"));
    }

    [TestMethod]
    public void Evaluate_APowerTooLargeToHold_Overflows()
    {
        Assert.ThrowsExactly<OverflowException>(() => ExpressionEvaluator.Evaluate("10^100"));
    }

    [TestMethod]
    public void Evaluate_AFactorial_MultipliesEveryWholeNumberUpToIt()
    {
        Assert.AreEqual(120m, ExpressionEvaluator.Evaluate("5!"));
    }

    [TestMethod]
    public void Evaluate_TheFactorialOfZero_IsOne()
    {
        Assert.AreEqual(1m, ExpressionEvaluator.Evaluate("0!"));
    }

    [TestMethod]
    public void Evaluate_TwoExclamationMarks_TakesTheFactorialTwice()
    {
        // (3!)! is 6!, which is 720.
        Assert.AreEqual(720m, ExpressionEvaluator.Evaluate("3!!"));
    }

    [TestMethod]
    public void Evaluate_AFactorialUnderAPower_IsTakenFirst()
    {
        Assert.AreEqual(36m, ExpressionEvaluator.Evaluate("3!^2"));
    }

    [TestMethod]
    public void Evaluate_AFactorialInAnExponent_IsTakenFirst()
    {
        Assert.AreEqual(64m, ExpressionEvaluator.Evaluate("2^3!"));
    }

    [TestMethod]
    public void Evaluate_AFactorialOfAGroup_TakesTheWholeGroup()
    {
        Assert.AreEqual(24m, ExpressionEvaluator.Evaluate("(2+2)!"));
    }

    [TestMethod]
    public void Evaluate_AMinusBeforeAFactorial_NegatesTheResult()
    {
        Assert.AreEqual(-6m, ExpressionEvaluator.Evaluate("-3!"));
    }

    [TestMethod]
    public void Evaluate_TheFactorialOfAFraction_HasNoAnswer()
    {
        Assert.ThrowsExactly<ArithmeticException>(() => ExpressionEvaluator.Evaluate("2.5!"));
    }

    [TestMethod]
    public void Evaluate_TheFactorialOfANegativeNumber_HasNoAnswer()
    {
        Assert.ThrowsExactly<ArithmeticException>(() => ExpressionEvaluator.Evaluate("(0-3)!"));
    }

    [TestMethod]
    public void Evaluate_AFactorialTooLargeToHold_Overflows()
    {
        // A decimal runs out between 27! and 28!.
        Assert.ThrowsExactly<OverflowException>(() => ExpressionEvaluator.Evaluate("28!"));
    }

    [TestMethod]
    public void Evaluate_AnExclamationMarkWithNothingBeforeIt_IsRejected()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("!5"));
    }

    [TestMethod]
    public void Evaluate_TenthsAddedTogether_ComeOutExactly()
    {
        // The whole reason for working in decimal: in binary floating point this lands a hair
        // short of 0.3, and every later digit inherits the miss.
        Assert.AreEqual(0.3m, ExpressionEvaluator.Evaluate("0.1+0.2"));
    }

    [TestMethod]
    public void Evaluate_ADivisionThatDoesNotComeOutEven_KeepsGoingToTheDigitsADecimalHolds()
    {
        Assert.AreEqual(0.3333333333m, System.Math.Round(ExpressionEvaluator.Evaluate("1/3"), 10));
    }

    [TestMethod]
    public void Evaluate_ANumberWithMoreDigitsThanADecimalHolds_Overflows()
    {
        Assert.ThrowsExactly<OverflowException>(() => ExpressionEvaluator.Evaluate(new string('9', 40)));
    }

    [TestMethod]
    public void Evaluate_AResultBiggerThanADecimalHolds_Overflows()
    {
        var large = new string('9', 28);

        Assert.ThrowsExactly<OverflowException>(() => ExpressionEvaluator.Evaluate(large + "*" + large));
    }

    [TestMethod]
    public void Evaluate_Whitespace_ChangesNothing()
    {
        Assert.AreEqual(20m, ExpressionEvaluator.Evaluate("  ( 2 + 3 ) * 4  "));
    }

    [TestMethod]
    public void Evaluate_ANumberBeforeAGroup_MultipliesThem()
    {
        Assert.AreEqual(14m, ExpressionEvaluator.Evaluate("2(3+4)"));
    }

    [TestMethod]
    public void Evaluate_ANumberAfterAGroup_MultipliesThem()
    {
        Assert.AreEqual(14m, ExpressionEvaluator.Evaluate("(3+4)2"));
    }

    [TestMethod]
    public void Evaluate_TwoGroupsSideBySide_MultipliesThem()
    {
        Assert.AreEqual(12m, ExpressionEvaluator.Evaluate("(1+2)(1+3)"));
    }

    [TestMethod]
    public void Evaluate_TwoBareNumbersSideBySide_IsRejected()
    {
        // A product would be a generous reading of what is far more likely a typing slip.
        Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("2 3"));
    }

    [TestMethod]
    public void Evaluate_DividingByZero_SaysSo()
    {
        Assert.ThrowsExactly<DivideByZeroException>(() => ExpressionEvaluator.Evaluate("1/0"));
    }

    [TestMethod]
    public void Evaluate_AnUnknownCharacter_IsRejected()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("1+#"));
    }

    [TestMethod]
    public void Evaluate_AClosingParenthesisThatOpensNothing_IsRejected()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("1+2)"));
    }

    [TestMethod]
    public void Evaluate_TwoDecimalSeparatorsInOneNumber_IsRejected()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("1.2.3"));
    }

    [TestMethod]
    public void Evaluate_AnExpressionThatStopsAfterAnOperator_IsReportedAsUnfinished()
    {
        var exception = Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("1+"));

        Assert.IsTrue(exception.IsIncomplete, "Half a typed expression is unfinished rather than wrong.");
    }

    [TestMethod]
    public void Evaluate_AParenthesisLeftOpen_IsReportedAsUnfinished()
    {
        var exception = Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("(1+2"));

        Assert.IsTrue(exception.IsIncomplete);
    }

    [TestMethod]
    public void Evaluate_AnEmptyPairOfParentheses_IsReportedAsUnfinished()
    {
        // This is what a fraction looks like before either half has been filled in.
        var exception = Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("()/()"));

        Assert.IsTrue(exception.IsIncomplete);
    }

    [TestMethod]
    public void Evaluate_NothingAtAll_IsReportedAsUnfinished()
    {
        var exception = Assert.ThrowsExactly<ExpressionFormatException>(() => ExpressionEvaluator.Evaluate("   "));

        Assert.IsTrue(exception.IsIncomplete);
    }

    [TestMethod]
    public void Evaluate_WithNoTypeNamed_WorksInDecimal()
    {
        Assert.AreEqual(typeof(decimal), ExpressionEvaluator.Evaluate("1").GetType());
    }

    [TestMethod]
    public void Evaluate_InDouble_ReadsTheSameExpressionTheSameWay()
    {
        Assert.AreEqual(20d, ExpressionEvaluator.Evaluate<double>("(2+3)*4"));
    }

    [TestMethod]
    public void Evaluate_InDouble_CarriesTheMissThatBinaryFloatingPointMakes()
    {
        // The counterpart of the decimal test above: naming double is asking for its arithmetic,
        // misses and all.
        Assert.AreNotEqual(0.3d, ExpressionEvaluator.Evaluate<double>("0.1+0.2"));
    }

    [TestMethod]
    public void Evaluate_InAWholeNumberType_DividesTheWayThatTypeDivides()
    {
        Assert.AreEqual(2, ExpressionEvaluator.Evaluate<int>("10/4"));
    }

    [TestMethod]
    public void Evaluate_InAWholeNumberType_RejectsANumberWithAFraction()
    {
        Assert.ThrowsExactly<OverflowException>(() => ExpressionEvaluator.Evaluate<int>("1.5"));
    }

    [TestMethod]
    public void Evaluate_AResultBiggerThanTheNamedTypeHolds_Overflows()
    {
        // Checked arithmetic is what turns a whole number type's silent wraparound into an answer
        // the reader is told about.
        Assert.ThrowsExactly<OverflowException>(() => ExpressionEvaluator.Evaluate<int>("2000000000+2000000000"));
    }

    [TestMethod]
    public void Evaluate_AFactorialBiggerThanTheNamedTypeHolds_Overflows()
    {
        // A double answers with infinity rather than overflowing, so the loop has to stop itself.
        Assert.ThrowsExactly<OverflowException>(() => ExpressionEvaluator.Evaluate<double>("2000!"));
    }
}
