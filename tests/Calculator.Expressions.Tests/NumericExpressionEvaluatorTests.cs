using System.Xml.Linq;

namespace Calculator.Expressions.Tests;

/// <summary>
/// Flattening the MathML a math-mode rich edit box hands back into the one line of text the
/// arithmetic done here reads, and what an evaluator handed that markup answers.
/// </summary>
[TestClass]
public sealed class NumericExpressionEvaluatorTests
{
    [TestMethod]
    public void Linearize_AnEmptyDocument_IsEmpty()
    {
        Assert.AreEqual(string.Empty, Linearize(Document("")));
    }

    [TestMethod]
    public void Linearize_ASum_ReadsAlongOneLine()
    {
        Assert.AreEqual("1+2", Linearize(Document("<mn>1</mn><mo>+</mo><mn>2</mn>")));
    }

    [TestMethod]
    public void Linearize_ARow_IsFlattenedIntoWhatSurroundsIt()
    {
        Assert.AreEqual(
            "(1+2)*3",
            Linearize(Document(
                "<mrow><mo>(</mo><mn>1</mn><mo>+</mo><mn>2</mn><mo>)</mo></mrow><mo>×</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_AFraction_ParenthesisesBothHalves()
    {
        // The bar groups everything above and below it, which a plain slash does not.
        Assert.AreEqual(
            "(1+2)/(3)",
            Linearize(Document(
                "<mfrac><mrow><mn>1</mn><mo>+</mo><mn>2</mn></mrow><mn>3</mn></mfrac>")));
    }

    [TestMethod]
    public void Linearize_AFractionInsideASum_KeepsTheSumUntouched()
    {
        var expression = Linearize(Document(
            "<mn>1</mn><mo>+</mo><mfrac><mn>1</mn><mn>2</mn></mfrac>"));

        Assert.AreEqual(1.5m, ExpressionEvaluator.Evaluate(expression));
    }

    [TestMethod]
    public void Linearize_TheMinusSign_BecomesAHyphen()
    {
        Assert.AreEqual("5-3", Linearize(Document("<mn>5</mn><mo>−</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_TheDivisionSign_BecomesASlash()
    {
        Assert.AreEqual("6/3", Linearize(Document("<mn>6</mn><mo>÷</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_ADotOperator_BecomesAnAsterisk()
    {
        Assert.AreEqual("6*3", Linearize(Document("<mn>6</mn><mo>⋅</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_InvisibleTimes_BecomesAnAsterisk()
    {
        Assert.AreEqual("2*3", Linearize(Document("<mn>2</mn><mo>⁢</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_InvisiblePlus_BecomesAPlus()
    {
        // What holds the two halves of a mixed number such as 1 1/2 together.
        var expression = Linearize(Document(
            "<mn>1</mn><mo>⁤</mo><mfrac><mn>1</mn><mn>2</mn></mfrac>"));

        Assert.AreEqual(1.5m, ExpressionEvaluator.Evaluate(expression));
    }

    [TestMethod]
    public void Linearize_SpacesThatOnlyGroupDigits_AreDropped()
    {
        Assert.AreEqual("1234", Linearize(Document("<mn>1 2 3 4</mn>")));
    }

    [TestMethod]
    public void Linearize_Brackets_AreReadAsParentheses()
    {
        Assert.AreEqual(
            "(1+2)",
            Linearize(Document("<mo>[</mo><mn>1</mn><mo>+</mo><mn>2</mn><mo>]</mo>")));
    }

    [TestMethod]
    public void Linearize_TheBoxShownForAPartNotFilledIn_IsDropped()
    {
        var expression = Linearize(Document("<mn>1</mn><mo>+</mo><mi>⬚</mi>"));

        Assert.AreEqual("1+", expression);
    }

    [TestMethod]
    public void Linearize_AName_IsRejectedForNow()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(
            () => Linearize(Document("<mi>x</mi><mo>+</mo><mn>1</mn>")));
    }

    [TestMethod]
    public void Linearize_ASuperscript_BecomesAPower()
    {
        Assert.AreEqual(
            "(4)^(2)",
            Linearize(Document("<msup><mn>4</mn><mn>2</mn></msup>")));
    }

    [TestMethod]
    public void Linearize_ASuperscriptOverASum_RaisesTheWholeSum()
    {
        // A raised position needs no brackets to say where the exponent ends; written flat, the
        // parentheses have to say it.
        var expression = Linearize(Document(
            "<msup><mn>2</mn><mrow><mn>1</mn><mo>+</mo><mn>2</mn></mrow></msup>"));

        Assert.AreEqual(8m, ExpressionEvaluator.Evaluate(expression));
    }

    [TestMethod]
    public void Linearize_AnExclamationMark_IsAFactorial()
    {
        Assert.AreEqual(120m, ExpressionEvaluator.Evaluate(
            Linearize(Document("<mn>5</mn><mo>!</mo>"))));
    }

    [TestMethod]
    public void Linearize_ARoot_IsRejectedForNow()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(
            () => Linearize(Document("<msqrt><mn>9</mn></msqrt>")));
    }

    [TestMethod]
    public async Task EvaluateAsync_MarkupHandedToADecimal_IsWorkedOutAndWrittenOut()
    {
        var result = await new DecimalExpressionEvaluator().EvaluateAsync(
            XDocument.Parse(Document("<mn>1</mn><mo>+</mo><mn>2</mn>")),
            CancellationToken.None);

        Assert.AreEqual("3", result);
    }

    [TestMethod]
    public async Task EvaluateAsync_MarkupHandedToWholeNumbers_KeepsOnlyTheWholePartOfADivision()
    {
        // The same markup, read by the same rules, and answered by the arithmetic that was asked
        // for rather than by the one that read it.
        var result = await new BigIntegerExpressionEvaluator().EvaluateAsync(
            XDocument.Parse(Document("<mfrac><mn>5</mn><mn>2</mn></mfrac>")),
            CancellationToken.None);

        Assert.AreEqual("2", result);
    }

    [TestMethod]
    public async Task EvaluateAsync_MarkupNotYetFinished_SaysSoAsAHint()
    {
        var evaluator = new DecimalExpressionEvaluator();

        var exception = await Assert.ThrowsExactlyAsync<ExpressionFormatException>(
            async () => await evaluator.EvaluateAsync(
                XDocument.Parse(Document("<mn>1</mn><mo>+</mo>")),
                CancellationToken.None));

        Assert.IsTrue(exception.IsIncomplete);
    }

    private static string Linearize(string mathML) =>
        NumericExpressionEvaluator.Linearize(XDocument.Parse(mathML));

    private static string Document(string content) =>
        $"<math xmlns=\"http://www.w3.org/1998/Math/MathML\">{content}</math>";
}
