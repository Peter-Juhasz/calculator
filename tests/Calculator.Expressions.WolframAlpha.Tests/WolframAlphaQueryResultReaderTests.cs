namespace Calculator.Expressions.WolframAlpha.Tests;

/// <summary>
/// Reading the XML a query comes back as, and finding the one line in it that is the answer.
/// </summary>
[TestClass]
public sealed class WolframAlphaQueryResultReaderTests
{
    [TestMethod]
    public void Read_AnAnswer_TakesTheTextUnderThePrimaryPod()
    {
        var result = WolframAlphaQueryResultReader.Read(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.Error);
        Assert.AreEqual("357", result.PrimaryPlainText);
    }

    [TestMethod]
    public void Read_AnAnswer_KeepsEveryPodInTheOrderTheyCame()
    {
        var result = WolframAlphaQueryResultReader.Read(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour);

        Assert.AreSequenceEqual(
            ["Input", "Result", "NumberName", "NumberLine"],
            result.Pods.Select(pod => pod.Id));
    }

    [TestMethod]
    public void Read_APodThatIsOnlyAPicture_HasNothingWrittenOut()
    {
        var result = WolframAlphaQueryResultReader.Read(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour);

        var numberLine = result.Pods.Single(pod => pod.Id == "NumberLine");

        Assert.IsNull(numberLine.Subpods.Single().PlainText);
    }

    [TestMethod]
    public void Read_TheInputPod_IsNotMistakenForTheAnswer()
    {
        // It comes first, is written out, and says something that looks like an expression. Only
        // the primary flag tells it apart from the pod that answers the query.
        var result = WolframAlphaQueryResultReader.Read(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour);

        Assert.IsFalse(result.Pods.Single(pod => pod.Id == "Input").IsPrimary);
        Assert.AreNotEqual("123 + 234", result.PrimaryPlainText);
    }

    [TestMethod]
    public void Read_AnAnswerWithNoPrimaryPod_FallsBackToTheResultPod()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            <queryresult success="true" error="false">
              <pod title="Input" id="Input" position="1">
                <subpod title=""><plaintext>2^10</plaintext></subpod>
              </pod>
              <pod title="Result" id="Result" position="11">
                <subpod title=""><plaintext>1024</plaintext></subpod>
              </pod>
            </queryresult>
            """);

        Assert.AreEqual("1024", result.PrimaryPlainText);
    }

    [TestMethod]
    public void Read_APrimaryPodWhoseFirstSubpodIsAPicture_TakesTheFirstOneWithText()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            <queryresult success="true" error="false">
              <pod title="Plot" id="Plot" primary="true">
                <subpod title=""><plaintext/></subpod>
                <subpod title=""><plaintext>x = 3</plaintext></subpod>
              </pod>
            </queryresult>
            """);

        Assert.AreEqual("x = 3", result.PrimaryPlainText);
    }

    [TestMethod]
    public void Read_AnAnswerWithNothingWrittenOut_HasNoResult()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            <queryresult success="true" error="false">
              <pod title="Plot" id="Plot" primary="true">
                <subpod title=""><plaintext/></subpod>
              </pod>
            </queryresult>
            """);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.PrimaryPlainText);
    }

    [TestMethod]
    public void Read_AnInputThatWasNotUnderstood_IsNotSuccessAndIsNotAnError()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            <queryresult success="false" error="false" numpods="0">
              <tips count="1"><tip text="Check your spelling, and use English"/></tips>
            </queryresult>
            """);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Error);
        Assert.IsEmpty(result.Pods);
    }

    [TestMethod]
    public void Read_ARefusedAppId_SaysWhatWentWrong()
    {
        var result = WolframAlphaQueryResultReader.Read(
            """
            <queryresult success="false" error="true">
              <error><code>1</code><msg>Invalid appid</msg></error>
            </queryresult>
            """);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("1", result.Error?.Code);
        Assert.AreEqual("Invalid appid", result.Error?.Message);
    }

    [TestMethod]
    public void Read_AnAnswerWhosePodsFailed_DoesNotReadTheirFlagAsAnError()
    {
        // Every pod carries an error flag of its own, and the answer as a whole carries one too.
        // Neither is the element that says what an error was.
        var result = WolframAlphaQueryResultReader.Read(
            """
            <queryresult success="true" error="false">
              <pod title="Result" id="Result" error="false" primary="true">
                <subpod title=""><plaintext>7</plaintext></subpod>
              </pod>
            </queryresult>
            """);

        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public void Read_SomethingThatIsNotXml_IsRefused()
    {
        Assert.ThrowsExactly<WolframAlphaException>(
            () => WolframAlphaQueryResultReader.Read("<queryresult success=\"true\""));
    }

    [TestMethod]
    public void Read_XmlThatIsNotAnAnswer_IsRefused()
    {
        Assert.ThrowsExactly<WolframAlphaException>(
            () => WolframAlphaQueryResultReader.Read("<html><body>Gateway timeout</body></html>"));
    }

    [TestMethod]
    public async Task ReadAsync_AnAnswer_ReadsTheSameAsFromText()
    {
        using var stream = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour));

        var result = await WolframAlphaQueryResultReader.ReadAsync(stream, TestContext.CancellationToken);

        Assert.AreEqual("357", result.PrimaryPlainText);
    }

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// What the service sends back for <c>123+234</c>, as it sends it.
    /// </summary>
    private const string SumOfOneHundredTwentyThreeAndTwoHundredThirtyFour =
        """
        <queryresult success="true" error="false" numpods="4" datatypes="Math" parsetiming="0.056" parsetimedout="false" id="V6xvpKNlXcI=" kernelId="337" processId="929880" version="2.6" inputstring="123+234" sbsallowed="false" timing="0.621" timedout="" timedoutpods="">
        <pod title="Input" numsubpods="1" error="false" scanner="Identity" id="Input" position="1">
        <subpod title="">
        <img src="https://public5c.wolframalpha.com/files/GIF_j5u8d362zg.gif" alt="123 + 234" title="123 + 234" width="68" height="22" type="Default" themes="1,2,3" colorinvertable="true" contenttype="image/gif"/>
        <plaintext>123 + 234</plaintext>
        </subpod>
        <expressiontypes count="1">
        <expressiontype name="Default"/>
        </expressiontypes>
        </pod>
        <pod title="Result" numsubpods="1" error="false" scanner="Simplification" id="Result" position="11" primary="true">
        <subpod title="">
        <img src="https://public5c.wolframalpha.com/files/GIF_j5ufzp0k2z.gif" alt="357" title="357" width="27" height="22" type="Default" themes="1,2,3" colorinvertable="true" contenttype="image/gif"/>
        <plaintext>357</plaintext>
        </subpod>
        <expressiontypes count="1">
        <expressiontype name="Default"/>
        </expressiontypes>
        </pod>
        <pod title="Number name" numsubpods="1" error="false" scanner="Integer" id="NumberName" position="910">
        <subpod title="">
        <img src="https://public5c.wolframalpha.com/files/GIF_j5udoozrs7.gif" alt="three hundred fifty-seven" title="three hundred fifty-seven" width="168" height="22" type="Default" themes="1,2,3" colorinvertable="true" contenttype="image/gif"/>
        <plaintext>three hundred fifty-seven</plaintext>
        </subpod>
        <expressiontypes count="1">
        <expressiontype name="Default"/>
        </expressiontypes>
        </pod>
        <pod title="Number line" numsubpods="1" error="false" scanner="NumberLine" id="NumberLine" position="912">
        <subpod title="">
        <img src="https://public5c.wolframalpha.com/files/GIF_j5ugzmn711.gif" alt="Number line" title="" width="302" height="49" type="2DMathPlot_2" themes="1,2,3" colorinvertable="true" contenttype="image/gif"/>
        <plaintext/>
        </subpod>
        <expressiontypes count="1">
        <expressiontype name="Default"/>
        </expressiontypes>
        </pod>
        </queryresult>
        """;
}
