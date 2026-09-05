namespace Calculator.Expressions.Tests;

/// <summary>
/// Working out what an expression written on one line comes to.
/// </summary>
[TestClass]
public sealed class ExpressionEvaluatorTests
{
    private const double Tolerance = 1e-10;

    [TestMethod]
    public void Evaluate_ASingleNumber_IsThatNumber()
    {
        Assert.AreEqual(42, ExpressionEvaluator.Evaluate("42"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_Addition_AddsInOrder()
    {
        Assert.AreEqual(6, ExpressionEvaluator.Evaluate("1+2+3"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_Subtraction_TakesFromTheLeft()
    {
        Assert.AreEqual(5, ExpressionEvaluator.Evaluate("10-3-2"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_Division_DividesFromTheLeft()
    {
        Assert.AreEqual(10, ExpressionEvaluator.Evaluate("100/5/2"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_MultiplicationAndAddition_MultipliesFirst()
    {
        Assert.AreEqual(14, ExpressionEvaluator.Evaluate("2+3*4"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_DivisionAndSubtraction_DividesFirst()
    {
        Assert.AreEqual(8, ExpressionEvaluator.Evaluate("10-4/2"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_Parentheses_AreWorkedOutBeforeWhatSurroundsThem()
    {
        Assert.AreEqual(20, ExpressionEvaluator.Evaluate("(2+3)*4"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_NestedParentheses_AreWorkedOutFromTheInside()
    {
        Assert.AreEqual(27, ExpressionEvaluator.Evaluate("3*((1+2)*(1+2))"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_LeadingMinus_NegatesWhatFollows()
    {
        Assert.AreEqual(-5, ExpressionEvaluator.Evaluate("-5"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_MinusBeforeAGroup_NegatesTheWholeGroup()
    {
        Assert.AreEqual(-7, ExpressionEvaluator.Evaluate("-(3+4)"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_MinusAfterAnOperator_NegatesTheOperandOnly()
    {
        Assert.AreEqual(2, ExpressionEvaluator.Evaluate("5+-3"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_DecimalPoint_IsReadAsAFraction()
    {
        Assert.AreEqual(3.75, ExpressionEvaluator.Evaluate("1.5+2.25"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_DecimalComma_IsReadTheSameWayAsAPoint()
    {
        Assert.AreEqual(3.75, ExpressionEvaluator.Evaluate("1,5+2,25"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_Whitespace_ChangesNothing()
    {
        Assert.AreEqual(20, ExpressionEvaluator.Evaluate("  ( 2 + 3 ) * 4  "), Tolerance);
    }

    [TestMethod]
    public void Evaluate_ANumberBeforeAGroup_MultipliesThem()
    {
        Assert.AreEqual(14, ExpressionEvaluator.Evaluate("2(3+4)"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_ANumberAfterAGroup_MultipliesThem()
    {
        Assert.AreEqual(14, ExpressionEvaluator.Evaluate("(3+4)2"), Tolerance);
    }

    [TestMethod]
    public void Evaluate_TwoGroupsSideBySide_MultipliesThem()
    {
        Assert.AreEqual(12, ExpressionEvaluator.Evaluate("(1+2)(1+3)"), Tolerance);
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
}
