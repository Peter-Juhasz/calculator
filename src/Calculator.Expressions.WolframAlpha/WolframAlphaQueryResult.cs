namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// What came back from one query: whether it was understood at all, and if it was, the pods the
/// answer was broken into.
/// </summary>
/// <param name="Success">Whether Wolfram Alpha made sense of the input.</param>
/// <param name="Error">
/// What went wrong on the service's side — a refused app id, say — or <see langword="null"/> when
/// nothing did. An input the service simply could not read is not an error: that is
/// <paramref name="Success"/> being <see langword="false"/> with nothing here.
/// </param>
/// <param name="Pods">
/// The sections of the answer, in the order they were given. A query for a sum has one for the
/// input as it was read back, one for the result, and usually several saying something further
/// about it.
/// </param>
public sealed record WolframAlphaQueryResult(
    bool Success,
    WolframAlphaError? Error,
    IReadOnlyList<WolframAlphaPod> Pods)
{
    /// <summary>
    /// The one line of text that answers the query, or <see langword="null"/> if the answer holds
    /// no such line.
    /// </summary>
    /// <remarks>
    /// An answer is many pods and each pod may hold several subpods, but exactly one pod is
    /// marked as the one that was asked for, and its first written-out subpod is the answer. Not
    /// every query has such a pod — some have only a plot, or a list of things none of which is
    /// the point — and where one is missing the pod the service calls "Result" is taken instead,
    /// which is the same thing under a name rather than a flag.
    /// </remarks>
    public string? PrimaryPlainText => Pods
        .Where(pod => pod.IsPrimary)
        .Concat(Pods.Where(pod => pod.Id == ResultPodId))
        .SelectMany(pod => pod.Subpods)
        .Select(subpod => subpod.PlainText)
        .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

    /// <summary>
    /// What the service calls the pod holding the answer, when it marks none of them as primary.
    /// </summary>
    private const string ResultPodId = "Result";
}

/// <summary>
/// Something that stopped the query being answered, as the service reported it.
/// </summary>
/// <param name="Code">The service's number for this kind of failure.</param>
/// <param name="Message">What it says went wrong.</param>
public sealed record WolframAlphaError(string? Code, string? Message);

/// <summary>
/// One section of an answer: a heading and what belongs under it.
/// </summary>
/// <param name="Title">What the section is called, as it would be shown.</param>
/// <param name="Id">
/// A stable name for the section that does not change with wording or language, such as
/// <c>Input</c> or <c>Result</c>.
/// </param>
/// <param name="IsPrimary">Whether this is the section that answers the query.</param>
/// <param name="Subpods">What the section holds, in order.</param>
public sealed record WolframAlphaPod(
    string? Title,
    string? Id,
    bool IsPrimary,
    IReadOnlyList<WolframAlphaSubpod> Subpods);

/// <summary>
/// One part of a section, and the writing-out of it.
/// </summary>
/// <param name="Title">What this part is called, which is usually nothing.</param>
/// <param name="PlainText">
/// The part written out as text. Empty where there is nothing to write out, as for a plot, whose
/// whole content is the picture.
/// </param>
public sealed record WolframAlphaSubpod(string? Title, string? PlainText);
