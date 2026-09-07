using System.ComponentModel.DataAnnotations;

namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// What is needed to ask Wolfram Alpha anything, and how long to wait for it.
/// </summary>
public sealed class WolframAlphaOptions
{
    /// <summary>
    /// Where these settings are read from.
    /// </summary>
    public const string SectionName = "WolframAlpha";

    /// <summary>
    /// The identifier the service issues to whoever is asking. Every request carries one, and a
    /// request without one is refused.
    /// </summary>
    /// <remarks>
    /// This is a secret, so it belongs somewhere that is not committed: an environment variable
    /// (<c>WolframAlpha__AppId</c>) or a local settings file that git ignores.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Where the service lives.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("https://api.wolframalpha.com/v2/");

    /// <summary>
    /// How long one attempt at a query may take.
    /// </summary>
    /// <remarks>
    /// Longer than an HTTP call would ordinarily be given. Wolfram Alpha does not look an answer
    /// up, it works it out, and an expression that takes it real thought takes real seconds — so
    /// the wait is set by what the service needs rather than by what a web request usually gets.
    /// </remarks>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a query may take in total, retries and all.
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(100);
}
