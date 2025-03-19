using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using System.Net.Http;
using Mono.Cecil.Cil;
using System.Reflection;
using static UnityEngine.Rendering.DebugUI;

[Serializable]
public class UCExpression
{
    public interface IContext
    {
        DataType GetVariableDataType(string varName);
        float GetVarNumber(string varName);
        bool GetVarBool(string varName);
        string GetVarString(string varName);
        void SetVariable(string varName, float value);
        void SetVariable(string varName, bool value);
        void SetVariable(string varName, string value);
        DataType GetFunctionType(string functionName);
    }

    public class ErrorException : Exception
    {
        public ErrorException(string message) : base(message)
        {
        }
    }

    public enum Type
    {
        Neg, And, Or, Less, LEqual, Greater, GEqual, Equal, NEqual,
        Var, FLiteral, BLiteral, SLiteral,
        UnaryMinus, UnaryPlus, Add, Subtract, Multiply, Divide, Modulo, FunctionCall
    }
    public enum DataType { None, Undefined, Bool, Number, String }

    public Type                 type; // condition as a string, to be parsed later
    public List<UCExpression>   args;
    public float                fLiteral;
    public string               sLiteral;
    public bool                 bLiteral;

    public bool EvaluateBool(IContext context)
    {
        switch (type)
        {
            case Type.Neg:
                CheckArguments(1, "negation", context, DataType. Bool);
                return !args[0].EvaluateBool(context);
            case Type.And:
                CheckArguments(2, "and", context, DataType.Bool, DataType.Bool);
                return args[0].EvaluateBool(context) && args[1].EvaluateBool(context);
            case Type.Or:
                CheckArguments(2, "or", context, DataType.Bool, DataType.Bool);
                return args[0].EvaluateBool(context) || args[1].EvaluateBool(context);
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
            case Type.SLiteral:
                return !string.IsNullOrEmpty(sLiteral);
            case Type.FunctionCall:
                if (EvaluateFunction<bool>(context) is bool boolValue) return boolValue;
                return false;
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
                return (bLiteral) ? 1.0f : 0.0f;
            case Type.SLiteral:
                return float.TryParse(sLiteral, out var result) ? result : 0f;
            case Type.UnaryMinus:
                return -args[0].EvaluateNumber(context);
            case Type.UnaryPlus:
                return args[0].EvaluateNumber(context);
            case Type.Add:
                return args[0].EvaluateNumber(context) + args[1].EvaluateNumber(context);
            case Type.Subtract:
                return args[0].EvaluateNumber(context) - args[1].EvaluateNumber(context);
            case Type.Multiply:
                return args[0].EvaluateNumber(context) * args[1].EvaluateNumber(context);
            case Type.Divide:
                return args[0].EvaluateNumber(context) / args[1].EvaluateNumber(context);
            case Type.Modulo:
                return args[0].EvaluateNumber(context) % args[1].EvaluateNumber(context);
            case Type.FunctionCall:
                if (EvaluateFunction<float>(context) is float floatValue) return floatValue;
                return 0.0f;
        }

        throw new ErrorException($"Not a number in expression of type {type}!");
    }

    public string EvaluateString(IContext context)
    {
        switch (type)
        {
            case Type.Var:
                return context.GetVarString(sLiteral);
            case Type.FLiteral:
                return fLiteral.ToString();
            case Type.BLiteral:
                return (bLiteral) ? ("true") : ("false");
            case Type.SLiteral:
                return sLiteral;
            case Type.FunctionCall:
                if (EvaluateFunction<string>(context) is string stringValue) return stringValue;
                return "";
        }

        throw (new ErrorException($"Not a string in expression of type {type}!"));
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
            case Type.Var: return context.GetVariableDataType(sLiteral);
            case Type.FLiteral: return DataType.Number;
            case Type.BLiteral: return DataType.Bool;
            case Type.SLiteral: return DataType.String;
            case Type.FunctionCall: return context.GetFunctionType(sLiteral);
        }
        return DataType.None;
    }

    private T EvaluateFunction<T>(IContext context)
    {
        var type = context.GetType();
        var methodInfo = type.GetMethod(sLiteral,
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (methodInfo == null)
        {
            throw new UCExpression.ErrorException($"Method \"{sLiteral}\" not found in context!");
        }

        // Check parameters, check parameter types
        List<object> funcArgs = new();
        ParameterInfo[] parameters = methodInfo.GetParameters();

        if (parameters.Length != args.Count)
        {
            throw new ErrorException($"Invalid number of argument for \"{sLiteral}\": expected {parameters.Length}, received {args.Count}!");
        }
        else
        {
            for (int index = 0; index < parameters.Length; index++)
            {
                ParameterInfo param = parameters[index];

                System.Type paramType = param.ParameterType;
                var         expression = args[index];

                if (paramType == typeof(bool))
                {
                    var pType = expression.GetDataType(context);
                    if ((pType == DataType.Bool) || (pType == DataType.Undefined))
                    {
                        funcArgs.Add(Convert.ChangeType(expression.EvaluateBool(context), paramType));
                    }
                    else
                    {
                        Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{sLiteral}\", received {pType}!");
                    }
                }
                else if ((paramType == typeof(float)) ||
                         (paramType == typeof(int)))
                {
                    var pType = expression.GetDataType(context);
                    if ((pType == DataType.Number) || (pType == DataType.Undefined))
                    {
                        funcArgs.Add(Convert.ChangeType(expression.EvaluateNumber(context), paramType));
                    }
                    else
                    {
                        Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{sLiteral}\", received {pType}!");
                    }
                }
                else if (paramType == typeof(string))
                {
                    var pType = expression.GetDataType(context);
                    if ((pType == DataType.String) || (pType == DataType.Undefined))
                    {
                        funcArgs.Add(Convert.ChangeType(expression.EvaluateString(context), paramType));
                    }
                    else
                    {
                        Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{sLiteral}\", received {pType}!");
                    }
                }
                else
                {
                    Debug.LogError($"Unsupported type {paramType} for argument #{index} ({param.Name}) for call to \"{sLiteral}\"!");
                }
            }
            if (funcArgs.Count == parameters.Length)
            {
                return (T)Convert.ChangeType(methodInfo.Invoke(context, funcArgs.ToArray()), typeof(T));
            }
            else
            {
                throw new ErrorException($"Failed to call method {sLiteral}!");
            }
        }        
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

    enum TokenType
    {
        None, End, Identifier, Number, String, And, Or, Neg, LParen, RParen, Comma,
        Less, LEqual, Greater, GEqual, Equal, NEqual, True, False,
        Plus, Minus, Multiply, Divide, Modulo
    }

    class Tokenizer
    {
        string expr;
        int pos;
        public TokenType CurrentToken { get; private set; }
        public string TokenValue { get; private set; }

        public Tokenizer(string expr)
        {
            this.expr = expr;
            NextToken();
        }

        public void NextToken()
        {
            while (pos < expr.Length && char.IsWhiteSpace(expr[pos])) pos++;
            if (pos >= expr.Length) { CurrentToken = TokenType.End; return; }

            char c = expr[pos];

            if (char.IsLetter(c))
            {
                int start = pos;
                while (pos < expr.Length && (char.IsLetterOrDigit(expr[pos]) || expr[pos] == '_')) pos++;
                TokenValue = expr[start..pos];
                CurrentToken = TokenValue switch
                {
                    "true" or "yes" => TokenType.True,
                    "false" or "no" => TokenType.False,
                    _ => TokenType.Identifier
                };
            }
            else if (char.IsDigit(c))
            {
                int start = pos;
                while (pos < expr.Length && (char.IsDigit(expr[pos]) || expr[pos] == '.')) pos++;
                TokenValue = expr[start..pos];
                CurrentToken = TokenType.Number;
            }
            else if (c == '"' || c == '\'')  // Added string literal handling here
            {
                char quoteType = c;
                pos++;
                int start = pos;
                while (pos < expr.Length && expr[pos] != quoteType)
                {
                    if (expr[pos] == '\\' && pos + 1 < expr.Length) pos += 2; // handle escape
                    else pos++;
                }
                if (pos >= expr.Length)
                    throw new ErrorException("Unterminated string literal.");

                TokenValue = expr[start..pos];
                pos++; // Skip closing quote
                CurrentToken = TokenType.String;
            }
            else
            {
                switch (c)
                {
                    case '+': pos++; CurrentToken = TokenType.Plus; break;
                    case '-': pos++; CurrentToken = TokenType.Minus; break;
                    case '*': pos++; CurrentToken = TokenType.Multiply; break;
                    case '/': pos++; CurrentToken = TokenType.Divide; break;
                    case '%': pos++; CurrentToken = TokenType.Modulo; break;
                    case '(': pos++; CurrentToken = TokenType.LParen; break;
                    case ')': pos++; CurrentToken = TokenType.RParen; break;
                    case ',': pos++; CurrentToken = TokenType.Comma; break;
                    default: throw new Exception($"Invalid char '{c}'");
                }
            }
        }
    }

    private static UCExpression ParseExpression(Tokenizer tokenizer) => ParseAddSubtract(tokenizer);

    private static UCExpression ParseAddSubtract(Tokenizer tokenizer)
    {
        var left = ParseMultiplyDivide(tokenizer);
        while (tokenizer.CurrentToken is TokenType.Plus or TokenType.Minus)
        {
            var op = tokenizer.CurrentToken;
            tokenizer.NextToken();
            left = new UCExpression
            {
                type = op == TokenType.Plus ? Type.Add : Type.Subtract,
                args = new List<UCExpression> { left, ParseMultiplyDivide(tokenizer) }
            };
        }
        return left;
    }

    private static UCExpression ParseMultiplyDivide(Tokenizer tokenizer)
    {
        var left = ParseUnary(tokenizer);
        while (tokenizer.CurrentToken is TokenType.Multiply or TokenType.Divide or TokenType.Modulo)
        {
            var op = tokenizer.CurrentToken;
            tokenizer.NextToken();
            left = new UCExpression
            {
                type = op switch
                {
                    TokenType.Multiply => Type.Multiply,
                    TokenType.Divide => Type.Divide,
                    TokenType.Modulo => Type.Modulo,
                    _ => throw new ErrorException("Invalid arithmetic operator.")
                },
                args = new List<UCExpression> { left, ParseUnary(tokenizer) }
            };
        }
        return left;
    }

    private static UCExpression ParseUnary(Tokenizer tokenizer)
    {
        if (tokenizer.CurrentToken is TokenType.Minus or TokenType.Plus or TokenType.Neg)
        {
            var op = tokenizer.CurrentToken;
            tokenizer.NextToken();
            return new UCExpression
            {
                type = op switch
                {
                    TokenType.Minus => Type.UnaryMinus,
                    TokenType.Plus => Type.UnaryPlus,
                    TokenType.Neg => Type.Neg,
                    _ => throw new ErrorException("Invalid unary operator.")
                },
                args = new List<UCExpression> { ParseUnary(tokenizer) }
            };
        }
        return ParsePrimary(tokenizer);
    }

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

    private static UCExpression ParsePrimary(Tokenizer tokenizer)
    {
        switch (tokenizer.CurrentToken)
        {
            case TokenType.Identifier:
                var identifier = tokenizer.TokenValue;
                tokenizer.NextToken();

                if (tokenizer.CurrentToken == TokenType.LParen) // Function call
                {
                    tokenizer.NextToken(); // Skip '('
                    var args = new List<UCExpression>();

                    if (tokenizer.CurrentToken != TokenType.RParen)
                    {
                        do
                        {
                            args.Add(ParseExpression(tokenizer));
                            if (tokenizer.CurrentToken == TokenType.Comma)
                            {
                                tokenizer.NextToken(); // Skip ','
                            }
                            else
                            {
                                break;
                            }
                        }
                        while (true);
                    }

                    if (tokenizer.CurrentToken != TokenType.RParen)
                        throw new ErrorException("Expected closing parenthesis in function call");

                    tokenizer.NextToken(); // Skip ')'
                    return new UCExpression { type = Type.FunctionCall, sLiteral = identifier, args = args };
                }

                // It's a variable
                return new UCExpression { type = Type.Var, sLiteral = identifier };

            case TokenType.Number:
                var exprNum = new UCExpression { type = Type.FLiteral, fLiteral = float.Parse(tokenizer.TokenValue) };
                tokenizer.NextToken();
                return exprNum;

            case TokenType.String:
                var exprStr = new UCExpression { type = Type.SLiteral, sLiteral = tokenizer.TokenValue };
                tokenizer.NextToken();
                return exprStr;

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
