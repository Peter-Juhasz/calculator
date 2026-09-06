using System.Globalization;
using System.Numerics;

namespace Calculator.Expressions;

/// <summary>
/// Works out what an expression comes to. It understands numbers, addition, subtraction,
/// multiplication, division, powers, factorials and parentheses, and nothing else: there are no
/// functions and no names to look up.
/// </summary>
/// <remarks>
/// <para>
/// The arithmetic is done in whatever number type the caller names, so the same reading of an
/// expression can be worked out in <see cref="decimal"/>, in <see cref="double"/>, or in a whole
/// number type. What each type does with a result it cannot hold is the type's own business, and
/// shows through: the arithmetic here is checked, so a type that would otherwise wrap around says
/// so instead.
/// </para>
/// <para>
/// The overload that names no type works in <see cref="decimal"/>, which is what a calculator
/// wants: the numbers people write are held as they wrote them, a tenth is a tenth, and
/// 0.1 + 0.2 is exactly 0.3. The cost is a narrower range, which a decimal announces by
/// overflowing rather than quietly drifting to infinity. The single exception is a power that is
/// not whole, which the framework has no decimal arithmetic for and which is therefore
/// approximated.
/// </para>
/// </remarks>
public static class ExpressionEvaluator
{
    /// <summary>
    /// Reads <paramref name="expression"/> and returns its value, worked out in
    /// <see cref="decimal"/>.
    /// </summary>
    /// <exception cref="ExpressionFormatException">The expression could not be read.</exception>
    /// <exception cref="DivideByZeroException">The expression divides by zero.</exception>
    /// <exception cref="OverflowException">The value is larger than a decimal can hold.</exception>
    /// <exception cref="ArithmeticException">
    /// The expression asks for something with no answer, such as a factorial of a fraction.
    /// </exception>
    public static decimal Evaluate(string expression) => Evaluate<decimal>(expression);

    /// <summary>
    /// Reads <paramref name="expression"/> and returns its value, worked out in
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The number type the arithmetic is done in.</typeparam>
    /// <exception cref="ExpressionFormatException">The expression could not be read.</exception>
    /// <exception cref="DivideByZeroException">The expression divides by zero.</exception>
    /// <exception cref="OverflowException">
    /// The value is larger than <typeparamref name="T"/> can hold.
    /// </exception>
    /// <exception cref="ArithmeticException">
    /// The expression asks for something with no answer, such as a factorial of a fraction.
    /// </exception>
    public static T Evaluate<T>(string expression) where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(expression);

        var reader = new Reader<T>(expression);
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
    private ref struct Reader<T>(ReadOnlySpan<char> text) where T : INumber<T>
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

        public T ReadExpression()
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
                value = op == '+' ? checked(value + right) : checked(value - right);
            }
        }

        private T ReadTerm()
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
                    value = op == '*' ? checked(value * right) : Divide(value, right);
                    continue;
                }

                // Notation writes multiplication by putting things next to each other: 2(3+4),
                // (1+2)(3+4), (1+2)3. Two bare numbers side by side is a typing slip rather than
                // a product, so that one is left to fail.
                if (op == '(' || (_lastWasGroup && (char.IsAsciiDigit(op) || IsDecimalSeparator(op))))
                {
                    value = checked(value * ReadSigned());
                    continue;
                }

                return value;
            }
        }

        private T ReadSigned()
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
                return sign == '-' ? checked(-operand) : operand;
            }

            return ReadPower();
        }

        /// <summary>
        /// Reads a value and, if one follows, the power it is raised to. The exponent is read
        /// back through <see cref="ReadSigned"/>, which makes powers right-associative and lets
        /// the exponent carry a sign: 2^3^2 is 2^(3^2), and 2^-1 is a half.
        /// </summary>
        private T ReadPower()
        {
            var value = ReadFactorial();

            SkipWhitespace();

            if (IsAtEnd || Current != '^')
            {
                return value;
            }

            _position++;
            var raised = Power(value, ReadSigned());

            // Whatever the base was, what comes back is a number rather than a group, so nothing
            // written after it is a product with it.
            _lastWasGroup = false;
            return raised;
        }

        /// <summary>
        /// Reads a value and any exclamation marks after it. Being read below powers is what
        /// makes 3!^2 the square of six, and 2^3! two raised to six.
        /// </summary>
        private T ReadFactorial()
        {
            var value = ReadValue();

            while (true)
            {
                SkipWhitespace();

                if (IsAtEnd || Current != '!')
                {
                    return value;
                }

                _position++;
                value = Factorial(value);

                // What comes back is a number rather than a group, so nothing written after it
                // is a product with it.
                _lastWasGroup = false;
            }
        }

        private T ReadValue()
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

        private T ReadNumber()
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

            if (!T.TryParse(literal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                // The scan above let through only digits and at most one separator, so anything
                // that fails here is either a separator standing on its own, or a number the
                // chosen type has no room for: more digits than its range holds, or a fraction
                // where it counts in whole numbers.
                if (literal.Length > 1)
                {
                    throw new OverflowException("That number does not fit.");
                }

                throw new ExpressionFormatException("'" + literal + "' is not a number.");
            }

            return value;
        }

        private static T Divide(T left, T right) =>
            T.IsZero(right)
                ? throw new DivideByZeroException("Dividing by zero has no answer.")
                : checked(left / right);

        private static T Power(T value, T exponent)
        {
            // A whole power is repeated multiplication, and repeated multiplication is as exact as
            // the type it is done in. This is the case nearly every typed power falls into.
            if (T.IsInteger(exponent))
            {
                var size = T.Abs(exponent);

                if (size <= T.CreateSaturating(long.MaxValue))
                {
                    var magnitude = WholePower(value, long.CreateSaturating(size));

                    if (!T.IsNegative(exponent))
                    {
                        return magnitude;
                    }

                    return T.IsZero(magnitude)
                        ? throw new DivideByZeroException("Zero to a negative power has no answer.")
                        : checked(T.One / magnitude);
                }
            }

            // A power that is not whole has no arithmetic of its own behind it in the framework,
            // so it is worked out in binary floating point and brought back. This is the one place
            // in here where an answer may be an approximation rather than as exact as the type
            // allows.
            var approximation = System.Math.Pow(double.CreateSaturating(value), double.CreateSaturating(exponent));

            if (double.IsNaN(approximation))
            {
                throw new ArithmeticException("A negative number has no such power.");
            }

            // Converting either infinity to a type that cannot hold one reports itself as an
            // overflow anyway, but says so here rather than several frames away.
            if (double.IsInfinity(approximation))
            {
                throw new OverflowException("The result is too large.");
            }

            return T.CreateChecked(approximation);
        }

        /// <summary>
        /// Raises <paramref name="value"/> to a whole, non-negative power, squaring as it goes so
        /// that a large power costs a handful of multiplications rather than one per step.
        /// </summary>
        private static T WholePower(T value, long exponent)
        {
            var result = T.One;

            while (exponent > 0)
            {
                if ((exponent & 1) == 1)
                {
                    result = checked(result * value);
                }

                exponent >>= 1;

                if (exponent > 0)
                {
                    value = checked(value * value);
                }
            }

            return result;
        }

        private static T Factorial(T value)
        {
            if (!T.IsInteger(value) || T.IsNegative(value))
            {
                throw new ArithmeticException("Only a whole number that is not negative has a factorial.");
            }

            var result = T.One;

            // A decimal runs out at 28!, so the loop is short whatever it is handed: an oversized
            // count overflows long before it comes anywhere near the end. A type that answers an
            // oversized factorial with infinity instead of overflowing has no such stopping point,
            // so it is given one.
            for (var factor = T.One + T.One; factor <= value; factor++)
            {
                result = checked(result * factor);

                if (!T.IsFinite(result))
                {
                    throw new OverflowException("That factorial is too large.");
                }
            }

            return result;
        }

        private static bool IsDecimalSeparator(char character) => character is '.' or ',';
    }
}
