namespace Calculator.Expressions.WolframAlpha.Tests;

/// <summary>
/// Reading the JSON a query comes back as, and finding the markup in it that is the answer.
/// </summary>
[TestClass]
public sealed class WolframAlphaQueryResultReaderTests
{
    [TestMethod]
    public void Read_AnAnswer_TakesTheMarkupUnderThePrimaryPod()
    {
        var result = WolframAlphaQueryResultReader.Read(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.Error);
        Assert.IsTrue(result.PrimaryMathML!.Contains("<mn>357</mn>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Read_TheMarkupOfAnAnswer_IsTakenAsItStands()
    {
        // It travels as a string inside the JSON and is left one here. What it describes is read
        // afterwards, and elsewhere.
        var result = WolframAlphaQueryResultReader.Read(Answer("<math><mn>357</mn></math>"));

        Assert.AreEqual("<math><mn>357</mn></math>", result.PrimaryMathML);
    }

    [TestMethod]
    public void Read_MarkupTheServiceEscaped_IsUnescapedByTheReadingOfTheJson()
    {
        var result = WolframAlphaQueryResultReader.Read(Answer(
            "\\u003Cmath\\u003E\\u003Cmn\\u003E357\\u003C/mn\\u003E\\u003C/math\\u003E"));

        Assert.AreEqual("<math><mn>357</mn></math>", result.PrimaryMathML);
    }

    [TestMethod]
    public void Read_AnAnswer_KeepsEveryPodInTheOrderTheyCame()
    {
        // Only the result pod is asked for, but nothing here depends on the service sending that
        // and only that.
        var result = WolframAlphaQueryResultReader.Read(
            """
            {"queryresult": {"success": true, "error": false, "pods": [
              {"title": "Input", "id": "Input", "subpods": [{"title": ""}]},
              {"title": "Result", "id": "Result", "primary": true, "subpods": [{"title": ""}]}
            ]}}
            """);

        Assert.AreSequenceEqual(["Input", "Result"], result.Pods!.Select(pod => pod.Id));
    }

    [TestMethod]
    public void Read_APodThatIsOnlyAPicture_HasNothingWrittenOut()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            {"queryresult": {"success": true, "error": false, "pods": [
              {"title": "Number line", "id": "NumberLine", "subpods": [{"title": ""}]}
            ]}}
            """);

        Assert.IsNull(result.Pods!.Single().Subpods!.Single().MathML);
    }

    [TestMethod]
    public void Read_AnAnswerWithNoPrimaryPod_FallsBackToTheResultPod()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            {"queryresult": {"success": true, "error": false, "pods": [
              {"title": "Result", "id": "Result", "subpods": [
                {"title": "", "mathml": "<math><mn>1024</mn></math>"}
              ]}
            ]}}
            """);

        Assert.AreEqual("<math><mn>1024</mn></math>", result.PrimaryMathML);
    }

    [TestMethod]
    public void Read_APrimaryPodWhoseFirstSubpodIsAPicture_TakesTheFirstOneWithMarkup()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            {"queryresult": {"success": true, "error": false, "pods": [
              {"title": "Plot", "id": "Plot", "primary": true, "subpods": [
                {"title": ""},
                {"title": "", "mathml": "<math><mn>3</mn></math>"}
              ]}
            ]}}
            """);

        Assert.AreEqual("<math><mn>3</mn></math>", result.PrimaryMathML);
    }

    [TestMethod]
    public void Read_AnAnswerWithNothingWrittenOut_HasNoResult()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            {"queryresult": {"success": true, "error": false, "pods": [
              {"title": "Plot", "id": "Plot", "primary": true, "subpods": [{"title": ""}]}
            ]}}
            """);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.PrimaryMathML);
    }

    [TestMethod]
    public void Read_AnInputThatWasNotUnderstood_IsNotSuccessAndIsNotAnError()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            {"queryresult": {"success": false, "error": false, "numpods": 0,
              "tips": {"text": "Check your spelling, and use English"}}}
            """);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Error);
        Assert.IsNull(result.Pods);
        Assert.IsNull(result.PrimaryMathML);
    }

    [TestMethod]
    public void Read_ARefusedAppId_SaysWhatWentWrong()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            {"queryresult": {"success": false, "error": {"code": "1", "msg": "Invalid appid"}}}
            """);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("1", result.Error?.Code);
        Assert.AreEqual("Invalid appid", result.Error?.Message);
    }

    [TestMethod]
    public void Read_AnAnswerThatWentWell_DoesNotReadItsErrorFlagAsAnError()
    {
        // The same word stands for the flag saying nothing went wrong and for what says what did.
        // Only the second is an error, and the first is not something that could not be read.
        var result = WolframAlphaQueryResultReader.Read(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour);

        Assert.IsNull(result.Error);
        Assert.IsNotNull(result.PrimaryMathML);
    }

    [TestMethod]
    public void Read_SomethingThatIsNotJson_IsRefused()
    {
        Assert.ThrowsExactly<WolframAlphaException>(
            () => WolframAlphaQueryResultReader.Read("{\"queryresult\": "));
    }

    [TestMethod]
    public void Read_JsonThatIsNotAnAnswer_IsRefused()
    {
        Assert.ThrowsExactly<WolframAlphaException>(
            () => WolframAlphaQueryResultReader.Read("{\"error\": \"Gateway timeout\"}"));
    }

    [TestMethod]
    public async Task ReadAsync_AnAnswer_ReadsTheSameAsFromText()
    {
        using var stream = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour));

        var result = await WolframAlphaQueryResultReader.ReadAsync(stream, TestContext.CancellationToken);

        Assert.IsTrue(result.PrimaryMathML!.Contains("<mn>357</mn>", StringComparison.Ordinal));
    }

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// One answer holding <paramref name="mathML"/>, which travels as a string inside the JSON —
    /// so what is written here is the markup as JSON spells it rather than as XML does.
    /// </summary>
    private static string Answer(string mathML) =>
        $$$"""
        {"queryresult": {"success": true, "error": false, "pods": [
          {"title": "Result", "id": "Result", "primary": true, "subpods": [
            {"title": "", "mathml": "{{{mathML}}}"}
          ]}
        ]}}
        """;

    /// <summary>
    /// What the service sends back for <c>123+234</c>: the one pod that was asked for, and markup
    /// laid out over several lines and carrying attributes of its own.
    /// </summary>
    private const string SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour =
        """
        {
          "queryresult": {
            "success": true,
            "error": false,
            "numpods": 1,
            "datatypes": "Math",
            "parsetiming": 0.106,
            "parsetimedout": false,
            "id": "V6xvpKNlXcI=6",
            "kernelId": "774",
            "processId": 1872674,
            "version": "2.6",
            "inputstring": "123+234",
            "sbsallowed": false,
            "pods": [
              {
                "title": "Result",
                "subpods": [
                  {
                    "title": "",
                    "mathml": "<math xmlns='http://www.w3.org/1998/Math/MathML'\n    mathematica:form='StandardForm'\n    xmlns:mathematica='http://www.wolfram.com/XML/'>\n <mn>357</mn>\n</math>"
                  }
                ],
                "numsubpods": 1,
                "error": false,
                "scanner": "Simplification",
                "id": "Result",
                "position": 11,
                "primary": true,
                "expressiontypes": {
                  "name": "Default"
                }
              }
            ],
            "timing": 0.306,
            "timedout": "",
            "timedoutpods": ""
          }
        }
        """;
}
