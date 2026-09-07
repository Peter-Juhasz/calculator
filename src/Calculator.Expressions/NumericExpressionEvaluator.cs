using System.Text;
using System.Xml.Linq;

namespace Calculator.Expressions;

/// <summary>
/// What the evaluators that work an expression out here, each in a number type of its own, have
/// in common: the notation they can read.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExpressionEvaluator"/> understands numbers, the four operations, powers, factorials
/// and parentheses, and nothing else — there are no functions and no names to look up. That holds
/// whichever number type does the arithmetic, so reading the markup is settled here, once, rather
/// than in each evaluator or in whoever is calling one.
/// </para>
/// <para>
/// Two things happen in that reading. Built-up notation is flattened, so a fraction stacked on the
/// screen becomes a division of two parenthesised halves. And the many characters that all mean
/// the same operation — the several multiplication dots, the minus sign that is not a hyphen, the
/// spaces that only group digits — are each reduced to the single spelling the grammar knows.
/// What is left over, a root or a name, is turned away rather than guessed at.
/// </para>
/// <para>
/// What a subclass adds is the number type the arithmetic is done in, and the spelling of the
/// result that follows from it. The answer is a number and nothing else, whichever type worked it
/// out, so it goes back as the markup of one: a <c>mn</c> with the math element around it.
/// </para>
/// </remarks>
public abstract class NumericExpressionEvaluator : IExpressionEvaluator
{
    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract ValueTask<XDocument> EvaluateAsync(string expression, CancellationToken cancellationToken);

    /// <inheritdoc />
    public ValueTask<XDocument> EvaluateAsync(XDocument mathML, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mathML);

        return EvaluateAsync(Linearize(mathML), cancellationToken);
    }

    /// <summary>
    /// Reads <paramref name="mathML"/> and returns the one line of text the expression comes to,
    /// or an empty string if it holds nothing.
    /// </summary>
    /// <exception cref="ExpressionFormatException">
    /// The markup uses notation that is not supported.
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

            case "mn":
            case "mo":
            case "mtext":
            case "ms":
                builder.Append(Normalize(element.Value));
                break;

            case "mspace":
                break;

            case "mfrac":
                WritePair(element, builder, "(", ")/(", ")");
                break;

            case "msup":
                WritePair(element, builder, "(", ")^(", ")");
                break;

            case "mfenced":
                builder.Append('(');
                WriteChildren(element, builder);
                builder.Append(')');
                break;

            // A letter is either a name, which there is nothing yet to look up, or the empty box
            // standing in for a part not filled in, which normalising away leaves nothing at all.
            case "mi":
                var identifier = Normalize(element.Value);
                if (identifier.Length > 0)
                {
                    throw new ExpressionFormatException("Names and functions are not supported yet.");
                }

                break;

            default:
                throw new ExpressionFormatException(
                    "Only the four operations, powers, factorials and parentheses are supported so far.");
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
    /// base and its exponent — onto one line.
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
    /// Reduces the several spellings of each operation to the one the evaluator reads, and drops
    /// the characters that are there only for typesetting.
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

                case '[':
                case '{':
                    builder.Append('(');
                    break;

                case ']':
                case '}':
                    builder.Append(')');
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
