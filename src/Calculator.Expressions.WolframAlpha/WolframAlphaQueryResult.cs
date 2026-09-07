using System.Text.Json.Serialization;

namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// What came back from one query: whether it was understood at all, and if it was, the pods the
/// answer was broken into.
/// </summary>
/// <remarks>
/// This and what hangs off it are the answer as it arrives and nothing more. Only the few things
/// worth having are named — whether it worked, what went wrong if it did not, and the markup of
/// the answer — and everything else the service sends is left unread: the images of each pod, how
/// wide they are, which kernel worked the query out, how long it took to parse. The markup itself
/// arrives as a string and stays one here; reading it as an expression is a step of its own.
/// </remarks>
/// <param name="Success">Whether Wolfram Alpha made sense of the input.</param>
/// <param name="Error">
/// What went wrong on the service's side — a refused app id, say — or <see langword="null"/> when
/// nothing did. An input the service simply could not read is not an error: that is
/// <paramref name="Success"/> being <see langword="false"/> with nothing here.
/// </param>
/// <param name="Pods">
/// The sections of the answer, in the order they were given, or <see langword="null"/> where there
/// are none — which is how an answer with nothing in it arrives. Only the section holding the
/// result is asked for, so ordinarily there is only the one.
/// </param>
public sealed record WolframAlphaQueryResult(
    bool Success,
    [property: JsonConverter(typeof(WolframAlphaErrorConverter))] WolframAlphaError? Error,
    IReadOnlyList<WolframAlphaPod>? Pods)
{
    /// <summary>
    /// The markup of the expression that answers the query, as it arrived, or
    /// <see langword="null"/> if the answer holds none.
    /// </summary>
    /// <remarks>
    /// Exactly one pod is marked as the one that was asked for, and its first written-out subpod
    /// is the answer. Not every query has such a pod — some have only a plot — and where one is
    /// missing the pod the service calls "Result" is taken instead, which is the same thing under
    /// a name rather than a flag.
    /// </remarks>
    [JsonIgnore]
    public string? PrimaryMathML
    {
        get
        {
            var pods = Pods ?? [];

            return pods
                .Where(pod => pod.IsPrimary)
                .Concat(pods.Where(pod => pod.Id == ResultPodId))
                .SelectMany(pod => pod.Subpods ?? [])
                .Select(subpod => subpod.MathML)
                .FirstOrDefault(markup => !string.IsNullOrWhiteSpace(markup));
        }
    }

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
public sealed record WolframAlphaError(
    string? Code,
    [property: JsonPropertyName("msg")] string? Message);

/// <summary>
/// One section of an answer, and what belongs under it.
/// </summary>
/// <param name="Id">
/// A stable name for the section that does not change with wording or language, such as
/// <c>Input</c> or <c>Result</c>.
/// </param>
/// <param name="IsPrimary">Whether this is the section that answers the query.</param>
/// <param name="Subpods">What the section holds, in order.</param>
public sealed record WolframAlphaPod(
    string? Id,
    [property: JsonPropertyName("primary")] bool IsPrimary,
    IReadOnlyList<WolframAlphaSubpod>? Subpods);

/// <summary>
/// One part of a section, and the writing-out of it.
/// </summary>
/// <param name="MathML">
/// The part written out as the markup of one expression. Absent where there is nothing to write
/// out, as for a plot, whose whole content is the picture.
/// </param>
public sealed record WolframAlphaSubpod(string? MathML);
