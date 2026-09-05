namespace Calculator.Expressions.Tests;

/// <summary>
/// Flattening the MathML a math-mode rich edit box hands back into one line of text.
/// </summary>
[TestClass]
public sealed class MathMlLinearizerTests
{
    [TestMethod]
    public void Linearize_NothingAtAll_IsEmpty()
    {
        Assert.AreEqual(string.Empty, MathMlLinearizer.Linearize(string.Empty));
    }

    [TestMethod]
    public void Linearize_AnEmptyDocument_IsEmpty()
    {
        Assert.AreEqual(string.Empty, MathMlLinearizer.Linearize(Document("")));
    }

    [TestMethod]
    public void Linearize_ASum_ReadsAlongOneLine()
    {
        Assert.AreEqual("1+2", MathMlLinearizer.Linearize(Document("<mn>1</mn><mo>+</mo><mn>2</mn>")));
    }

    [TestMethod]
    public void Linearize_ARow_IsFlattenedIntoWhatSurroundsIt()
    {
        Assert.AreEqual(
            "(1+2)*3",
            MathMlLinearizer.Linearize(Document(
                "<mrow><mo>(</mo><mn>1</mn><mo>+</mo><mn>2</mn><mo>)</mo></mrow><mo>×</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_AFraction_ParenthesisesBothHalves()
    {
        // The bar groups everything above and below it, which a plain slash does not.
        Assert.AreEqual(
            "(1+2)/(3)",
            MathMlLinearizer.Linearize(Document(
                "<mfrac><mrow><mn>1</mn><mo>+</mo><mn>2</mn></mrow><mn>3</mn></mfrac>")));
    }

    [TestMethod]
    public void Linearize_AFractionInsideASum_KeepsTheSumUntouched()
    {
        var expression = MathMlLinearizer.Linearize(Document(
            "<mn>1</mn><mo>+</mo><mfrac><mn>1</mn><mn>2</mn></mfrac>"));

        Assert.AreEqual(1.5m, ExpressionEvaluator.Evaluate(expression));
    }

    [TestMethod]
    public void Linearize_TheMinusSign_BecomesAHyphen()
    {
        Assert.AreEqual("5-3", MathMlLinearizer.Linearize(Document("<mn>5</mn><mo>−</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_TheDivisionSign_BecomesASlash()
    {
        Assert.AreEqual("6/3", MathMlLinearizer.Linearize(Document("<mn>6</mn><mo>÷</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_ADotOperator_BecomesAnAsterisk()
    {
        Assert.AreEqual("6*3", MathMlLinearizer.Linearize(Document("<mn>6</mn><mo>⋅</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_InvisibleTimes_BecomesAnAsterisk()
    {
        Assert.AreEqual("2*3", MathMlLinearizer.Linearize(Document("<mn>2</mn><mo>⁢</mo><mn>3</mn>")));
    }

    [TestMethod]
    public void Linearize_InvisiblePlus_BecomesAPlus()
    {
        // What holds the two halves of a mixed number such as 1 1/2 together.
        var expression = MathMlLinearizer.Linearize(Document(
            "<mn>1</mn><mo>⁤</mo><mfrac><mn>1</mn><mn>2</mn></mfrac>"));

        Assert.AreEqual(1.5m, ExpressionEvaluator.Evaluate(expression));
    }

    [TestMethod]
    public void Linearize_SpacesThatOnlyGroupDigits_AreDropped()
    {
        Assert.AreEqual("1234", MathMlLinearizer.Linearize(Document("<mn>1 2 3 4</mn>")));
    }

    [TestMethod]
    public void Linearize_Brackets_AreReadAsParentheses()
    {
        Assert.AreEqual(
            "(1+2)",
            MathMlLinearizer.Linearize(Document("<mo>[</mo><mn>1</mn><mo>+</mo><mn>2</mn><mo>]</mo>")));
    }

    [TestMethod]
    public void Linearize_TheBoxShownForAPartNotFilledIn_IsDropped()
    {
        var expression = MathMlLinearizer.Linearize(Document("<mn>1</mn><mo>+</mo><mi>⬚</mi>"));

        Assert.AreEqual("1+", expression);
    }

    [TestMethod]
    public void Linearize_AName_IsRejectedForNow()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(
            () => MathMlLinearizer.Linearize(Document("<mi>x</mi><mo>+</mo><mn>1</mn>")));
    }

    [TestMethod]
    public void Linearize_ASuperscript_IsRejectedForNow()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(
            () => MathMlLinearizer.Linearize(Document("<msup><mn>4</mn><mn>2</mn></msup>")));
    }

    [TestMethod]
    public void Linearize_ARoot_IsRejectedForNow()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(
            () => MathMlLinearizer.Linearize(Document("<msqrt><mn>9</mn></msqrt>")));
    }

    [TestMethod]
    public void Linearize_MarkupThatIsNotWellFormed_IsRejected()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(() => MathMlLinearizer.Linearize("<math><mn>1</math>"));
    }

    [TestMethod]
    public void Linearize_AByteOrderMarkAndTerminator_AreIgnored()
    {
        Assert.AreEqual("1+2", MathMlLinearizer.Linearize("﻿" + Document("<mn>1</mn><mo>+</mo><mn>2</mn>") + "\0"));
    }

    private static string Document(string content) =>
        $"<math xmlns=\"http://www.w3.org/1998/Math/MathML\">{content}</math>";
}
