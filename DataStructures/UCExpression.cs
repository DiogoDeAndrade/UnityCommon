using System.Collections.Generic;
using System;

[Serializable]
public class UCExpression
{
    public interface IContext
    {
        DataType GetDataType(string varName);
        float GetVarNumber(string varName);
        bool GetVarBool(string varName);
        void SetVariable(string varName, float value);
        void SetVariable(string varName, bool value);
    }

    public class ErrorException : Exception
    {
        public ErrorException(string message) : base(message)
        {
        }
    }

    public enum Type { Neg, And, Or, Less, LEqual, Greater, GEqual, Equal, NEqual, Var, FLiteral, BLiteral }
    public enum DataType { None, Bool, Number }

    public Type                 type; // condition as a string, to be parsed later
    public List<UCExpression>   args;
    public float                fLiteral;
    public string               sLiteral;
    public bool                 bLiteral;

    public bool Evaluate(IContext context)
    {
        switch (type)
        {
            case Type.Neg:
                CheckArguments(1, "negation", context, DataType. Bool);
                return !args[0].Evaluate(context);
            case Type.And:
                CheckArguments(2, "and", context, DataType.Bool, DataType.Bool);
                return args[0].Evaluate(context) && args[1].Evaluate(context);
            case Type.Or:
                CheckArguments(2, "or", context, DataType.Bool, DataType.Bool);
                return args[0].Evaluate(context) || args[1].Evaluate(context);
            case Type.Less:
                CheckArguments(2, "or", context, DataType.Number, DataType.Number);
                return args[0].EvaluateNumber(context) < args[1].EvaluateNumber(context);
            case Type.LEqual:
                CheckArguments(2, "or", context, DataType.Number, DataType.Number);
                return args[0].EvaluateNumber(context) <= args[1].EvaluateNumber(context);
            case Type.Greater:
                CheckArguments(2, "or", context, DataType.Number, DataType.Number);
                return args[0].EvaluateNumber(context) > args[1].EvaluateNumber(context);
            case Type.GEqual:
                CheckArguments(2, "or", context, DataType.Number, DataType.Number);
                return args[0].EvaluateNumber(context) >= args[1].EvaluateNumber(context);
            case Type.Equal:
                CheckArguments(2, "or", context, DataType.Number, DataType.Number);
                return args[0].EvaluateNumber(context) == args[1].EvaluateNumber(context);
            case Type.NEqual:
                CheckArguments(2, "or", context, DataType.Number, DataType.Number);
                return args[0].EvaluateNumber(context) != args[1].EvaluateNumber(context);
            case Type.Var:
                if (GetDataType(context) == DataType.Bool) return context.GetVarBool(sLiteral);
                else return context.GetVarNumber(sLiteral) != 0.0f;
            case Type.FLiteral:
                return fLiteral != 0.0f;
            case Type.BLiteral:
                return bLiteral;
            default:
                break;
        }

        throw (new ErrorException($"Invalid expression type {type}!"));
    }

    public float EvaluateNumber(IContext context)
    {
        switch (type)
        {
            case Type.Var:
                return context.GetVarNumber(sLiteral);
            case Type.FLiteral:
                return fLiteral;
            case Type.BLiteral:
                return (bLiteral) ? (1.0f) : (0.0f);
        }

        throw (new ErrorException($"Not a number in expression of type {type}!"));
    }

    public DataType GetDataType(IContext context)
    {
        switch (type)
        {
            case Type.Neg: return DataType.Bool;
            case Type.And: return DataType.Bool;
            case Type.Or: return DataType.Bool;
            case Type.Less: return DataType.Bool;
            case Type.LEqual: return DataType.Bool;
            case Type.Greater: return DataType.Bool;
            case Type.GEqual: return DataType.Bool;
            case Type.Equal: return DataType.Bool;
            case Type.NEqual: return DataType.Bool;
            case Type.Var: return context.GetDataType(sLiteral);
            case Type.FLiteral: return DataType.Number;
            case Type.BLiteral: return DataType.Bool;
        }
        return DataType.None;
    }

    bool CheckArguments(int count, string stringContext, IContext context, params DataType[] dataTypes)
    {
        if (args == null)
        {
            throw new ErrorException($"No arguments for {stringContext}");
        }
        if (args.Count != count)
        {
            throw new ErrorException($"Wrong number of arguments for {stringContext}");
        }
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].GetDataType(context) != dataTypes[i])
            {
                if (args[i].type == Type.Var)
                    throw new ErrorException($"Bad argument #{i} - expected {dataTypes[i]}, received {args[i].GetDataType(context)} for variable {args[i].sLiteral}");
                else
                    throw new ErrorException($"Bad argument #{i} - expected {dataTypes[i]}, received {args[i].GetDataType(context)}");
            }
        }

        return true;
    }

    public static bool TryParse(string expressionString, out UCExpression expression)
    {
        try
        {
            var tokenizer = new Tokenizer(expressionString);
            expression = ParseExpression(tokenizer);
            if (tokenizer.CurrentToken != TokenType.End)
                throw new ErrorException("Unexpected token at the end of expression.");
            return true;
        }
        catch
        {
            expression = null;
            return false;
        }
    }

    private enum TokenType { None, End, Identifier, Number, And, Or, Neg, LParen, RParen, Less, LEqual, Greater, GEqual, Equal, NEqual, True, False }

    private class Tokenizer
    {
        string expression;
        int pos;
        public TokenType CurrentToken { get; private set; }
        public string TokenValue { get; private set; }

        public Tokenizer(string expr)
        {
            expression = expr;
            pos = 0;
            NextToken();
        }

        // Modify Tokenizer.NextToken() method:
        public void NextToken()
        {
            while (pos < expression.Length && char.IsWhiteSpace(expression[pos])) pos++;
            if (pos >= expression.Length)
            {
                CurrentToken = TokenType.End;
                return;
            }

            char c = expression[pos];

            if (char.IsLetter(c))
            {
                int start = pos;
                while (pos < expression.Length && (char.IsLetterOrDigit(expression[pos]) || expression[pos] == '_')) pos++;
                TokenValue = expression.Substring(start, pos - start);

                if (TokenValue is "true" or "yes")
                    CurrentToken = TokenType.True;
                else if (TokenValue is "false" or "no")
                    CurrentToken = TokenType.False;
                else
                    CurrentToken = TokenType.Identifier;
            }
            else if (char.IsDigit(c))
            {
                int start = pos;
                while (pos < expression.Length && (char.IsDigit(expression[pos]) || expression[pos] == '.')) pos++;
                TokenValue = expression.Substring(start, pos - start);
                CurrentToken = TokenType.Number;
            }
            else
            {
                switch (c)
                {
                    case '!': pos++; CurrentToken = (pos < expression.Length && expression[pos] == '=') ? (++pos, TokenType.NEqual).Item2 : TokenType.Neg; break;
                    case '&': pos += 2; CurrentToken = TokenType.And; break;
                    case '|': pos += 2; CurrentToken = TokenType.Or; break;
                    case '<': pos++; CurrentToken = (pos < expression.Length && expression[pos] == '=') ? (++pos, TokenType.LEqual).Item2 : TokenType.Less; break;
                    case '>': pos++; CurrentToken = (pos < expression.Length && expression[pos] == '=') ? (++pos, TokenType.GEqual).Item2 : TokenType.Greater; break;
                    case '=': pos += 2; CurrentToken = TokenType.Equal; break;
                    case '(': pos++; CurrentToken = TokenType.LParen; break;
                    case ')': pos++; CurrentToken = TokenType.RParen; break;
                    default: throw new ErrorException($"Invalid character: {c}");
                }
            }
        }
    }

    private static UCExpression ParseExpression(Tokenizer tokenizer) => ParseOr(tokenizer);

    private static UCExpression ParseOr(Tokenizer tokenizer)
    {
        var left = ParseAnd(tokenizer);
        while (tokenizer.CurrentToken == TokenType.Or)
        {
            tokenizer.NextToken();
            left = new UCExpression { type = Type.Or, args = new List<UCExpression> { left, ParseAnd(tokenizer) } };
        }
        return left;
    }

    private static UCExpression ParseAnd(Tokenizer tokenizer)
    {
        var left = ParseEquality(tokenizer);
        while (tokenizer.CurrentToken == TokenType.And)
        {
            tokenizer.NextToken();
            left = new UCExpression { type = Type.And, args = new List<UCExpression> { left, ParseEquality(tokenizer) } };
        }
        return left;
    }

    private static UCExpression ParseEquality(Tokenizer tokenizer)
    {
        var left = ParseComparison(tokenizer);
        while (tokenizer.CurrentToken == TokenType.Equal || tokenizer.CurrentToken == TokenType.NEqual)
        {
            var op = tokenizer.CurrentToken;
            tokenizer.NextToken();
            left = new UCExpression
            {
                type = op == TokenType.Equal ? Type.Equal : Type.NEqual,
                args = new List<UCExpression> { left, ParseComparison(tokenizer) }
            };
        }
        return left;
    }

    private static UCExpression ParseComparison(Tokenizer tokenizer)
    {
        var left = ParseUnary(tokenizer);
        while (tokenizer.CurrentToken is TokenType.Less or TokenType.LEqual or TokenType.Greater or TokenType.GEqual)
        {
            var op = tokenizer.CurrentToken;
            tokenizer.NextToken();
            left = new UCExpression
            {
                type = op switch
                {
                    TokenType.Less => Type.Less,
                    TokenType.LEqual => Type.LEqual,
                    TokenType.Greater => Type.Greater,
                    TokenType.GEqual => Type.GEqual,
                    _ => throw new ErrorException("Invalid comparison operator.")
                },
                args = new List<UCExpression> { left, ParseUnary(tokenizer) }
            };
        }
        return left;
    }

    private static UCExpression ParseUnary(Tokenizer tokenizer)
    {
        if (tokenizer.CurrentToken == TokenType.Neg)
        {
            tokenizer.NextToken();
            return new UCExpression { type = Type.Neg, args = new List<UCExpression> { ParseUnary(tokenizer) } };
        }
        return ParsePrimary(tokenizer);
    }

    private static UCExpression ParsePrimary(Tokenizer tokenizer)
    {
        switch (tokenizer.CurrentToken)
        {
            case TokenType.Identifier:
                var exprVar = new UCExpression { type = Type.Var, sLiteral = tokenizer.TokenValue };
                tokenizer.NextToken();
                return exprVar;

            case TokenType.Number:
                var exprNum = new UCExpression { type = Type.FLiteral, fLiteral = float.Parse(tokenizer.TokenValue) };
                tokenizer.NextToken();
                return exprNum;

            case TokenType.True:
                tokenizer.NextToken();
                return new UCExpression { type = Type.BLiteral, bLiteral = true };

            case TokenType.False:
                tokenizer.NextToken();
                return new UCExpression { type = Type.BLiteral, bLiteral = false };

            case TokenType.LParen:
                tokenizer.NextToken();
                var exprParen = ParseExpression(tokenizer);
                if (tokenizer.CurrentToken != TokenType.RParen)
                    throw new ErrorException("Expected closing parenthesis");
                tokenizer.NextToken();
                return exprParen;

            default:
                throw new ErrorException("Unexpected token");
        }
    }
}
