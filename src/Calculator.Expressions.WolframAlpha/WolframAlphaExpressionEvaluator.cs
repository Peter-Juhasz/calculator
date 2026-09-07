using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Calculator.Expressions.WolframAlpha;

/// <summary>
/// Hands the expression to Wolfram Alpha and puts back what it says the answer is.
/// </summary>
/// <remarks>
/// <para>
/// The other evaluators work an expression out here, in a number type chosen in advance, and are
/// bounded by what that type can say. This one does no arithmetic at all: it sends the line off
/// and reads back the answer, so it can be asked things the grammar in this app does not have —
/// a square root, a logarithm, a constant by name — and can answer with something a number type
/// has no way of holding, a fraction left unresolved or an equation solved for x.
/// </para>
/// <para>
/// The answer is asked for and handed on as MathML, which is what makes that possible: it comes
/// back built up the way it would be written by hand rather than flattened onto a line.
/// </para>
/// <para>
/// Being able to ask for more is also why the markup is read differently here. Where the
/// evaluators that do their own arithmetic turn away everything their grammar has no place for,
/// this one writes roots, sums, integrals, limits, matrices, names and functions out in Wolfram
/// Alpha's own syntax and lets the service decide what to make of them.
/// </para>
/// <para>
/// The cost is that every reading is a request over the network, which takes as long as it takes
/// and can simply fail.
/// </para>
/// </remarks>
public sealed class WolframAlphaExpressionEvaluator : IExpressionEvaluator
{
    private readonly WolframAlphaFullResultsClient _client;

    public WolframAlphaExpressionEvaluator(WolframAlphaFullResultsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    public string DisplayName => "Wolfram Alpha";

    /// <exception cref="ExpressionFormatException">
    /// Wolfram Alpha could not make sense of the expression.
    /// </exception>
    /// <exception cref="WolframAlphaException">
    /// Wolfram Alpha could not be asked, or did not answer with a result.
    /// </exception>
    public async ValueTask<XDocument> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ExpressionFormatException("The expression is empty.", isIncomplete: true);
        }

        var result = await _client.QueryAsync(expression, cancellationToken);

        if (result.Error is { } error)
        {
            throw new WolframAlphaException(error.Message ?? "Wolfram Alpha reported an error.");
        }

        if (!result.Success)
        {
            // Half a typed expression looks exactly like a wrong one from here — the service says
            // only that it could not read it, not why. Said as a hint, because most of the time
            // it is the ordinary state of an expression that is still being typed.
            throw new ExpressionFormatException(
                "Wolfram Alpha could not make sense of the expression.",
                isIncomplete: true);
        }

        var markup = result.PrimaryMathML
            ?? throw new WolframAlphaException("Wolfram Alpha answered without a result to show.");

        return ParseMathML(markup);
    }

    /// <summary>
    /// Reads the markup of an answer as the expression it describes.
    /// </summary>
    /// <remarks>
    /// The markup travels as a string inside the answer, so it arrives already read once, as JSON,
    /// and is still only a run of characters at that point. This is the second reading, and the one
    /// that makes an expression of it.
    /// </remarks>
    /// <exception cref="WolframAlphaException">The markup is malformed.</exception>
    public static XDocument ParseMathML(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);

        try
        {
            return XDocument.Parse(markup);
        }
        catch (XmlException exception)
        {
            throw new WolframAlphaException("Wolfram Alpha sent back something unreadable.", exception);
        }
    }

    /// <exception cref="ExpressionFormatException">
    /// The markup uses notation that cannot be written out for Wolfram Alpha.
    /// </exception>
    /// <exception cref="WolframAlphaException">
    /// Wolfram Alpha could not be asked, or did not answer with a result.
    /// </exception>
    public ValueTask<XDocument> EvaluateAsync(XDocument mathML, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mathML);

        return EvaluateAsync(Linearize(mathML), cancellationToken);
    }

    /// <summary>
    /// Reads <paramref name="mathML"/> and returns the query it comes to, written the way it would
    /// be typed into the site, or an empty string if it holds nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What is built up on the screen is written out in the linear syntax Wolfram Alpha reads:
    /// a bar becomes a division, a raised position a caret, a radical sign <c>sqrt</c>, and what
    /// is written under and over a sign — the bounds of a sum, the point a limit approaches —
    /// becomes the <c>_(…)^(…)</c> that syntax spells them with.
    /// </para>
    /// <para>
    /// Names are left alone rather than turned away. There is nothing here that could look one up,
    /// and nothing that needs to: <c>x</c>, <c>sin</c> and <c>log</c> mean something at the other
    /// end, and the few signs with a spelling of their own — π, ∞, the summation sign — are given
    /// the word Wolfram Alpha knows them by.
    /// </para>
    /// </remarks>
    /// <exception cref="ExpressionFormatException">
    /// The markup uses notation that cannot be written out for Wolfram Alpha.
    /// </exception>
    public static string Linearize(XDocument mathML)
    {
        ArgumentNullException.ThrowIfNull(mathML);

        if (mathML.Root is not { } root)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        Write(root, builder);
        return builder.ToString();
    }

    private static void Write(XElement element, StringBuilder builder)
    {
        switch (element.Name.LocalName)
        {
            case "math":
            case "mrow":
            case "mstyle":
            case "mpadded":
                WriteChildren(element, builder);
                break;

            // Presentation markup first, then an annotation saying the same thing another way.
            // Only the first of the two is the expression.
            case "semantics":
                var presentation = element.Elements().FirstOrDefault();
                if (presentation is not null)
                {
                    Write(presentation, builder);
                }

                break;

            // A name is written out as it stands. Whether it is a variable, a function or a
            // constant is Wolfram Alpha's business rather than ours.
            case "mn":
            case "mo":
            case "mi":
            case "mtext":
            case "ms":
                builder.Append(Normalize(element.Value));
                break;

            // Space is only ever typesetting here, and what is hidden is not part of the query.
            case "mspace":
            case "mphantom":
                break;

            case "mfrac":
                WritePair(element, builder, "(", ")/(", ")");
                break;

            case "msup":
                WritePair(element, builder, "(", ")^(", ")");
                break;

            case "msqrt":
                builder.Append("sqrt(");
                WriteChildren(element, builder);
                builder.Append(')');
                break;

            // A root of any degree is the same thing as a fractional power, which is written on
            // one line without needing a name for the root itself.
            case "mroot":
                WritePair(element, builder, "(", ")^(1/(", "))");
                break;

            case "mfenced":
                builder.Append('(');
                WriteChildren(element, builder);
                builder.Append(')');
                break;

            case "msub":
            case "munder":
                WriteScripts(element, builder, below: true, above: false);
                break;

            case "mover":
                WriteScripts(element, builder, below: false, above: true);
                break;

            case "msubsup":
            case "munderover":
                WriteScripts(element, builder, below: true, above: true);
                break;

            case "mtable":
                WriteTable(element, builder);
                break;

            default:
                throw new ExpressionFormatException(
                    "That notation cannot be written out for Wolfram Alpha.");
        }
    }

    private static void WriteChildren(XElement element, StringBuilder builder)
    {
        foreach (var child in element.Elements())
        {
            Write(child, builder);
        }
    }

    /// <summary>
    /// Writes the two parts of something built up on the screen — the halves of a fraction, a
    /// base and its exponent, a radicand and its degree — onto one line.
    /// </summary>
    /// <remarks>
    /// Both parts are parenthesised on the way down. What is stacked needs no brackets to say
    /// where it begins and ends, so a fraction bar groups everything above and below it and a
    /// raised position groups the whole exponent; written flat, only parentheses can say that.
    /// </remarks>
    private static void WritePair(XElement element, StringBuilder builder, string open, string between, string close)
    {
        var parts = element.Elements().ToArray();

        if (parts.Length != 2)
        {
            throw new ExpressionFormatException("That could not be read.");
        }

        builder.Append(open);
        Write(parts[0], builder);
        builder.Append(between);
        Write(parts[1], builder);
        builder.Append(close);
    }

    /// <summary>
    /// Writes what stands under or over a sign — the bounds of a sum, the point a limit
    /// approaches, an index — after the sign it belongs to.
    /// </summary>
    /// <remarks>
    /// The sign itself is written bare rather than parenthesised: <c>sum_(i=1)^(10)</c> and
    /// <c>log_(2)</c> are read as one thing by Wolfram Alpha, where <c>(sum)_(i=1)</c> and
    /// <c>(log)_(2)</c> are read as a name in brackets and lose the sense of it.
    /// </remarks>
    private static void WriteScripts(XElement element, StringBuilder builder, bool below, bool above)
    {
        var parts = element.Elements().ToArray();
        var expected = 1 + (below ? 1 : 0) + (above ? 1 : 0);

        if (parts.Length != expected)
        {
            throw new ExpressionFormatException("That could not be read.");
        }

        Write(parts[0], builder);

        var next = 1;

        if (below)
        {
            builder.Append("_(");
            Write(parts[next++], builder);
            builder.Append(')');
        }

        if (above)
        {
            builder.Append("^(");
            Write(parts[next], builder);
            builder.Append(')');
        }
    }

    /// <summary>
    /// Writes a table out as the list of rows Wolfram Alpha reads a matrix as.
    /// </summary>
    private static void WriteTable(XElement element, StringBuilder builder)
    {
        builder.Append('{');

        var firstRow = true;

        foreach (var row in element.Elements())
        {
            if (row.Name.LocalName is not ("mtr" or "mlabeledtr"))
            {
                throw new ExpressionFormatException("That table could not be read.");
            }

            if (!firstRow)
            {
                builder.Append(", ");
            }

            firstRow = false;
            builder.Append('{');

            var firstCell = true;

            foreach (var cell in row.Elements())
            {
                if (cell.Name.LocalName != "mtd")
                {
                    throw new ExpressionFormatException("That table could not be read.");
                }

                if (!firstCell)
                {
                    builder.Append(", ");
                }

                firstCell = false;
                WriteChildren(cell, builder);
            }

            builder.Append('}');
        }

        builder.Append('}');
    }

    /// <summary>
    /// Reduces the several spellings of each operation to the one Wolfram Alpha reads, gives the
    /// signs that have a word of their own that word, and drops the characters that are there
    /// only for typesetting.
    /// </summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            switch (character)
            {
                case '\u2212': // minus sign
                case '\u2013': // en dash
                    builder.Append('-');
                    break;

                case '\u00D7': // multiplication sign
                case '\u22C5': // dot operator
                case '\u2217': // asterisk operator
                case '\u00B7': // middle dot
                case '\u2062': // invisible times
                    builder.Append('*');
                    break;

                case '\u00F7': // division sign
                case '\u2215': // division slash
                case '\u2044': // fraction slash
                case '\u2236': // ratio
                    builder.Append('/');
                    break;

                // Invisible plus is what joins the two halves of a mixed number such as 1 1/2.
                case '\u2064':
                    builder.Append('+');
                    break;

                case '\u00B1': // plus-minus sign
                    builder.Append("+-");
                    break;

                case '[':
                case '{':
                    builder.Append('(');
                    break;

                case ']':
                case '}':
                    builder.Append(')');
                    break;

                // The signs that are a word rather than an operator at the other end.
                case '\u03C0': // greek small letter pi
                    builder.Append("pi");
                    break;

                case '\u221E': // infinity
                    builder.Append("infinity");
                    break;

                case '\u212F': // script small e
                case '\u2147': // double-struck italic small e
                    builder.Append('e');
                    break;

                case '\u2148': // double-struck italic small i
                    builder.Append('i');
                    break;

                case '\u2211': // n-ary summation
                    builder.Append("sum");
                    break;

                case '\u220F': // n-ary product
                    builder.Append("product");
                    break;

                case '\u222B': // integral
                    builder.Append("integral");
                    break;

                case '\u2264': // less-than or equal to
                    builder.Append("<=");
                    break;

                case '\u2265': // greater-than or equal to
                    builder.Append(">=");
                    break;

                case '\u2260': // not equal to
                    builder.Append("!=");
                    break;

                case '\u2192': // rightwards arrow, which is how a limit says what approaches what
                    builder.Append("->");
                    break;

                case '\u2061': // function application
                case '\u2063': // invisible separator
                case '\u200B': // zero width space
                case '\uFEFF': // zero width no-break space
                case '\u00A0': // no-break space, which groups digits rather than separating them
                case '\u2007': // figure space, likewise
                case '\u2009': // thin space, likewise
                case '\u202F': // narrow no-break space, likewise
                case '\u2B1A': // dotted square, the box shown for a part not filled in yet
                case '\u25A1': // white square, likewise
                    break;

                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
}
