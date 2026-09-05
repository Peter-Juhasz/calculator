using System.Globalization;

namespace Calculator.Expressions;

/// <summary>
/// Works out what an expression comes to. It understands numbers, addition, subtraction,
/// multiplication, division and parentheses, and nothing else: there are no functions and no
/// names to look up.
/// </summary>
/// <remarks>
/// Arithmetic is done in <see cref="decimal"/> rather than binary floating point, so the numbers
/// people write are held as they wrote them: a tenth is a tenth, and 0.1 + 0.2 is exactly 0.3.
/// The cost is a narrower range, which a decimal announces by overflowing rather than quietly
/// drifting to infinity.
/// </remarks>
public static class ExpressionEvaluator
{
    /// <summary>
    /// Reads <paramref name="expression"/> and returns its value.
    /// </summary>
    /// <exception cref="ExpressionFormatException">The expression could not be read.</exception>
    /// <exception cref="DivideByZeroException">The expression divides by zero.</exception>
    /// <exception cref="OverflowException">The value is larger than a decimal can hold.</exception>
    public static decimal Evaluate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var reader = new Reader(expression);
        var value = reader.ReadExpression();
        reader.SkipWhitespace();

        if (!reader.IsAtEnd)
        {
            throw NotHere(reader.Current);
        }

        return value;
    }

    private static ExpressionFormatException NotHere(char character) =>
        new("'" + character + "' does not belong here.");

    private static ExpressionFormatException Unfinished() =>
        new("The expression is not finished.", isIncomplete: true);

    /// <summary>
    /// Reads the expression left to right, one grammar rule per method: sums are made of
    /// products, products of signed values, and signed values of numbers and parenthesised sums.
    /// Nesting the methods that way is what gives multiplication its precedence over addition.
    /// </summary>
    private ref struct Reader(ReadOnlySpan<char> text)
    {
        private readonly ReadOnlySpan<char> _text = text;
        private int _position;

        /// <summary>
        /// Whether the value just read was a parenthesised group, which is the one case where
        /// two values written next to each other are read as a product.
        /// </summary>
        private bool _lastWasGroup;

        public readonly bool IsAtEnd => _position >= _text.Length;

        public readonly char Current => _text[_position];

        public void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
            {
                _position++;
            }
        }

        public decimal ReadExpression()
        {
            var value = ReadTerm();

            while (true)
            {
                SkipWhitespace();

                if (IsAtEnd || (Current != '+' && Current != '-'))
                {
                    return value;
                }

                var op = Current;
                _position++;

                var right = ReadTerm();
                value = op == '+' ? value + right : value - right;
            }
        }

        private decimal ReadTerm()
        {
            var value = ReadSigned();

            while (true)
            {
                SkipWhitespace();

                if (IsAtEnd)
                {
                    return value;
                }

                var op = Current;

                if (op is '*' or '/')
                {
                    _position++;
                    var right = ReadSigned();
                    value = op == '*' ? value * right : Divide(value, right);
                    continue;
                }

                // Notation writes multiplication by putting things next to each other: 2(3+4),
                // (1+2)(3+4), (1+2)3. Two bare numbers side by side is a typing slip rather than
                // a product, so that one is left to fail.
                if (op == '(' || (_lastWasGroup && (char.IsAsciiDigit(op) || IsDecimalSeparator(op))))
                {
                    value *= ReadSigned();
                    continue;
                }

                return value;
            }
        }

        private decimal ReadSigned()
        {
            SkipWhitespace();

            if (IsAtEnd)
            {
                throw Unfinished();
            }

            if (Current is '+' or '-')
            {
                var sign = Current;
                _position++;

                var operand = ReadSigned();
                return sign == '-' ? -operand : operand;
            }

            return ReadValue();
        }

        private decimal ReadValue()
        {
            SkipWhitespace();

            if (IsAtEnd)
            {
                throw Unfinished();
            }

            if (Current == '(')
            {
                _position++;
                SkipWhitespace();

                // An empty pair of parentheses is what a fraction looks like while its two halves
                // are still being filled in, so it counts as unfinished rather than wrong.
                if (IsAtEnd || Current == ')')
                {
                    throw Unfinished();
                }

                var value = ReadExpression();
                SkipWhitespace();

                if (IsAtEnd)
                {
                    throw new ExpressionFormatException("A parenthesis is still open.", isIncomplete: true);
                }

                if (Current != ')')
                {
                    throw NotHere(Current);
                }

                _position++;
                _lastWasGroup = true;
                return value;
            }

            if (char.IsAsciiDigit(Current) || IsDecimalSeparator(Current))
            {
                _lastWasGroup = false;
                return ReadNumber();
            }

            throw NotHere(Current);
        }

        private decimal ReadNumber()
        {
            var start = _position;
            var separators = 0;

            while (!IsAtEnd && (char.IsAsciiDigit(Current) || IsDecimalSeparator(Current)))
            {
                if (IsDecimalSeparator(Current) && ++separators > 1)
                {
                    throw new ExpressionFormatException("A number can hold only one decimal separator.");
                }

                _position++;
            }

            // Either separator is accepted, so that the box takes what the keyboard in front of
            // the reader gives them. Parsing then happens against one fixed spelling of a number.
            var literal = _text[start.._position].ToString().Replace(',', '.');

            if (!decimal.TryParse(literal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                // The scan above let through only digits and at most one separator, so anything
                // that fails here is either a separator standing on its own or a number with more
                // digits than a decimal can hold.
                if (literal.Length > 1)
                {
                    throw new OverflowException("That number is too large.");
                }

                throw new ExpressionFormatException("'" + literal + "' is not a number.");
            }

            return value;
        }

        private static decimal Divide(decimal left, decimal right) =>
            right == 0
                ? throw new DivideByZeroException("Dividing by zero has no answer.")
                : left / right;

        private static bool IsDecimalSeparator(char character) => character is '.' or ',';
    }
}
