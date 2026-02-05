using System;
using System.Collections.Generic;
using System.Linq;

namespace builtin.tools.Lindenmayer.Expressions;

/// <summary>
/// Base class for all expression AST nodes.
/// </summary>
public abstract class ExprNode
{
    public abstract object Evaluate(ExpressionContext context);
}


/// <summary>
/// Represents a numeric literal (float).
/// </summary>
public class NumberNode : ExprNode
{
    public float Value { get; }

    public NumberNode(float value)
    {
        Value = value;
    }

    public override object Evaluate(ExpressionContext context) => Value;

    public override string ToString() => Value.ToString();
}


/// <summary>
/// Represents a string literal.
/// </summary>
public class StringNode : ExprNode
{
    public string Value { get; }

    public StringNode(string value)
    {
        Value = value;
    }

    public override object Evaluate(ExpressionContext context) => Value;

    public override string ToString() => $"\"{Value}\"";
}


/// <summary>
/// Represents a boolean literal.
/// </summary>
public class BooleanNode : ExprNode
{
    public bool Value { get; }

    public BooleanNode(bool value)
    {
        Value = value;
    }

    public override object Evaluate(ExpressionContext context) => Value;

    public override string ToString() => Value.ToString().ToLower();
}


/// <summary>
/// Represents a variable reference (e.g., $r, $l).
/// </summary>
public class VariableNode : ExprNode
{
    public string Name { get; }

    public VariableNode(string name)
    {
        Name = name;
    }

    public override object Evaluate(ExpressionContext context)
    {
        return context.GetVariable(Name);
    }

    public override string ToString() => $"${Name}";
}


/// <summary>
/// Binary operators.
/// </summary>
public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal,
    NotEqual,
    And,
    Or
}


/// <summary>
/// Represents a binary operation (e.g., a + b, x > y).
/// </summary>
public class BinaryOpNode : ExprNode
{
    public ExprNode Left { get; }
    public BinaryOperator Operator { get; }
    public ExprNode Right { get; }

    public BinaryOpNode(ExprNode left, BinaryOperator op, ExprNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }

    public override object Evaluate(ExpressionContext context)
    {
        // Short-circuit evaluation for logical operators
        if (Operator == BinaryOperator.And)
        {
            var leftVal = Left.Evaluate(context);
            if (!ExpressionContext.ToBoolean(leftVal))
                return false;
            return ExpressionContext.ToBoolean(Right.Evaluate(context));
        }

        if (Operator == BinaryOperator.Or)
        {
            var leftVal = Left.Evaluate(context);
            if (ExpressionContext.ToBoolean(leftVal))
                return true;
            return ExpressionContext.ToBoolean(Right.Evaluate(context));
        }

        var left = Left.Evaluate(context);
        var right = Right.Evaluate(context);

        return Operator switch
        {
            BinaryOperator.Add => ExpressionContext.ToFloat(left) + ExpressionContext.ToFloat(right),
            BinaryOperator.Subtract => ExpressionContext.ToFloat(left) - ExpressionContext.ToFloat(right),
            BinaryOperator.Multiply => ExpressionContext.ToFloat(left) * ExpressionContext.ToFloat(right),
            BinaryOperator.Divide => ExpressionContext.ToFloat(left) / ExpressionContext.ToFloat(right),
            BinaryOperator.Modulo => ExpressionContext.ToFloat(left) % ExpressionContext.ToFloat(right),
            BinaryOperator.GreaterThan => ExpressionContext.ToFloat(left) > ExpressionContext.ToFloat(right),
            BinaryOperator.LessThan => ExpressionContext.ToFloat(left) < ExpressionContext.ToFloat(right),
            BinaryOperator.GreaterThanOrEqual => ExpressionContext.ToFloat(left) >= ExpressionContext.ToFloat(right),
            BinaryOperator.LessThanOrEqual => ExpressionContext.ToFloat(left) <= ExpressionContext.ToFloat(right),
            BinaryOperator.Equal => Math.Abs(ExpressionContext.ToFloat(left) - ExpressionContext.ToFloat(right)) < 0.0001f,
            BinaryOperator.NotEqual => Math.Abs(ExpressionContext.ToFloat(left) - ExpressionContext.ToFloat(right)) >= 0.0001f,
            _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
        };
    }

    public override string ToString() => $"({Left} {OperatorToString(Operator)} {Right})";

    private static string OperatorToString(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.LessThan => "<",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.And => "&&",
        BinaryOperator.Or => "||",
        _ => "?"
    };
}


/// <summary>
/// Unary operators.
/// </summary>
public enum UnaryOperator
{
    Negate,
    Not
}


/// <summary>
/// Represents a unary operation (e.g., -x, !condition).
/// </summary>
public class UnaryOpNode : ExprNode
{
    public UnaryOperator Operator { get; }
    public ExprNode Operand { get; }

    public UnaryOpNode(UnaryOperator op, ExprNode operand)
    {
        Operator = op;
        Operand = operand;
    }

    public override object Evaluate(ExpressionContext context)
    {
        var value = Operand.Evaluate(context);

        return Operator switch
        {
            UnaryOperator.Negate => -ExpressionContext.ToFloat(value),
            UnaryOperator.Not => !ExpressionContext.ToBoolean(value),
            _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
        };
    }

    public override string ToString() => Operator switch
    {
        UnaryOperator.Negate => $"-{Operand}",
        UnaryOperator.Not => $"!{Operand}",
        _ => $"?{Operand}"
    };
}


/// <summary>
/// Represents a function call (e.g., rnd(), sin(x), clamp(x, min, max)).
/// </summary>
public class FunctionCallNode : ExprNode
{
    public string FunctionName { get; }
    public IReadOnlyList<ExprNode> Arguments { get; }

    public FunctionCallNode(string functionName, IReadOnlyList<ExprNode> arguments)
    {
        FunctionName = functionName;
        Arguments = arguments;
    }

    public override object Evaluate(ExpressionContext context)
    {
        return context.CallFunction(FunctionName, Arguments);
    }

    public override string ToString()
    {
        var args = string.Join(", ", Arguments.Select(a => a.ToString()));
        return $"{FunctionName}({args})";
    }
}


/// <summary>
/// Represents a ternary conditional expression (condition ? trueExpr : falseExpr).
/// </summary>
public class TernaryNode : ExprNode
{
    public ExprNode Condition { get; }
    public ExprNode TrueExpr { get; }
    public ExprNode FalseExpr { get; }

    public TernaryNode(ExprNode condition, ExprNode trueExpr, ExprNode falseExpr)
    {
        Condition = condition;
        TrueExpr = trueExpr;
        FalseExpr = falseExpr;
    }

    public override object Evaluate(ExpressionContext context)
    {
        var conditionValue = Condition.Evaluate(context);
        if (ExpressionContext.ToBoolean(conditionValue))
        {
            return TrueExpr.Evaluate(context);
        }
        return FalseExpr.Evaluate(context);
    }

    public override string ToString() => $"({Condition} ? {TrueExpr} : {FalseExpr})";
}
