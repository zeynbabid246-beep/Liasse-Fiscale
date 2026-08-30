using System.Globalization;

namespace LiasseFiscale.Api.Services;

public enum XPathTokenType
{
    Identifier,
    Number,
    String,
    OpenParen,
    CloseParen,
    Comma,
    Equal,
    NotEqual,
    GreaterEqual,
    LessEqual,
    GreaterThan,
    LessThan,
    Plus,
    Minus,
    Multiply,
    Divide,
    If,
    Then,
    Else,
    Sum,
    Or,
    And,
    Not,
    EndOfInput
}

public record XPathToken(XPathTokenType Type, string Text, decimal? NumberValue = null, string? StringValue = null);

public class XPathTokenizer
{
    private readonly string _input;
    private int _pos;

    public XPathTokenizer(string input)
    {
        _input = input ?? string.Empty;
        _pos = 0;
    }

    public List<XPathToken> Tokenize()
    {
        var tokens = new List<XPathToken>();
        while (_pos < _input.Length)
        {
            char c = _input[_pos];

            if (char.IsWhiteSpace(c))
            {
                _pos++;
                continue;
            }

            if (c == '(')
            {
                tokens.Add(new XPathToken(XPathTokenType.OpenParen, "("));
                _pos++;
                continue;
            }
            if (c == ')')
            {
                tokens.Add(new XPathToken(XPathTokenType.CloseParen, ")"));
                _pos++;
                continue;
            }
            if (c == ',')
            {
                tokens.Add(new XPathToken(XPathTokenType.Comma, ","));
                _pos++;
                continue;
            }
            if (c == '+')
            {
                tokens.Add(new XPathToken(XPathTokenType.Plus, "+"));
                _pos++;
                continue;
            }
            if (c == '-')
            {
                tokens.Add(new XPathToken(XPathTokenType.Minus, "-"));
                _pos++;
                continue;
            }
            if (c == '*')
            {
                tokens.Add(new XPathToken(XPathTokenType.Multiply, "*"));
                _pos++;
                continue;
            }
            if (c == '/')
            {
                tokens.Add(new XPathToken(XPathTokenType.Divide, "/"));
                _pos++;
                continue;
            }
            if (c == '=')
            {
                tokens.Add(new XPathToken(XPathTokenType.Equal, "="));
                _pos++;
                continue;
            }
            if (c == '!' && _pos + 1 < _input.Length && _input[_pos + 1] == '=')
            {
                tokens.Add(new XPathToken(XPathTokenType.NotEqual, "!="));
                _pos += 2;
                continue;
            }
            if (c == '>')
            {
                if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                {
                    tokens.Add(new XPathToken(XPathTokenType.GreaterEqual, ">="));
                    _pos += 2;
                }
                else
                {
                    tokens.Add(new XPathToken(XPathTokenType.GreaterThan, ">"));
                    _pos++;
                }
                continue;
            }
            if (c == '<')
            {
                if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                {
                    tokens.Add(new XPathToken(XPathTokenType.LessEqual, "<="));
                    _pos += 2;
                }
                else
                {
                    tokens.Add(new XPathToken(XPathTokenType.LessThan, "<"));
                    _pos++;
                }
                continue;
            }

            // String literal '...' or "..."
            if (c == '\'' || c == '"')
            {
                char quote = c;
                _pos++;
                int start = _pos;
                while (_pos < _input.Length && _input[_pos] != quote)
                {
                    _pos++;
                }
                string strVal = _input[start.._pos];
                if (_pos < _input.Length && _input[_pos] == quote)
                {
                    _pos++;
                }
                tokens.Add(new XPathToken(XPathTokenType.String, strVal, StringValue: strVal));
                continue;
            }

            // Numeric literal
            if (char.IsDigit(c) || (c == '.' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                int start = _pos;
                while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.'))
                {
                    _pos++;
                }
                string numStr = _input[start.._pos];
                decimal numVal = decimal.Parse(numStr, CultureInfo.InvariantCulture);
                tokens.Add(new XPathToken(XPathTokenType.Number, numStr, NumberValue: numVal));
                continue;
            }

            // Identifiers / keywords / paths (e.g. lf:F60010001, @codeformejuridique, if, sum, etc.)
            if (char.IsLetter(c) || c == '_' || c == '@')
            {
                int start = _pos;
                while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_' || _input[_pos] == ':' || _input[_pos] == '@' || _input[_pos] == '/' || _input[_pos] == '-'))
                {
                    _pos++;
                }
                string text = _input[start.._pos];
                string lower = text.ToLowerInvariant();

                if (lower == "if")
                {
                    tokens.Add(new XPathToken(XPathTokenType.If, text));
                }
                else if (lower == "then")
                {
                    tokens.Add(new XPathToken(XPathTokenType.Then, text));
                }
                else if (lower == "else")
                {
                    tokens.Add(new XPathToken(XPathTokenType.Else, text));
                }
                else if (lower == "sum")
                {
                    tokens.Add(new XPathToken(XPathTokenType.Sum, text));
                }
                else if (lower == "or")
                {
                    tokens.Add(new XPathToken(XPathTokenType.Or, text));
                }
                else if (lower == "and")
                {
                    tokens.Add(new XPathToken(XPathTokenType.And, text));
                }
                else if (lower == "not")
                {
                    tokens.Add(new XPathToken(XPathTokenType.Not, text));
                }
                else if (lower == "eq")
                {
                    tokens.Add(new XPathToken(XPathTokenType.Equal, text));
                }
                else if (lower == "ne")
                {
                    tokens.Add(new XPathToken(XPathTokenType.NotEqual, text));
                }
                else if (lower == "div")
                {
                    tokens.Add(new XPathToken(XPathTokenType.Divide, text));
                }
                else
                {
                    tokens.Add(new XPathToken(XPathTokenType.Identifier, text));
                }
                continue;
            }

            // Unknown character, skip
            _pos++;
        }

        tokens.Add(new XPathToken(XPathTokenType.EndOfInput, ""));
        return tokens;
    }
}

public readonly struct XPathValue
{
    public enum ValueKind { Number, String, Boolean }

    public ValueKind Kind { get; }
    public decimal Number { get; }
    public string String { get; }
    public bool Boolean { get; }

    private XPathValue(ValueKind kind, decimal number, string str, bool boolean)
    {
        Kind = kind;
        Number = number;
        String = str;
        Boolean = boolean;
    }

    public static XPathValue FromNumber(decimal val) => new(ValueKind.Number, val, val.ToString(CultureInfo.InvariantCulture), val != 0);
    public static XPathValue FromString(string val) => new(ValueKind.String, decimal.TryParse(val, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0, val, !string.IsNullOrEmpty(val));
    public static XPathValue FromBool(bool val) => new(ValueKind.Boolean, val ? 1 : 0, val ? "true" : "false", val);

    public override string ToString() => Kind switch
    {
        ValueKind.Number => Number.ToString(CultureInfo.InvariantCulture),
        ValueKind.String => String,
        ValueKind.Boolean => Boolean ? "true" : "false",
        _ => string.Empty
    };
}

public class EvaluationContext
{
    public Dictionary<string, decimal> FieldValues { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AttributeValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    public decimal GetFieldNumber(string identifier)
    {
        var cleanId = CleanIdentifier(identifier);
        if (FieldValues.TryGetValue(cleanId, out var val))
        {
            return val;
        }
        return 0m;
    }

    public string GetAttributeString(string identifier)
    {
        var cleanId = CleanIdentifier(identifier);
        if (AttributeValues.TryGetValue(cleanId, out var val))
        {
            return val;
        }
        return string.Empty;
    }

    public static string CleanIdentifier(string raw)
    {
        var s = raw.Trim();
        if (s.StartsWith("lf:", StringComparison.OrdinalIgnoreCase))
        {
            s = s[3..];
        }
        return s;
    }
}

public class XPathAssertEvaluator
{
    private readonly List<XPathToken> _tokens;
    private int _pos;
    private readonly EvaluationContext _context;
    private string? _lastComparisonFailure;

    public XPathAssertEvaluator(string expression, EvaluationContext context)
    {
        _tokens = new XPathTokenizer(expression).Tokenize();
        _pos = 0;
        _context = context;
    }

    public static bool Evaluate(string assertExpression, EvaluationContext context, out string message)
    {
        try
        {
            var evaluator = new XPathAssertEvaluator(assertExpression, context);
            var result = evaluator.ParseStatement(out message);
            return result;
        }
        catch (Exception ex)
        {
            message = $"Erreur d'évaluation de la règle [{assertExpression}] : {ex.Message}";
            return false;
        }
    }

    private XPathToken Peek() => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private XPathToken Consume()
    {
        var t = Peek();
        if (_pos < _tokens.Count) _pos++;
        return t;
    }

    private bool Match(XPathTokenType type)
    {
        if (Peek().Type == type)
        {
            Consume();
            return true;
        }
        return false;
    }

    private bool ParseStatement(out string message)
    {
        message = string.Empty;
        var result = ParseOr();

        bool isTrue = result.Kind == XPathValue.ValueKind.Boolean ? result.Boolean : result.Number != 0;
        if (!isTrue)
        {
            message = _lastComparisonFailure ?? $"Condition non vérifiée (résultat = {result})";
        }
        return isTrue;
    }

    private static bool Compare(XPathValue left, XPathTokenType op, XPathValue right)
    {
        if (left.Kind == XPathValue.ValueKind.String || right.Kind == XPathValue.ValueKind.String)
        {
            var sLeft = left.String;
            var sRight = right.String;
            int cmp = string.Compare(sLeft, sRight, StringComparison.OrdinalIgnoreCase);
            return op switch
            {
                XPathTokenType.Equal => cmp == 0,
                XPathTokenType.NotEqual => cmp != 0,
                XPathTokenType.GreaterThan => cmp > 0,
                XPathTokenType.GreaterEqual => cmp >= 0,
                XPathTokenType.LessThan => cmp < 0,
                XPathTokenType.LessEqual => cmp <= 0,
                _ => false
            };
        }

        decimal dLeft = left.Number;
        decimal dRight = right.Number;
        const decimal tolerance = 0.0001m;

        return op switch
        {
            XPathTokenType.Equal => Math.Abs(dLeft - dRight) <= tolerance,
            XPathTokenType.NotEqual => Math.Abs(dLeft - dRight) > tolerance,
            XPathTokenType.GreaterThan => dLeft > dRight + tolerance,
            XPathTokenType.GreaterEqual => dLeft >= dRight - tolerance,
            XPathTokenType.LessThan => dLeft < dRight - tolerance,
            XPathTokenType.LessEqual => dLeft <= dRight + tolerance,
            _ => false
        };
    }

    private static string FormatFailureMessage(XPathValue left, XPathTokenType op, XPathValue right)
    {
        string opStr = op switch
        {
            XPathTokenType.Equal => "=",
            XPathTokenType.NotEqual => "!=",
            XPathTokenType.GreaterEqual => ">=",
            XPathTokenType.LessEqual => "<=",
            XPathTokenType.GreaterThan => ">",
            XPathTokenType.LessThan => "<",
            _ => op.ToString()
        };

        return $"Valeur constatée {left} {opStr} attendu {right}";
    }

    private XPathValue ParseOr()
    {
        var left = ParseAnd();
        while (Match(XPathTokenType.Or))
        {
            var right = ParseAnd();
            bool bLeft = left.Kind == XPathValue.ValueKind.Boolean ? left.Boolean : left.Number != 0;
            bool bRight = right.Kind == XPathValue.ValueKind.Boolean ? right.Boolean : right.Number != 0;
            left = XPathValue.FromBool(bLeft || bRight);
        }
        return left;
    }

    private XPathValue ParseAnd()
    {
        var left = ParseEquality();
        while (Match(XPathTokenType.And))
        {
            var right = ParseEquality();
            bool bLeft = left.Kind == XPathValue.ValueKind.Boolean ? left.Boolean : left.Number != 0;
            bool bRight = right.Kind == XPathValue.ValueKind.Boolean ? right.Boolean : right.Number != 0;
            left = XPathValue.FromBool(bLeft && bRight);
        }
        return left;
    }

    private XPathValue ParseEquality()
    {
        var left = ParseRelational();
        while (Peek().Type == XPathTokenType.Equal || Peek().Type == XPathTokenType.NotEqual)
        {
            var op = Consume();
            var right = ParseRelational();
            bool passed = Compare(left, op.Type, right);
            if (!passed)
            {
                _lastComparisonFailure = FormatFailureMessage(left, op.Type, right);
            }
            left = XPathValue.FromBool(passed);
        }
        return left;
    }

    private XPathValue ParseRelational()
    {
        var left = ParseAdditive();
        while (Peek().Type == XPathTokenType.GreaterEqual ||
               Peek().Type == XPathTokenType.LessEqual ||
               Peek().Type == XPathTokenType.GreaterThan ||
               Peek().Type == XPathTokenType.LessThan)
        {
            var op = Consume();
            var right = ParseAdditive();
            bool passed = Compare(left, op.Type, right);
            if (!passed)
            {
                _lastComparisonFailure = FormatFailureMessage(left, op.Type, right);
            }
            left = XPathValue.FromBool(passed);
        }
        return left;
    }

    private XPathValue ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Peek().Type == XPathTokenType.Plus || Peek().Type == XPathTokenType.Minus)
        {
            var op = Consume();
            var right = ParseMultiplicative();
            if (op.Type == XPathTokenType.Plus)
            {
                left = XPathValue.FromNumber(left.Number + right.Number);
            }
            else
            {
                left = XPathValue.FromNumber(left.Number - right.Number);
            }
        }
        return left;
    }

    private XPathValue ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Peek().Type == XPathTokenType.Multiply || Peek().Type == XPathTokenType.Divide)
        {
            var op = Consume();
            var right = ParseUnary();
            if (op.Type == XPathTokenType.Multiply)
            {
                left = XPathValue.FromNumber(left.Number * right.Number);
            }
            else
            {
                left = right.Number == 0 ? XPathValue.FromNumber(0) : XPathValue.FromNumber(left.Number / right.Number);
            }
        }
        return left;
    }

    private XPathValue ParseUnary()
    {
        if (Match(XPathTokenType.Minus))
        {
            var expr = ParseUnary();
            return XPathValue.FromNumber(-expr.Number);
        }
        if (Match(XPathTokenType.Plus))
        {
            return ParseUnary();
        }
        if (Match(XPathTokenType.Not))
        {
            Match(XPathTokenType.OpenParen);
            var expr = ParseOr();
            Match(XPathTokenType.CloseParen);
            bool b = expr.Kind == XPathValue.ValueKind.Boolean ? expr.Boolean : expr.Number != 0;
            return XPathValue.FromBool(!b);
        }
        return ParsePrimary();
    }

    private XPathValue ParsePrimary()
    {
        var token = Peek();

        if (token.Type == XPathTokenType.If)
        {
            Consume(); // 'if'
            Match(XPathTokenType.OpenParen);
            var condition = ParseOr();
            Match(XPathTokenType.CloseParen);

            if (!Match(XPathTokenType.Then))
            {
                throw new InvalidOperationException("Mot-clé 'then' attendu après 'if (condition)'.");
            }
            var thenBranch = ParseOr();

            if (!Match(XPathTokenType.Else))
            {
                throw new InvalidOperationException("Mot-clé 'else' attendu après 'then'.");
            }
            var elseBranch = ParseOr();

            bool condBool = condition.Kind == XPathValue.ValueKind.Boolean ? condition.Boolean : condition.Number != 0;
            return condBool ? thenBranch : elseBranch;
        }

        if (token.Type == XPathTokenType.Sum)
        {
            Consume(); // 'sum'
            Match(XPathTokenType.OpenParen); // outer sum(
            
            // XSD XPath often has sum( ((a), (b)) ) or sum( (a, b) )
            bool hasInnerSeq = Match(XPathTokenType.OpenParen);

            decimal sum = 0;
            if (Peek().Type != XPathTokenType.CloseParen)
            {
                while (true)
                {
                    var item = ParseOr();
                    sum += item.Number;

                    if (!Match(XPathTokenType.Comma))
                    {
                        break;
                    }
                }
            }

            if (hasInnerSeq)
            {
                Match(XPathTokenType.CloseParen);
            }
            Match(XPathTokenType.CloseParen);

            return XPathValue.FromNumber(sum);
        }

        if (token.Type == XPathTokenType.Number)
        {
            Consume();
            return XPathValue.FromNumber(token.NumberValue ?? 0);
        }

        if (token.Type == XPathTokenType.String)
        {
            Consume();
            return XPathValue.FromString(token.StringValue ?? string.Empty);
        }

        if (token.Type == XPathTokenType.Identifier)
        {
            Consume();
            string text = token.Text;

            if (text.Contains("/@") || text.StartsWith("@"))
            {
                string attrVal = _context.GetAttributeString(text);
                return XPathValue.FromString(attrVal);
            }

            decimal numVal = _context.GetFieldNumber(text);
            return XPathValue.FromNumber(numVal);
        }

        if (Match(XPathTokenType.OpenParen))
        {
            var expr = ParseOr();
            Match(XPathTokenType.CloseParen);
            return expr;
        }

        throw new InvalidOperationException($"Token inattendu dans l'expression : {token.Type} '{token.Text}' à la position {_pos}.");
    }
}
