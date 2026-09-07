using System.Xml;
using System.Xml.Linq;

namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// Reads the XML a query comes back as into a <see cref="WolframAlphaQueryResult"/>.
/// </summary>
/// <remarks>
/// Only what an answer is made of is kept — the pods, and the text under them. The rest of what
/// the service sends is about drawing the answer on its own site: the images of each pod, how
/// wide they are, which colour themes they suit, how long the query took to parse.
/// </remarks>
public static class WolframAlphaQueryResultReader
{
    /// <summary>
    /// Reads <paramref name="xml"/> as the answer to one query.
    /// </summary>
    /// <exception cref="WolframAlphaException">
    /// The XML is malformed, or is not an answer to a query.
    /// </exception>
    public static WolframAlphaQueryResult Read(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        XDocument document;

        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha sent back something unreadable.", exception);
        }

        return Read(document);
    }

    /// <summary>
    /// Reads the answer to one query out of <paramref name="stream"/>.
    /// </summary>
    /// <exception cref="WolframAlphaException">
    /// The XML is malformed, or is not an answer to a query.
    /// </exception>
    public static async ValueTask<WolframAlphaQueryResult> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument document;

        try
        {
            document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        }
        catch (XmlException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha sent back something unreadable.", exception);
        }

        return Read(document);
    }

    private static WolframAlphaQueryResult Read(XDocument document)
    {
        var root = document.Root;

        if (root is null || root.Name.LocalName != "queryresult")
        {
            throw new WolframAlphaException("Wolfram Alpha sent back something other than an answer.");
        }

        return new WolframAlphaQueryResult(
            Success: ReadFlag(root, "success"),
            Error: ReadError(root),
            Pods: [.. root.Elements("pod").Select(ReadPod)]);
    }

    /// <summary>
    /// The failure the service is reporting, or <see langword="null"/> if it reports none.
    /// </summary>
    /// <remarks>
    /// The word <c>error</c> is used twice over: as a flag on the answer and on every pod, and as
    /// the element that says what the error was. Only the element is worth keeping, and it is
    /// there only when there is something to say.
    /// </remarks>
    private static WolframAlphaError? ReadError(XElement root)
    {
        var error = root.Element("error");

        if (error is null)
        {
            return null;
        }

        return new WolframAlphaError(
            Code: Text(error.Element("code")),
            Message: Text(error.Element("msg")));
    }

    private static WolframAlphaPod ReadPod(XElement pod) => new(
        Title: (string?)pod.Attribute("title"),
        Id: (string?)pod.Attribute("id"),
        IsPrimary: ReadFlag(pod, "primary"),
        Subpods: [.. pod.Elements("subpod").Select(ReadSubpod)]);

    private static WolframAlphaSubpod ReadSubpod(XElement subpod) => new(
        Title: (string?)subpod.Attribute("title"),
        PlainText: Text(subpod.Element("plaintext")));

    /// <summary>
    /// A flag the service writes as <c>true</c> or <c>false</c>, and leaves off altogether where
    /// it does not apply — as <c>primary</c> is left off every pod but the one.
    /// </summary>
    private static bool ReadFlag(XElement element, string name) =>
        string.Equals((string?)element.Attribute(name), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What an element says, or <see langword="null"/> where it says nothing. An empty
    /// <c>plaintext</c> is how a pod that is only a picture writes itself out, and that is worth
    /// telling apart from a pod that wrote nothing at all only by being absent.
    /// </summary>
    private static string? Text(XElement? element)
    {
        var text = element?.Value.Trim();

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
