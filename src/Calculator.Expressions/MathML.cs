using System.Xml.Linq;

namespace Calculator.Expressions;

/// <summary>
/// The markup an answer is written in, and the two things everything here asks of it: making one
/// out of a number, and reading a number back out of one.
/// </summary>
/// <remarks>
/// An answer leaves an evaluator as markup rather than as a line of text because not every answer
/// is a line: a service that does algebra can answer with a fraction, a root or an equation, and
/// what is stacked on the screen has no faithful spelling on one line. Arithmetic done here has
/// only ever the one shape of answer, and says so by wrapping its number in the same markup.
/// </remarks>
public static class MathML
{
    /// <summary>
    /// Where the elements of the markup belong. The box the expression is typed into and the
    /// service that answers it both write it out qualified, so what is built here is too.
    /// </summary>
    public static readonly XNamespace Namespace = "http://www.w3.org/1998/Math/MathML";

    /// <summary>
    /// Writes <paramref name="number"/> out as the whole of one expression: the number, and the
    /// math element around it.
    /// </summary>
    public static XDocument Number(string number)
    {
        ArgumentNullException.ThrowIfNull(number);

        return new XDocument(
            new XElement(
                Namespace + "math",
                new XElement(Namespace + "mn", number)));
    }

    /// <summary>
    /// The number <paramref name="mathML"/> is, or <see langword="null"/> where it is anything
    /// more than one.
    /// </summary>
    /// <remarks>
    /// An answer that is a bare number is worth having as that number rather than as the markup of
    /// one — it is what anyone would want to paste elsewhere. Anything built up, a fraction or an
    /// equation, has no such plain reading and is only itself.
    /// </remarks>
    public static string? AsNumber(XDocument mathML)
    {
        ArgumentNullException.ThrowIfNull(mathML);

        if (mathML.Root is not { } root || root.Name.LocalName != "math")
        {
            return null;
        }

        return root.Elements().ToArray() is [{ HasElements: false } only] &&
            only.Name.LocalName == "mn"
            ? only.Value
            : null;
    }
}
