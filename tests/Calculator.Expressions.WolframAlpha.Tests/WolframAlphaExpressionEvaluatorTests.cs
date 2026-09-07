using System.Xml.Linq;

namespace Calculator.Expressions.WolframAlpha.Tests;

/// <summary>
/// Writing what is built up on the screen out in the syntax Wolfram Alpha reads, which is a good
/// deal more than the arithmetic in this app can be asked for.
/// </summary>
[TestClass]
public sealed class WolframAlphaExpressionEvaluatorTests
{
    [TestMethod]
    public void Linearize_AnEmptyDocument_IsEmpty()
    {
        Assert.AreEqual(string.Empty, Linearize(""));
    }

    [TestMethod]
    public void Linearize_ASum_ReadsAlongOneLine()
    {
        Assert.AreEqual("1+2", Linearize("<mn>1</mn><mo>+</mo><mn>2</mn>"));
    }

    [TestMethod]
    public void Linearize_AFraction_ParenthesisesBothHalves()
    {
        Assert.AreEqual(
            "(1+2)/(3)",
            Linearize("<mfrac><mrow><mn>1</mn><mo>+</mo><mn>2</mn></mrow><mn>3</mn></mfrac>"));
    }

    [TestMethod]
    public void Linearize_AMultiplicationSign_BecomesAnAsterisk()
    {
        Assert.AreEqual("6*3", Linearize("<mn>6</mn><mo>×</mo><mn>3</mn>"));
    }

    [TestMethod]
    public void Linearize_ASquareRoot_IsAskedForByName()
    {
        // Nothing in the arithmetic done here has a root; the service does, so it is sent one.
        Assert.AreEqual("sqrt(9)", Linearize("<msqrt><mn>9</mn></msqrt>"));
    }

    [TestMethod]
    public void Linearize_ARootOfAnyDegree_IsWrittenAsAFractionalPower()
    {
        Assert.AreEqual("(8)^(1/(3))", Linearize("<mroot><mn>8</mn><mn>3</mn></mroot>"));
    }

    [TestMethod]
    public void Linearize_AName_IsLeftAlone()
    {
        Assert.AreEqual("x+1", Linearize("<mi>x</mi><mo>+</mo><mn>1</mn>"));
    }

    [TestMethod]
    public void Linearize_AFunction_KeepsItsName()
    {
        // The character between the name and what it is applied to says only that this is a
        // function being applied, and says it in a way that is not written down.
        Assert.AreEqual(
            "sin(x)",
            Linearize("<mi>sin</mi><mo>&#x2061;</mo><mrow><mo>(</mo><mi>x</mi><mo>)</mo></mrow>"));
    }

    [TestMethod]
    public void Linearize_ALogarithmWithABase_KeepsTheBaseUnderIt()
    {
        Assert.AreEqual(
            "log_(2)(8)",
            Linearize("<msub><mi>log</mi><mn>2</mn></msub><mrow><mo>(</mo><mn>8</mn><mo>)</mo></mrow>"));
    }

    [TestMethod]
    public void Linearize_ASumSign_IsAskedForByNameWithItsBounds()
    {
        Assert.AreEqual(
            "sum_(i=1)^(10)i",
            Linearize(
                "<munderover><mo>∑</mo><mrow><mi>i</mi><mo>=</mo><mn>1</mn></mrow><mn>10</mn></munderover>" +
                "<mi>i</mi>"));
    }

    [TestMethod]
    public void Linearize_ALimit_KeepsWhatItApproaches()
    {
        Assert.AreEqual(
            "lim_(x->0)(x)/(2)",
            Linearize(
                "<munder><mi>lim</mi><mrow><mi>x</mi><mo>→</mo><mn>0</mn></mrow></munder>" +
                "<mfrac><mi>x</mi><mn>2</mn></mfrac>"));
    }

    [TestMethod]
    public void Linearize_AConstantWithAWordOfItsOwn_IsGivenThatWord()
    {
        Assert.AreEqual("2*pi", Linearize("<mn>2</mn><mo>×</mo><mi>π</mi>"));
    }

    [TestMethod]
    public void Linearize_Infinity_IsGivenTheWordForIt()
    {
        Assert.AreEqual("infinity", Linearize("<mi>∞</mi>"));
    }

    [TestMethod]
    public void Linearize_ATable_IsWrittenAsRowsOfARow()
    {
        Assert.AreEqual(
            "{{1, 2}, {3, 4}}",
            Linearize(
                "<mtable>" +
                "<mtr><mtd><mn>1</mn></mtd><mtd><mn>2</mn></mtd></mtr>" +
                "<mtr><mtd><mn>3</mn></mtd><mtd><mn>4</mn></mtd></mtr>" +
                "</mtable>"));
    }

    [TestMethod]
    public void Linearize_TheBoxShownForAPartNotFilledIn_IsDropped()
    {
        Assert.AreEqual("1+", Linearize("<mn>1</mn><mo>+</mo><mi>&#x2B1A;</mi>"));
    }

    [TestMethod]
    public void Linearize_NotationWithNoSpellingOfItsOwn_IsRejected()
    {
        Assert.ThrowsExactly<ExpressionFormatException>(
            () => Linearize("<mmultiscripts><mi>x</mi><mn>1</mn><none/></mmultiscripts>"));
    }

    [TestMethod]
    public void ParseMathML_TheMarkupOfAnAnswer_IsReadDespiteWhatItCarries()
    {
        // What comes back is laid out over several lines and carries attributes of its own.
        // Neither is part of the expression.
        var result = WolframAlphaExpressionEvaluator.ParseMathML(
            "<math xmlns='http://www.w3.org/1998/Math/MathML'\n" +
            "    mathematica:form='StandardForm'\n" +
            "    xmlns:mathematica='http://www.wolfram.com/XML/'>\n <mn>357</mn>\n</math>");

        Assert.AreEqual("357", MathML.AsNumber(result));
    }

    [TestMethod]
    public void ParseMathML_AnAnswerBuiltUpRatherThanFlat_KeepsTheWayItIsBuilt()
    {
        // What the service can say and a number type cannot: a fraction left as a fraction. It is
        // worth nothing flattened, and is only kept by being read as the markup it came as.
        var result = WolframAlphaExpressionEvaluator.ParseMathML(
            "<math xmlns='http://www.w3.org/1998/Math/MathML'>" +
            "<mfrac><mn>1</mn><mn>3</mn></mfrac></math>");

        Assert.IsNull(MathML.AsNumber(result));
        Assert.AreEqual("mfrac", result.Root?.Elements().Single().Name.LocalName);
    }

    [TestMethod]
    public void ParseMathML_MarkupThatIsNotMarkup_IsRefused()
    {
        Assert.ThrowsExactly<WolframAlphaException>(
            () => WolframAlphaExpressionEvaluator.ParseMathML("<math><mn>357"));
    }

    private static string Linearize(string content) => WolframAlphaExpressionEvaluator.Linearize(
        XDocument.Parse($"<math xmlns=\"http://www.w3.org/1998/Math/MathML\">{content}</math>"));
}
