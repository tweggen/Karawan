using System;
using System.Collections.Generic;

namespace builtin.tools.Lindenmayer.Expressions;

/// <summary>
/// Exception thrown when parsing fails.
/// </summary>
public class ParseException : Exception
{
    public int Position { get; }

    public ParseException(string message, int position) : base(message)
    {
        Position = position;
    }
}


/// <summary>
/// Recursive descent parser for expression strings.
///
/// Operator precedence (lowest to highest):
/// 1. Ternary: ? :
/// 2. Logical OR: ||
/// 3. Logical AND: &amp;&amp;
/// 4. Equality: == !=
/// 5. Comparison: &lt; &gt; &lt;= &gt;=
/// 6. Additive: + -
/// 7. Multiplicative: * / %
/// 8. Unary: - !
/// 9. Primary: numbers, strings, variables, function calls, parentheses
/// </summary>
public class ExpressionParser
{
    private IReadOnlyList<Token> _tokens = null!;
    private int _current;

    /// <summary>
    /// Parse an expression string into an AST.
    /// </summary>
    public ExprNode Parse(string expression)
    {
        var lexer = new ExpressionLexer(expression);
        _tokens = lexer.Tokenize();
        _current = 0;

        // Check for lexer errors
        foreach (var token in _tokens)
        {
            if (token.Type == TokenType.Error)
            {
                throw new ParseException(token.Value, token.Position);
            }
        }

        var result = ParseExpression();

        if (!IsAtEnd())
        {
            throw new ParseException($"Unexpected token: {Peek().Value}", Peek().Position);
        }

        return result;
    }

    /// <summary>
    /// Parse tokens directly (for when lexer is called separately).
    /// </summary>
    public ExprNode Parse(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
        _current = 0;

        var result = ParseExpression();

        if (!IsAtEnd())
        {
            throw new ParseException($"Unexpected token: {Peek().Value}", Peek().Position);
        }

        return result;
    }

    private ExprNode ParseExpression()
    {
        return ParseTernary();
    }

    // Ternary: condition ? trueExpr : falseExpr
    private ExprNode ParseTernary()
    {
        var expr = ParseOr();

        if (Match(TokenType.Question))
        {
            var trueExpr = ParseExpression();
            Consume(TokenType.Colon, "Expected ':' in ternary expression");
            var falseExpr = ParseTernary();
            return new TernaryNode(expr, trueExpr, falseExpr);
        }

        return expr;
    }

    // Logical OR: ||
    private ExprNode ParseOr()
    {
        var expr = ParseAnd();

        while (Match(TokenType.PipePipe))
        {
            var right = ParseAnd();
            expr = new BinaryOpNode(expr, BinaryOperator.Or, right);
        }

        return expr;
    }

    // Logical AND: &&
    private ExprNode ParseAnd()
    {
        var expr = ParseEquality();

        while (Match(TokenType.AmpersandAmpersand))
        {
            var right = ParseEquality();
            expr = new BinaryOpNode(expr, BinaryOperator.And, right);
        }

        return expr;
    }

    // Equality: == !=
    private ExprNode ParseEquality()
    {
        var expr = ParseComparison();

        while (true)
        {
            if (Match(TokenType.EqualEqual))
            {
                var right = ParseComparison();
                expr = new BinaryOpNode(expr, BinaryOperator.Equal, right);
            }
            else if (Match(TokenType.BangEqual))
            {
                var right = ParseComparison();
                expr = new BinaryOpNode(expr, BinaryOperator.NotEqual, right);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    // Comparison: < > <= >=
    private ExprNode ParseComparison()
    {
        var expr = ParseAdditive();

        while (true)
        {
            if (Match(TokenType.LessThan))
            {
                var right = ParseAdditive();
                expr = new BinaryOpNode(expr, BinaryOperator.LessThan, right);
            }
            else if (Match(TokenType.GreaterThan))
            {
                var right = ParseAdditive();
                expr = new BinaryOpNode(expr, BinaryOperator.GreaterThan, right);
            }
            else if (Match(TokenType.LessThanOrEqual))
            {
                var right = ParseAdditive();
                expr = new BinaryOpNode(expr, BinaryOperator.LessThanOrEqual, right);
            }
            else if (Match(TokenType.GreaterThanOrEqual))
            {
                var right = ParseAdditive();
                expr = new BinaryOpNode(expr, BinaryOperator.GreaterThanOrEqual, right);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    // Additive: + -
    private ExprNode ParseAdditive()
    {
        var expr = ParseMultiplicative();

        while (true)
        {
            if (Match(TokenType.Plus))
            {
                var right = ParseMultiplicative();
                expr = new BinaryOpNode(expr, BinaryOperator.Add, right);
            }
            else if (Match(TokenType.Minus))
            {
                var right = ParseMultiplicative();
                expr = new BinaryOpNode(expr, BinaryOperator.Subtract, right);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    // Multiplicative: * / %
    private ExprNode ParseMultiplicative()
    {
        var expr = ParseUnary();

        while (true)
        {
            if (Match(TokenType.Star))
            {
                var right = ParseUnary();
                expr = new BinaryOpNode(expr, BinaryOperator.Multiply, right);
            }
            else if (Match(TokenType.Slash))
            {
                var right = ParseUnary();
                expr = new BinaryOpNode(expr, BinaryOperator.Divide, right);
            }
            else if (Match(TokenType.Percent))
            {
                var right = ParseUnary();
                expr = new BinaryOpNode(expr, BinaryOperator.Modulo, right);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    // Unary: - !
    private ExprNode ParseUnary()
    {
        if (Match(TokenType.Minus))
        {
            var operand = ParseUnary();
            return new UnaryOpNode(UnaryOperator.Negate, operand);
        }

        if (Match(TokenType.Bang))
        {
            var operand = ParseUnary();
            return new UnaryOpNode(UnaryOperator.Not, operand);
        }

        return ParsePrimary();
    }

    // Primary: numbers, strings, booleans, variables, function calls, parentheses
    private ExprNode ParsePrimary()
    {
        // Number
        if (Match(TokenType.Number))
        {
            var token = Previous();
            if (float.TryParse(token.Value, global::System.Globalization.NumberStyles.Float,
                global::System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return new NumberNode(value);
            }
            throw new ParseException($"Invalid number: {token.Value}", token.Position);
        }

        // String
        if (Match(TokenType.String))
        {
            return new StringNode(Previous().Value);
        }

        // Boolean literals
        if (Match(TokenType.True))
        {
            return new BooleanNode(true);
        }
        if (Match(TokenType.False))
        {
            return new BooleanNode(false);
        }

        // Variable
        if (Match(TokenType.Variable))
        {
            return new VariableNode(Previous().Value);
        }

        // Identifier (function call)
        if (Match(TokenType.Identifier))
        {
            var name = Previous().Value;

            // Check if this is a function call
            if (Match(TokenType.LeftParen))
            {
                var arguments = new List<ExprNode>();

                if (!Check(TokenType.RightParen))
                {
                    do
                    {
                        arguments.Add(ParseExpression());
                    } while (Match(TokenType.Comma));
                }

                Consume(TokenType.RightParen, "Expected ')' after function arguments");
                return new FunctionCallNode(name, arguments);
            }

            // Bare identifier - treat as variable for backwards compatibility
            // This allows "x" to work the same as "$x" in some contexts
            return new VariableNode(name);
        }

        // Parenthesized expression
        if (Match(TokenType.LeftParen))
        {
            var expr = ParseExpression();
            Consume(TokenType.RightParen, "Expected ')' after expression");
            return expr;
        }

        throw new ParseException($"Expected expression, got: {Peek().Type}", Peek().Position);
    }

    // Helper methods

    private bool Match(TokenType type)
    {
        if (Check(type))
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd())
            return false;
        return Peek().Type == type;
    }

    private Token Advance()
    {
        if (!IsAtEnd())
            _current++;
        return Previous();
    }

    private bool IsAtEnd() => Peek().Type == TokenType.EndOfInput;

    private Token Peek() => _tokens[_current];

    private Token Previous() => _tokens[_current - 1];

    private void Consume(TokenType type, string message)
    {
        if (Check(type))
        {
            Advance();
            return;
        }
        throw new ParseException(message, Peek().Position);
    }
}
