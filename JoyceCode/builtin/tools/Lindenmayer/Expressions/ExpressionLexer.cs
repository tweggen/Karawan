using System;
using System.Collections.Generic;

namespace builtin.tools.Lindenmayer.Expressions;

/// <summary>
/// Token types for the expression lexer.
/// </summary>
public enum TokenType
{
    // Literals
    Number,
    String,
    True,
    False,

    // Identifiers and variables
    Identifier,    // Function names, etc.
    Variable,      // $varname

    // Operators
    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    // Comparison
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    EqualEqual,
    BangEqual,

    // Logical
    AmpersandAmpersand,
    PipePipe,
    Bang,

    // Ternary
    Question,
    Colon,

    // Grouping
    LeftParen,
    RightParen,
    Comma,

    // End
    EndOfInput,

    // Error
    Error
}


/// <summary>
/// Represents a token produced by the lexer.
/// </summary>
public readonly struct Token
{
    public TokenType Type { get; }
    public string Value { get; }
    public int Position { get; }

    public Token(TokenType type, string value, int position)
    {
        Type = type;
        Value = value;
        Position = position;
    }

    public override string ToString() => $"{Type}({Value}) at {Position}";
}


/// <summary>
/// Tokenizes expression strings into a stream of tokens.
/// </summary>
public class ExpressionLexer
{
    private readonly string _input;
    private int _position;
    private readonly List<Token> _tokens;

    public ExpressionLexer(string input)
    {
        _input = input ?? string.Empty;
        _position = 0;
        _tokens = new List<Token>();
    }

    /// <summary>
    /// Tokenize the entire input and return all tokens.
    /// </summary>
    public IReadOnlyList<Token> Tokenize()
    {
        _tokens.Clear();
        _position = 0;

        while (!IsAtEnd())
        {
            SkipWhitespace();
            if (IsAtEnd())
                break;

            var token = ScanToken();
            _tokens.Add(token);

            if (token.Type == TokenType.Error)
                break;
        }

        _tokens.Add(new Token(TokenType.EndOfInput, "", _position));
        return _tokens;
    }

    private Token ScanToken()
    {
        int start = _position;
        char c = Advance();

        // Single-character tokens
        switch (c)
        {
            case '(': return new Token(TokenType.LeftParen, "(", start);
            case ')': return new Token(TokenType.RightParen, ")", start);
            case ',': return new Token(TokenType.Comma, ",", start);
            case '+': return new Token(TokenType.Plus, "+", start);
            case '-': return new Token(TokenType.Minus, "-", start);
            case '*': return new Token(TokenType.Star, "*", start);
            case '/': return new Token(TokenType.Slash, "/", start);
            case '%': return new Token(TokenType.Percent, "%", start);
            case '?': return new Token(TokenType.Question, "?", start);
            case ':': return new Token(TokenType.Colon, ":", start);
        }

        // Two-character tokens
        if (c == '>' && Match('='))
            return new Token(TokenType.GreaterThanOrEqual, ">=", start);
        if (c == '>')
            return new Token(TokenType.GreaterThan, ">", start);

        if (c == '<' && Match('='))
            return new Token(TokenType.LessThanOrEqual, "<=", start);
        if (c == '<')
            return new Token(TokenType.LessThan, "<", start);

        if (c == '=' && Match('='))
            return new Token(TokenType.EqualEqual, "==", start);

        if (c == '!' && Match('='))
            return new Token(TokenType.BangEqual, "!=", start);
        if (c == '!')
            return new Token(TokenType.Bang, "!", start);

        if (c == '&' && Match('&'))
            return new Token(TokenType.AmpersandAmpersand, "&&", start);

        if (c == '|' && Match('|'))
            return new Token(TokenType.PipePipe, "||", start);

        // String literals
        if (c == '"')
            return ScanString(start);

        // Variables ($name)
        if (c == '$')
            return ScanVariable(start);

        // Numbers
        if (char.IsDigit(c) || (c == '.' && !IsAtEnd() && char.IsDigit(Peek())))
        {
            return ScanNumber(start, c);
        }

        // Identifiers and keywords
        if (char.IsLetter(c) || c == '_')
            return ScanIdentifier(start, c);

        return new Token(TokenType.Error, $"Unexpected character: {c}", start);
    }

    private Token ScanString(int start)
    {
        var sb = new global::System.Text.StringBuilder();

        while (!IsAtEnd() && Peek() != '"')
        {
            char c = Advance();
            if (c == '\\' && !IsAtEnd())
            {
                char escaped = Advance();
                sb.Append(escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '"' => '"',
                    '\\' => '\\',
                    _ => escaped
                });
            }
            else
            {
                sb.Append(c);
            }
        }

        if (IsAtEnd())
            return new Token(TokenType.Error, "Unterminated string", start);

        Advance(); // Consume closing quote
        return new Token(TokenType.String, sb.ToString(), start);
    }

    private Token ScanVariable(int start)
    {
        var sb = new global::System.Text.StringBuilder();

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }

        if (sb.Length == 0)
            return new Token(TokenType.Error, "Expected variable name after $", start);

        return new Token(TokenType.Variable, sb.ToString(), start);
    }

    private Token ScanNumber(int start, char firstChar)
    {
        var sb = new global::System.Text.StringBuilder();
        sb.Append(firstChar);

        // Integer part
        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        // Decimal part
        if (!IsAtEnd() && Peek() == '.' && (IsAtEnd(1) || char.IsDigit(Peek(1))))
        {
            sb.Append(Advance()); // Consume '.'
            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        // Exponent part
        if (!IsAtEnd() && (Peek() == 'e' || Peek() == 'E'))
        {
            sb.Append(Advance());
            if (!IsAtEnd() && (Peek() == '+' || Peek() == '-'))
            {
                sb.Append(Advance());
            }
            while (!IsAtEnd() && char.IsDigit(Peek()))
            {
                sb.Append(Advance());
            }
        }

        return new Token(TokenType.Number, sb.ToString(), start);
    }

    private Token ScanIdentifier(int start, char firstChar)
    {
        var sb = new global::System.Text.StringBuilder();
        sb.Append(firstChar);

        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }

        string value = sb.ToString();

        // Check for keywords
        return value.ToLower() switch
        {
            "true" => new Token(TokenType.True, value, start),
            "false" => new Token(TokenType.False, value, start),
            _ => new Token(TokenType.Identifier, value, start)
        };
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
        {
            Advance();
        }
    }

    private bool IsAtEnd(int offset = 0) => _position + offset >= _input.Length;

    private char Peek(int offset = 0)
    {
        int pos = _position + offset;
        return pos < _input.Length ? _input[pos] : '\0';
    }

    private char Advance()
    {
        return _input[_position++];
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _input[_position] != expected)
            return false;
        _position++;
        return true;
    }
}
