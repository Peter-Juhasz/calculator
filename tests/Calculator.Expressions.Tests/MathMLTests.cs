using System.Xml.Linq;

namespace Calculator.Expressions.Tests;

/// <summary>
/// Making the markup of a number, and reading a number back out of markup — which is what settles
/// whether an answer can be handed on as the plain number it is.
/// </summary>
[TestClass]
public sealed class MathMLTests
{
    [TestMethod]
    public void Number_ANumber_IsWrappedInAMathElement()
    {
        Assert.AreEqual(
            "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mn>357</mn></math>",
            MathML.Number("357").ToString(SaveOptions.DisableFormatting));
    }

    [TestMethod]
    public void Number_ANumberWrittenForReading_KeepsTheWayItWasWritten()
    {
        // The grouping and the sign are part of how the number is spelled, not of the markup.
        Assert.AreEqual("-1,234.5", MathML.AsNumber(MathML.Number("-1,234.5")));
    }

    [TestMethod]
    public void AsNumber_TheMarkupOfANumber_IsThatNumber()
    {
        Assert.AreEqual("357", AsNumber("<mn>357</mn>"));
    }

    [TestMethod]
    public void AsNumber_MarkupTheServiceSent_IsReadDespiteWhatItCarries()
    {
        // What comes back from Wolfram Alpha is laid out over several lines and carries attributes
        // of its own. Neither is part of the expression.
        Assert.AreEqual(
            "357",
            MathML.AsNumber(XDocument.Parse(
                "<math xmlns='http://www.w3.org/1998/Math/MathML'\n" +
                "    mathematica:form='StandardForm'\n" +
                "    xmlns:mathematica='http://www.wolfram.com/XML/'>\n <mn>357</mn>\n</math>")));
    }

    [TestMethod]
    public void AsNumber_MarkupThatIsMoreThanANumber_IsNotANumber()
    {
        Assert.IsNull(AsNumber("<mfrac><mn>1</mn><mn>3</mn></mfrac>"));
    }

    [TestMethod]
    public void AsNumber_MarkupHoldingMoreThanOneThing_IsNotANumber()
    {
        Assert.IsNull(AsNumber("<mi>x</mi><mo>=</mo><mn>3</mn>"));
    }

    [TestMethod]
    public void AsNumber_MarkupHoldingNothing_IsNotANumber()
    {
        Assert.IsNull(AsNumber(""));
    }

    private static string? AsNumber(string content) => MathML.AsNumber(
        XDocument.Parse($"<math xmlns=\"http://www.w3.org/1998/Math/MathML\">{content}</math>"));
}
