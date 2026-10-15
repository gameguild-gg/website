using System.Globalization;
using System.Text.Json;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Parses and evaluates the <see cref="AbacPolicy.AttributeExpression"/> and
///     <see cref="AbacPolicy.ConditionExpression"/> payloads (issue #293).
/// </summary>
/// <remarks>
///     <para>
///         <b>AttributeExpression</b> is a JSON object mapping an attribute path to either a
///         literal (equality, mirroring the JSON condition maps) or an operator object:
///         <c>{"user.department": "IT", "user.clearanceLevel": {"$gte": 3}, "resource.tag": {"$in": ["a","b"]}}</c>.
///         Supported operators: <c>$eq</c>, <c>$ne</c>, <c>$gt</c>, <c>$gte</c>, <c>$lt</c>,
///         <c>$lte</c>, <c>$in</c>, <c>$nin</c>, <c>$exists</c>.
///     </para>
///     <para>
///         <b>ConditionExpression</b> is a boolean expression over attribute paths and
///         literals: <c>user.clearanceLevel >= resource.requiredClearance AND environment.current-hour &lt;= 20</c>.
///         Grammar (recursive descent, <c>AND</c> binds tighter than <c>OR</c>, parentheses
///         allowed): <c>or-expr := and-expr (OR and-expr)*</c>, <c>and-expr := primary (AND primary)*</c>,
///         <c>primary := '(' or-expr ')' | comparison</c>,
///         <c>comparison := operand op operand</c> with <c>op</c> one of
///         <c>&gt;= &lt;= &gt; &lt; == != in</c>. Operands are attribute paths (dotted
///         identifiers) or literals (numbers, single/double-quoted strings, true/false).
///     </para>
///     <para>
///         <b>Fail-closed evaluation:</b> a missing attribute, a type mismatch or a parse
///         error makes the enclosing comparison false; malformed payloads report errors via
///         <see cref="ValidateAttributeExpression"/> so callers can surface them, and
///         evaluation logs and returns false (the policy does not match) — mirroring the
///         established <c>EvaluateJsonConditions</c> behavior.
///     </para>
/// </remarks>
public static class AbacExpressionEvaluator
{
    /// <summary>
    ///     Evaluates an <see cref="AbacPolicy.AttributeExpression"/> JSON payload against a
    ///     request context. Every key must hold; an empty object matches trivially.
    /// </summary>
    public static bool EvaluateAttributeExpression(
        string jsonExpression,
        AbacRequestContext context,
        ICollection<string>? errors = null)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonExpression);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors?.Add("AttributeExpression must be a JSON object");
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!EvaluateAttributeCondition(property.Name, property.Value, context))
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException exception)
        {
            errors?.Add($"Malformed AttributeExpression JSON: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Structural validation of an <see cref="AbacPolicy.AttributeExpression"/> payload
    ///     without evaluating it: it must parse as a JSON object whose values are literals,
    ///     arrays of literals or operator objects with supported operators.
    /// </summary>
    public static IReadOnlyList<string> ValidateAttributeExpression(string jsonExpression)
    {
        var errors = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(jsonExpression);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add("AttributeExpression must be a JSON object");
                return errors;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                ValidateConditionValue(property.Name, property.Value, errors);
            }
        }
        catch (JsonException exception)
        {
            errors.Add($"Malformed AttributeExpression JSON: {exception.Message}");
        }

        return errors;
    }

    /// <summary>
    ///     Evaluates a <see cref="AbacPolicy.ConditionExpression"/> boolean expression
    ///     against a request context.
    /// </summary>
    public static bool EvaluateConditionExpression(
        string expression,
        AbacRequestContext context,
        ICollection<string>? errors = null)
    {
        List<AbacExpressionToken> tokens;
        try
        {
            tokens = AbacExpressionTokenizer.Tokenize(expression);
        }
        catch (FormatException exception)
        {
            errors?.Add(exception.Message);
            return false;
        }

        var parser = new AbacExpressionParser(tokens, context);
        try
        {
            var result = parser.ParseOrExpression();
            parser.ExpectEnd();
            return result;
        }
        catch (FormatException exception)
        {
            errors?.Add(exception.Message);
            return false;
        }
    }

    /// <summary>
    ///     Structural validation of a <see cref="AbacPolicy.ConditionExpression"/>:
    ///     the expression must tokenize and parse against an empty context.
    /// </summary>
    public static IReadOnlyList<string> ValidateConditionExpression(string expression)
    {
        var errors = new List<string>();
        try
        {
            var tokens = AbacExpressionTokenizer.Tokenize(expression);
            var parser = new AbacExpressionParser(
                tokens,
                new AbacRequestContext(
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>()));
            _ = parser.ParseOrExpression();
            parser.ExpectEnd();
        }
        catch (FormatException exception)
        {
            errors.Add(exception.Message);
        }

        return errors;
    }

    private static void ValidateConditionValue(string name, JsonElement value, List<string> errors)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
                    {
                        errors.Add($"'{name}': array entries must be string or number literals");
                    }
                }

                return;
            case JsonValueKind.Object:
                var anyOperator = false;
                foreach (var operatorProperty in value.EnumerateObject())
                {
                    anyOperator = true;
                    if (!IsSupportedOperator(operatorProperty.Name))
                    {
                        errors.Add($"'{name}': unsupported operator '{operatorProperty.Name}'");
                    }
                }

                if (!anyOperator)
                {
                    errors.Add($"'{name}': operator object must not be empty");
                }

                return;
            default:
                errors.Add($"'{name}': value must be a literal, an array of literals or an operator object");
                return;
        }
    }

    private static bool EvaluateAttributeCondition(string attributePath, JsonElement expected, AbacRequestContext context)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            foreach (var operatorProperty in expected.EnumerateObject())
            {
                if (!EvaluateOperator(attributePath, operatorProperty.Name, operatorProperty.Value, context))
                {
                    return false;
                }
            }

            return true;
        }

        return CompareLiteral(attributePath, "$eq", expected, context);
    }

    private static bool EvaluateOperator(
        string attributePath,
        string operatorName,
        JsonElement operand,
        AbacRequestContext context) => operatorName switch
    {
        "$eq" => CompareLiteral(attributePath, "$eq", operand, context),
        "$ne" => !CompareLiteral(attributePath, "$eq", operand, context),
        "$gt" => CompareLiteral(attributePath, "$gt", operand, context),
        "$gte" => CompareLiteral(attributePath, "$gte", operand, context),
        "$lt" => CompareLiteral(attributePath, "$lt", operand, context),
        "$lte" => CompareLiteral(attributePath, "$lte", operand, context),
        "$exists" => operand.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? (ResolveAttribute(attributePath, context) is not null) == operand.GetBoolean()
            : false,
        "$in" => operand.ValueKind == JsonValueKind.Array
            && ResolveAttribute(attributePath, context) is { } inActual
            && operand.EnumerateArray().Any(candidate => LooseEquals(inActual, candidate)),
        "$nin" => operand.ValueKind == JsonValueKind.Array
            && (ResolveAttribute(attributePath, context) is not { } ninActual
                || operand.EnumerateArray().All(candidate => !LooseEquals(ninActual, candidate))),
        _ => false
    };

    private static bool IsSupportedOperator(string operatorName) => operatorName is
        "$eq" or "$ne" or "$gt" or "$gte" or "$lt" or "$lte" or "$in" or "$nin" or "$exists";

    private static bool CompareLiteral(string attributePath, string @operator, JsonElement expected, AbacRequestContext context)
    {
        var actual = ResolveAttribute(attributePath, context);
        if (actual is null)
        {
            return false;
        }

        return @operator switch
        {
            "$eq" => LooseEquals(actual, expected),
            "$gt" => CompareOrdered(actual, expected) > 0,
            "$gte" => CompareOrdered(actual, expected) >= 0,
            "$lt" => CompareOrdered(actual, expected) < 0,
            "$lte" => CompareOrdered(actual, expected) <= 0,
            _ => false
        };
    }

    /// <summary>
    ///     Resolves a dotted attribute path against the four attribute categories. Canonical
    ///     prefixes (<c>subject.</c>/<c>user.</c>, <c>resource.</c>, <c>environment.</c>,
    ///     <c>action.</c>) select one category; a bare key is looked up in every category
    ///     (subject first, then resource, environment, action).
    /// </summary>
    internal static object? ResolveAttribute(string attributePath, AbacRequestContext context)
    {
        var separator = attributePath.IndexOf('.');
        if (separator <= 0 || separator == attributePath.Length - 1)
        {
            // Bare key: search every category.
            return Search(context.SubjectAttributes)
                ?? Search(context.ResourceAttributes)
                ?? Search(context.EnvironmentAttributes)
                ?? Search(context.ActionAttributes);

            object? Search(IReadOnlyDictionary<string, object> category)
                => category.TryGetValue(attributePath, out var value) && value is not null ? value : null;
        }

        var prefix = attributePath[..separator].ToLowerInvariant();
        var key = attributePath[(separator + 1)..];
        var category = prefix switch
        {
            "subject" or "user" => context.SubjectAttributes,
            "resource" => context.ResourceAttributes,
            "environment" or "env" => context.EnvironmentAttributes,
            "action" => context.ActionAttributes,
            // Unknown prefix: no category matches, so a typo cannot silently resolve to a
            // differently-named attribute (fail closed).
            _ => null
        };

        return category is not null && category.TryGetValue(key, out var resolved) ? resolved : null;
    }

    private static bool LooseEquals(object actual, JsonElement expected)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.String:
                return string.Equals(actual.ToString(), expected.GetString(), StringComparison.OrdinalIgnoreCase);
            case JsonValueKind.Number:
                if (expected.TryGetInt64(out var expectedLong)
                    && long.TryParse(actual.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var actualLong))
                {
                    return actualLong == expectedLong;
                }

                return decimal.TryParse(actual.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var actualDecimal)
                       && expected.TryGetDecimal(out var expectedDecimal)
                       && actualDecimal == expectedDecimal;
            case JsonValueKind.True:
                return actual is bool b && b;
            case JsonValueKind.False:
                return actual is bool b2 && !b2;
            default:
                return false;
        }
    }

    private static int CompareOrdered(object actual, JsonElement expected)
    {
        if (expected.ValueKind is not (JsonValueKind.Number or JsonValueKind.String))
        {
            return int.MinValue; // incomparable: fail closed
        }

        if (expected.ValueKind == JsonValueKind.String)
        {
            var actualString = actual.ToString();
            var expectedString = expected.GetString();
            return actualString is null || expectedString is null
                ? int.MinValue
                : string.Compare(actualString, expectedString, StringComparison.OrdinalIgnoreCase);
        }

        if (expected.TryGetInt64(out var expectedLong)
            && long.TryParse(actual.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var actualLong))
        {
            return actualLong.CompareTo(expectedLong);
        }

        if (expected.TryGetDecimal(out var expectedDecimal)
            && decimal.TryParse(actual.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var actualDecimal))
        {
            return actualDecimal.CompareTo(expectedDecimal);
        }

        if (actual is DateTime actualDate
            && DateTime.TryParse(expected.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expectedDate))
        {
            return actualDate.CompareTo(expectedDate);
        }

        return int.MinValue;
    }
}

/// <summary>
///     Operand of a <see cref="AbacPolicy.ConditionExpression"/> comparison: either an
///     attribute path resolved against the context, or a literal.
/// </summary>
internal readonly record struct AbacExpressionOperand(bool IsLiteral, string? Literal, string? AttributePath)
{
    public object? Resolve(AbacRequestContext context) => IsLiteral
        ? Literal
        : AttributePath is null ? null : AbacExpressionEvaluator.ResolveAttribute(AttributePath, context);
}

/// <summary>
///     Tokens of a <see cref="AbacPolicy.ConditionExpression"/>.
/// </summary>
internal enum AbacExpressionTokenKind
{
    Identifier,
    Number,
    String,
    And,
    Or,
    Not,
    OpenParen,
    CloseParen,
    CompareOperator,
    End
}

internal readonly record struct AbacExpressionToken(AbacExpressionTokenKind Kind, string Text)
{
    public AbacExpressionCompareOperator CompareOperator => Kind switch
    {
        AbacExpressionTokenKind.CompareOperator => ParseCompare(Text),
        _ => throw new FormatException($"Token '{Text}' is not a comparison operator")
    };

    public static AbacExpressionCompareOperator ParseCompare(string text) => text switch
    {
        ">=" => AbacExpressionCompareOperator.GreaterThanOrEqual,
        "<=" => AbacExpressionCompareOperator.LessThanOrEqual,
        ">" => AbacExpressionCompareOperator.GreaterThan,
        "<" => AbacExpressionCompareOperator.LessThan,
        "==" or "=" => AbacExpressionCompareOperator.Equal,
        "!=" => AbacExpressionCompareOperator.NotEqual,
        "in" => AbacExpressionCompareOperator.In,
        _ => throw new FormatException($"Unknown comparison operator '{text}'")
    };
}

internal enum AbacExpressionCompareOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    In
}

internal static class AbacExpressionTokenizer
{
    public static List<AbacExpressionToken> Tokenize(string expression)
    {
        var tokens = new List<AbacExpressionToken>();
        var position = 0;

        while (position < expression.Length)
        {
            var character = expression[position];

            if (char.IsWhiteSpace(character))
            {
                position++;
                continue;
            }

            switch (character)
            {
                case '(':
                    tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.OpenParen, "("));
                    position++;
                    continue;
                case ')':
                    tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.CloseParen, ")"));
                    position++;
                    continue;
                case '>' or '<':
                    if (position + 1 < expression.Length && expression[position + 1] == '=')
                    {
                        tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.CompareOperator, $"{character}="));
                        position += 2;
                    }
                    else
                    {
                        tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.CompareOperator, character.ToString()));
                        position++;
                    }

                    continue;
                case '=' when position + 1 < expression.Length && expression[position + 1] == '=':
                    tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.CompareOperator, "=="));
                    position += 2;
                    continue;
                case '=':
                    tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.CompareOperator, "="));
                    position++;
                    continue;
                case '!' when position + 1 < expression.Length && expression[position + 1] == '=':
                    tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.CompareOperator, "!="));
                    position += 2;
                    continue;
                case '"' or '\'':
                    var (stringLiteral, advancedString) = ReadQuotedString(expression, position, character);
                    tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.String, stringLiteral));
                    position += advancedString;
                    continue;
                default:
                    if (char.IsDigit(character) || (character == '-' && position + 1 < expression.Length && char.IsDigit(expression[position + 1])))
                    {
                        var (number, advancedNumber) = ReadNumber(expression, position);
                        tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.Number, number));
                        position += advancedNumber;
                        continue;
                    }

                    if (char.IsLetter(character) || character == '_' || character == '.')
                    {
                        // Attribute names may contain hyphens (environment.current-hour);
                        // there is no arithmetic, so '-' inside an identifier is unambiguous.
                        var identifier = new List<char>();
                        while (position < expression.Length
                               && (char.IsLetterOrDigit(expression[position]) || expression[position] == '_' || expression[position] == '.' || expression[position] == '-'))
                        {
                            identifier.Add(expression[position]);
                            position++;
                        }

                        var word = new string(identifier.ToArray());
                        switch (word.ToUpperInvariant())
                        {
                            case "AND":
                                tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.And, word));
                                break;
                            case "OR":
                                tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.Or, word));
                                break;
                            case "NOT":
                                tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.Not, word));
                                break;
                            default:
                                tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.Identifier, word));
                                break;
                        }

                        continue;
                    }

                    throw new FormatException($"Unexpected character '{character}' at position {position}");
            }
        }

        tokens.Add(new AbacExpressionToken(AbacExpressionTokenKind.End, string.Empty));
        return tokens;
    }

    private static (string Value, int Advanced) ReadQuotedString(string expression, int start, char quote)
    {
        var builder = new List<char>();
        var position = start + 1;
        while (position < expression.Length && expression[position] != quote)
        {
            builder.Add(expression[position]);
            position++;
        }

        if (position >= expression.Length)
        {
            throw new FormatException($"Unterminated string literal starting at position {start}");
        }

        return (new string(builder.ToArray()), position + 1 - start);
    }

    private static (string Value, int Advanced) ReadNumber(string expression, int start)
    {
        var builder = new List<char>();
        var position = start;
        while (position < expression.Length
               && (char.IsDigit(expression[position]) || expression[position] == '.' || expression[position] == '-'))
        {
            builder.Add(expression[position]);
            position++;
        }

        return (new string(builder.ToArray()), position - start);
    }
}

/// <summary>
///     Recursive-descent parser/evaluator for <see cref="AbacPolicy.ConditionExpression"/>.
/// </summary>
internal sealed class AbacExpressionParser
{
    private readonly List<AbacExpressionToken> _tokens;
    private readonly AbacRequestContext _context;
    private int _position;

    public AbacExpressionParser(List<AbacExpressionToken> tokens, AbacRequestContext context)
    {
        _tokens = tokens;
        _context = context;
    }

    public bool ParseOrExpression()
    {
        var result = ParseAndExpression();
        while (Current.Kind == AbacExpressionTokenKind.Or)
        {
            Advance();
            // Non-short-circuiting OR: both sides always parse; evaluation is side-effect free.
            var right = ParseAndExpression();
            result |= right;
        }

        return result;
    }

    private bool ParseAndExpression()
    {
        var result = ParsePrimary();
        while (Current.Kind == AbacExpressionTokenKind.And)
        {
            Advance();
            var right = ParsePrimary();
            result &= right;
        }

        return result;
    }

    private bool ParsePrimary()
    {
        if (Current.Kind == AbacExpressionTokenKind.Not)
        {
            Advance();
            return !ParsePrimary();
        }

        if (Current.Kind == AbacExpressionTokenKind.OpenParen)
        {
            Advance();
            var result = ParseOrExpression();
            Expect(AbacExpressionTokenKind.CloseParen, "')'");
            return result;
        }

        return ParseComparison();
    }

    private bool ParseComparison()
    {
        var left = ParseOperand();

        if (Current.Kind == AbacExpressionTokenKind.CompareOperator
            || (Current.Kind == AbacExpressionTokenKind.Identifier
                && Current.Text.Equals("in", StringComparison.OrdinalIgnoreCase)))
        {
            var operatorToken = Current.Kind == AbacExpressionTokenKind.CompareOperator
                ? Current.Text
                : "in";
            Advance();
            var right = ParseOperand();
            return EvaluateComparison(left, AbacExpressionToken.ParseCompare(operatorToken), right);
        }

        // A bare boolean attribute path (e.g. "environment.is-production") is truthy-checked.
        if (left.IsLiteral)
        {
            throw new FormatException("Expected a comparison operator after a literal operand");
        }

        return IsTruthy(left.Resolve(_context));
    }

    private AbacExpressionOperand ParseOperand()
    {
        var token = Current;
        switch (token.Kind)
        {
            case AbacExpressionTokenKind.String:
            case AbacExpressionTokenKind.Number:
                Advance();
                return new AbacExpressionOperand(IsLiteral: true, Literal: token.Text, AttributePath: null);
            case AbacExpressionTokenKind.Identifier
                when token.Text.Equals("true", StringComparison.OrdinalIgnoreCase)
                     || token.Text.Equals("false", StringComparison.OrdinalIgnoreCase):
                Advance();
                return new AbacExpressionOperand(IsLiteral: true, Literal: token.Text.ToLowerInvariant(), AttributePath: null);
            case AbacExpressionTokenKind.Identifier:
                Advance();
                return new AbacExpressionOperand(IsLiteral: false, Literal: null, AttributePath: token.Text);
            default:
                throw new FormatException($"Expected an operand but found '{token.Text}'");
        }
    }

    private bool EvaluateComparison(
        AbacExpressionOperand left,
        AbacExpressionCompareOperator operatorKind,
        AbacExpressionOperand right)
    {
        var leftValue = left.Resolve(_context);
        var rightValue = right.Resolve(_context);

        if (leftValue is null || rightValue is null)
        {
            return false;
        }

        switch (operatorKind)
        {
            case AbacExpressionCompareOperator.Equal:
                return LooseEqualsOperands(leftValue, rightValue);
            case AbacExpressionCompareOperator.NotEqual:
                return !LooseEqualsOperands(leftValue, rightValue);
            case AbacExpressionCompareOperator.In:
                return rightValue is IEnumerable<string> candidates
                    && leftValue.ToString() is { Length: > 0 } leftText
                    && candidates.Contains(leftText, StringComparer.OrdinalIgnoreCase);
            default:
                var comparison = CompareOperands(leftValue, rightValue);
                if (comparison == int.MinValue)
                {
                    return false;
                }

                return operatorKind switch
                {
                    AbacExpressionCompareOperator.GreaterThan => comparison > 0,
                    AbacExpressionCompareOperator.GreaterThanOrEqual => comparison >= 0,
                    AbacExpressionCompareOperator.LessThan => comparison < 0,
                    AbacExpressionCompareOperator.LessThanOrEqual => comparison <= 0,
                    _ => false
                };
        }
    }

    private static bool LooseEqualsOperands(object left, object right) =>
        string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);

    private static int CompareOperands(object left, object right)
    {
        var leftText = left.ToString();
        var rightText = right.ToString();

        if (long.TryParse(leftText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftLong)
            && long.TryParse(rightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightLong))
        {
            return leftLong.CompareTo(rightLong);
        }

        if (decimal.TryParse(leftText, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftDecimal)
            && decimal.TryParse(rightText, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightDecimal))
        {
            return leftDecimal.CompareTo(rightDecimal);
        }

        if (DateTime.TryParse(leftText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var leftDate)
            && DateTime.TryParse(rightText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        _ => true
    };

    private AbacExpressionToken Current => _tokens[Math.Min(_position, _tokens.Count - 1)];

    private void Advance() => _position++;

    private void Expect(AbacExpressionTokenKind kind, string description)
    {
        if (Current.Kind != kind)
        {
            throw new FormatException($"Expected {description}");
        }

        Advance();
    }

    public void ExpectEnd()
    {
        if (Current.Kind != AbacExpressionTokenKind.End)
        {
            throw new FormatException($"Unexpected trailing token '{Current.Text}'");
        }
    }
}
