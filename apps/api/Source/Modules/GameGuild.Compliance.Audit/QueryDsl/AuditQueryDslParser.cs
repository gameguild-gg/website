using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Parses audit query DSL inputs into the typed <see cref="AuditQueryDslQuery"/> model.
///     Two input forms are supported:
///     <list type="bullet">
///         <item>Structured JSON — {"filter":{"and":[{"field":"actor","op":"eq","value":"alice"}]},"sortDirection":"desc"}</item>
///         <item>Light text syntax — actor:alice AND action:granted AFTER 2026-01-01</item>
///     </list>
///     Parsing fails closed: unknown fields, unknown operators, type mismatches, empty
///     groups, and malformed input all produce a validation <see cref="Error"/> naming the problem.
/// </summary>
public interface IAuditQueryDslParser
{
    /// <summary>Parse the structured JSON form of the DSL.</summary>
    Result<AuditQueryDslQuery> ParseJson(string json);

    /// <summary>Parse the light text form of the DSL (e.g. "actor:alice AND (severity:High OR severity:Critical)").</summary>
    Result<AuditQueryDslQuery> ParseText(string text);
}

/// <inheritdoc />
public sealed class AuditQueryDslParser : IAuditQueryDslParser
{
    private const string ErrorCode = "AuditQueryDsl.Invalid";

    private static readonly Dictionary<string, AuditQueryComparison> OperatorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = AuditQueryComparison.Eq,
        ["ne"] = AuditQueryComparison.Ne,
        ["gt"] = AuditQueryComparison.Gt,
        ["gte"] = AuditQueryComparison.Gte,
        ["ge"] = AuditQueryComparison.Gte,
        ["lt"] = AuditQueryComparison.Lt,
        ["lte"] = AuditQueryComparison.Lte,
        ["le"] = AuditQueryComparison.Lte,
        ["in"] = AuditQueryComparison.In,
        ["contains"] = AuditQueryComparison.Contains,
        ["startswith"] = AuditQueryComparison.StartsWith,
        ["matches"] = AuditQueryComparison.Wildcard
    };

    public Result<AuditQueryDslQuery> ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "Query JSON is empty."));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (JsonException exception)
        {
            return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, $"Query JSON is malformed: {exception.Message}"));
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "Query JSON root must be an object."));
            }

            if (!root.TryGetProperty("filter", out var filterElement))
            {
                return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "Query JSON must contain a 'filter' property."));
            }

            AuditQueryNode? filter;
            var filterError = ParseJsonNode(filterElement, "filter", out filter);
            if (filterError is not null)
            {
                return Result.Failure<AuditQueryDslQuery>(filterError);
            }

            var sortBy = AuditQuerySortFields.Timestamp;
            if (root.TryGetProperty("sortBy", out var sortByElement))
            {
                if (sortByElement.ValueKind != JsonValueKind.String)
                {
                    return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "'sortBy' must be a string."));
                }

                sortBy = sortByElement.GetString()!;
                if (!AuditQuerySortFields.All.Contains(sortBy))
                {
                    return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, $"Unknown sortBy '{sortBy}'. Supported: {string.Join(", ", AuditQuerySortFields.All)}."));
                }
            }

            var sortDirection = "desc";
            if (root.TryGetProperty("sortDirection", out var sortDirectionElement))
            {
                if (sortDirectionElement.ValueKind != JsonValueKind.String)
                {
                    return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "'sortDirection' must be a string."));
                }

                sortDirection = sortDirectionElement.GetString()!;
                if (!string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "'sortDirection' must be 'asc' or 'desc'."));
                }
            }

            int? take = null;
            if (root.TryGetProperty("take", out var takeElement))
            {
                if (takeElement.ValueKind != JsonValueKind.Number || !takeElement.TryGetInt32(out var parsedTake) || parsedTake < 1)
                {
                    return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "'take' must be a positive number."));
                }

                take = parsedTake;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name is not ("filter" or "sortBy" or "sortDirection" or "take"))
                {
                    return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, $"Unknown query property '{property.Name}'. Supported: filter, sortBy, sortDirection, take."));
                }
            }

            return Result.Success(new AuditQueryDslQuery
            {
                Filter = filter!,
                SortBy = sortBy,
                SortDirection = sortDirection.ToLowerInvariant(),
                Take = take
            });
        }
    }

    public Result<AuditQueryDslQuery> ParseText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "Query text is empty."));
        }

        var tokenizer = new AuditQueryTextTokenizer(text);
        var tokens = tokenizer.Tokenize();
        if (tokens.Count > 512)
        {
            return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "Query text is too complex: more than 512 tokens."));
        }

        var parser = new TextParser(tokens);
        AuditQueryNode? node;
        var error = parser.ParseOrExpression(out node);
        if (error is not null)
        {
            return Result.Failure<AuditQueryDslQuery>(error);
        }

        if (parser.Position < parser.Tokens.Count)
        {
            return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, $"Unexpected token '{parser.Tokens[parser.Position].Text}' after position {parser.Position + 1}."));
        }

        if (parser.DepthExceeded)
        {
            return Result.Failure<AuditQueryDslQuery>(Error.Validation(ErrorCode, "Query nesting exceeds the maximum depth of 16."));
        }

        return Result.Success(new AuditQueryDslQuery
        {
            Filter = node!,
            SortBy = AuditQuerySortFields.Timestamp,
            SortDirection = "desc"
        });
    }

    private static Error? ParseJsonNode(JsonElement element, string path, out AuditQueryNode? node)
    {
        node = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Error.Validation(ErrorCode, $"Filter node at '{path}' must be an object.");
        }

        var negate = false;
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("negate"))
            {
                if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                {
                    return Error.Validation(ErrorCode, $"'negate' at '{path}' must be a boolean.");
                }

                negate = property.Value.GetBoolean();
            }
        }

        var hasAnd = element.TryGetProperty("and", out var andElement);
        var hasOr = element.TryGetProperty("or", out var orElement);
        if (hasAnd || hasOr)
        {
            var isAnd = hasAnd;
            var logicalElement = hasAnd ? andElement : orElement!;
            if (hasAnd && hasOr)
            {
                return Error.Validation(ErrorCode, $"Group node at '{path}' cannot declare both 'and' and 'or'.");
            }

            if (element.TryGetProperty("field", out _) || element.TryGetProperty("op", out _) || element.TryGetProperty("value", out _))
            {
                return Error.Validation(ErrorCode, $"Group node at '{path}' cannot mix logical keys with field/op/value condition keys.");
            }

            foreach (var property in element.EnumerateObject().Select(item => item.Name))
            {
                if (property is not ("and" or "or" or "negate"))
                {
                    return Error.Validation(ErrorCode, $"Unknown group key '{property}' at '{path}'. Supported: and, or, negate.");
                }
            }

            if (logicalElement.ValueKind != JsonValueKind.Array || logicalElement.GetArrayLength() == 0)
            {
                return Error.Validation(ErrorCode, $"Logical '{(isAnd ? "and" : "or")}' at '{path}' must be a non-empty array.");
            }

            if (logicalElement.GetArrayLength() > 64)
            {
                return Error.Validation(ErrorCode, $"Logical '{(isAnd ? "and" : "or")}' at '{path}' exceeds 64 children.");
            }

            var children = new List<AuditQueryNode>();
            var index = 0;
            foreach (var child in logicalElement.EnumerateArray())
            {
                var childError = ParseJsonNode(child, $"{path}[{index}]", out var childNode);
                if (childError is not null)
                {
                    return childError;
                }

                children.Add(childNode!);
                index++;
            }

            node = new AuditQueryGroup
            {
                Logic = isAnd ? AuditQueryLogicalOperator.And : AuditQueryLogicalOperator.Or,
                Children = children,
                Negate = negate
            };
            return null;
        }

        if (!element.TryGetProperty("field", out var fieldElement))
        {
            return Error.Validation(ErrorCode, $"Filter node at '{path}' must be a group ('and'/'or' array) or a condition with 'field', 'op' and 'value'.");
        }

        foreach (var property in element.EnumerateObject().Select(item => item.Name))
        {
            if (property is not ("field" or "op" or "value" or "negate"))
            {
                return Error.Validation(ErrorCode, $"Unknown condition key '{property}' at '{path}'. Supported: field, op, value, negate.");
            }
        }

        if (fieldElement.ValueKind != JsonValueKind.String)
        {
            return Error.Validation(ErrorCode, $"'field' at '{path}' must be a string.");
        }

        var fieldName = fieldElement.GetString()!;
        var spec = AuditQueryFieldRegistry.Resolve(fieldName);
        if (spec is null)
        {
            return Error.Validation(ErrorCode, $"Unknown field '{fieldName}' at '{path}'. Known fields: {AuditQueryFieldRegistry.DescribeAllFields()}.");
        }

        if (!element.TryGetProperty("op", out var opElement) || opElement.ValueKind != JsonValueKind.String)
        {
            return Error.Validation(ErrorCode, $"Condition on '{fieldName}' at '{path}' requires a string 'op'.");
        }

        var opName = opElement.GetString()!;
        if (!OperatorNames.TryGetValue(opName, out var comparison))
        {
            return Error.Validation(ErrorCode, $"Unknown operator '{opName}' at '{path}'. Known operators: {string.Join(", ", OperatorNames.Keys.OrderBy(key => key, StringComparer.Ordinal))}.");
        }

        if (!spec.AllowedOperators.Contains(comparison))
        {
            return Error.Validation(ErrorCode, $"Operator '{opName}' is not supported for field '{fieldName}'. Supported: {DescribeOperators(spec)}.");
        }

        if (!element.TryGetProperty("value", out var valueElement))
        {
            return Error.Validation(ErrorCode, $"Condition on '{fieldName}' at '{path}' requires a 'value'.");
        }

        var valueError = ValidateValue(spec, comparison, valueElement, path);
        if (valueError is not null)
        {
            return valueError;
        }

        node = new AuditQueryCondition
        {
            Field = spec.Field,
            Operator = comparison,
            Value = valueElement.Clone(),
            Negate = negate
        };
        return null;
    }

    private static string DescribeOperators(AuditQueryFieldRegistry.FieldSpec spec) =>
        string.Join(", ", spec.AllowedOperators.Select(op => OperatorNames.First(pair => pair.Value == op).Key));

    internal static Error? ValidateValue(AuditQueryFieldRegistry.FieldSpec spec, AuditQueryComparison comparison, JsonElement value, string path)
    {
        if (comparison == AuditQueryComparison.In)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                return Error.Validation(ErrorCode, $"Operator 'in' on '{spec.Names.First()}' at '{path}' requires an array value.");
            }

            if (value.GetArrayLength() == 0)
            {
                return Error.Validation(ErrorCode, $"Operator 'in' on '{spec.Names.First()}' at '{path}' requires a non-empty array.");
            }

            foreach (var item in value.EnumerateArray())
            {
                var itemError = ValidateScalarValue(spec, item, path);
                if (itemError is not null)
                {
                    return itemError;
                }
            }

            return null;
        }

        return ValidateScalarValue(spec, value, path);
    }

    private static Error? ValidateScalarValue(AuditQueryFieldRegistry.FieldSpec spec, JsonElement value, string path)
    {
        var fieldName = spec.Names.First();
        switch (spec.ValueKind)
        {
            case AuditQueryFieldValueKind.Text:
                if (value.ValueKind != JsonValueKind.String)
                {
                    return Error.Validation(ErrorCode, $"Field '{fieldName}' at '{path}' requires a string value.");
                }

                break;

            case AuditQueryFieldValueKind.Guid:
                if (value.ValueKind != JsonValueKind.String || !Guid.TryParse(value.GetString(), out _))
                {
                    return Error.Validation(ErrorCode, $"Field '{fieldName}' at '{path}' requires a GUID string value.");
                }

                break;

            case AuditQueryFieldValueKind.DateTime:
                if (value.ValueKind != JsonValueKind.String ||
                    !DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                {
                    return Error.Validation(ErrorCode, $"Field '{fieldName}' at '{path}' requires an ISO-8601 date/time string value.");
                }

                break;

            case AuditQueryFieldValueKind.RiskLevel:
                if (value.ValueKind != JsonValueKind.String || !Enum.TryParse<AuditRiskLevel>(value.GetString(), ignoreCase: true, out _))
                {
                    return Error.Validation(ErrorCode, $"Field '{fieldName}' at '{path}' requires one of: {string.Join(", ", Enum.GetNames<AuditRiskLevel>())}.");
                }

                break;

            case AuditQueryFieldValueKind.SourceType:
                if (value.ValueKind != JsonValueKind.String || !Enum.TryParse<SecurityAuditSourceType>(value.GetString(), ignoreCase: true, out _))
                {
                    return Error.Validation(ErrorCode, $"Field '{fieldName}' at '{path}' requires one of: {string.Join(", ", Enum.GetNames<SecurityAuditSourceType>())}.");
                }

                break;

            case AuditQueryFieldValueKind.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return Error.Validation(ErrorCode, $"Field '{fieldName}' at '{path}' requires a boolean value.");
                }

                break;

            default:
                return Error.Validation(ErrorCode, $"Field '{fieldName}' at '{path}' has an unsupported value kind.");
        }

        return null;
    }

    private sealed record Token(AuditQueryTokenKind Kind, string Text, int Position);

    private enum AuditQueryTokenKind
    {
        Word,
        Quoted,
        And,
        Or,
        Not,
        After,
        Before,
        LeftParen,
        RightParen
    }

    private sealed class AuditQueryTextTokenizer(string text)
    {
        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            var position = 0;
            while (position < text.Length)
            {
                var character = text[position];
                if (char.IsWhiteSpace(character))
                {
                    position++;
                    continue;
                }

                if (character == '(')
                {
                    tokens.Add(new Token(AuditQueryTokenKind.LeftParen, "(", position));
                    position++;
                    continue;
                }

                if (character == ')')
                {
                    tokens.Add(new Token(AuditQueryTokenKind.RightParen, ")", position));
                    position++;
                    continue;
                }

                if (character == '"')
                {
                    var (quoted, nextPosition) = ReadQuoted(text, position);
                    tokens.Add(new Token(AuditQueryTokenKind.Quoted, quoted, position));
                    position = nextPosition;
                    continue;
                }

                var start = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('(' or ')' or '"'))
                {
                    position++;
                }

                var word = text[start..position];
                tokens.Add(new Token(Classify(word), word, start));
            }

            return tokens;
        }

        private static (string Value, int NextPosition) ReadQuoted(string text, int position)
        {
            var builder = new StringBuilder();
            var index = position + 1;
            while (index < text.Length)
            {
                var character = text[index];
                if (character == '\\' && index + 1 < text.Length && text[index + 1] is ('"' or '\\'))
                {
                    builder.Append(text[index + 1]);
                    index += 2;
                    continue;
                }

                if (character == '"')
                {
                    return (builder.ToString(), index + 1);
                }

                builder.Append(character);
                index++;
            }

            return (builder.ToString(), index);
        }

        private static AuditQueryTokenKind Classify(string word) => word.ToLowerInvariant() switch
        {
            "and" => AuditQueryTokenKind.And,
            "or" => AuditQueryTokenKind.Or,
            "not" => AuditQueryTokenKind.Not,
            "after" => AuditQueryTokenKind.After,
            "before" => AuditQueryTokenKind.Before,
            _ => AuditQueryTokenKind.Word
        };
    }

    private sealed class TextParser(List<Token> tokens)
    {
        private const int MaxDepth = 16;

        public IReadOnlyList<Token> Tokens { get; } = tokens;

        public int Position { get; private set; }

        public bool DepthExceeded { get; private set; }

        private int Depth { get; set; }

        public Error? ParseOrExpression(out AuditQueryNode? node)
        {
            node = null;
            var andError = ParseAndExpression(out var left);
            if (andError is not null)
            {
                return andError;
            }

            var terms = new List<AuditQueryNode> { left! };
            while (Position < Tokens.Count && Tokens[Position].Kind == AuditQueryTokenKind.Or)
            {
                Position++;
                var nextError = ParseAndExpression(out var next);
                if (nextError is not null)
                {
                    return nextError;
                }

                terms.Add(next!);
            }

            node = terms.Count == 1 ? terms[0] : new AuditQueryGroup { Logic = AuditQueryLogicalOperator.Or, Children = terms };
            return null;
        }

        private Error? ParseAndExpression(out AuditQueryNode? node)
        {
            node = null;
            var unaryError = ParseUnary(out var left);
            if (unaryError is not null)
            {
                return unaryError;
            }

            var terms = new List<AuditQueryNode> { left! };
            while (Position < Tokens.Count && Tokens[Position].Kind == AuditQueryTokenKind.And)
            {
                Position++;
                var nextError = ParseUnary(out var next);
                if (nextError is not null)
                {
                    return nextError;
                }

                terms.Add(next!);
            }

            node = terms.Count == 1 ? terms[0] : new AuditQueryGroup { Logic = AuditQueryLogicalOperator.And, Children = terms };
            return null;
        }

        private Error? ParseUnary(out AuditQueryNode? node)
        {
            node = null;
            if (Position < Tokens.Count && Tokens[Position].Kind == AuditQueryTokenKind.Not)
            {
                Position++;
                var innerError = ParseUnary(out var inner);
                if (innerError is not null)
                {
                    return innerError;
                }

                node = inner switch
                {
                    AuditQueryGroup group => new AuditQueryGroup { Logic = group.Logic, Children = group.Children, Negate = !group.Negate },
                    AuditQueryCondition condition => new AuditQueryCondition { Field = condition.Field, Operator = condition.Operator, Value = condition.Value, Negate = !condition.Negate },
                    _ => null
                };
                return null;
            }

            return ParsePrimary(out node);
        }

        private Error? ParsePrimary(out AuditQueryNode? node)
        {
            node = null;
            if (Position >= Tokens.Count)
            {
                return Error.Validation(ErrorCode, "Query ended where a condition was expected.");
            }

            var token = Tokens[Position];
            if (token.Kind == AuditQueryTokenKind.LeftParen)
            {
                if (++Depth > MaxDepth)
                {
                    DepthExceeded = true;
                    return Error.Validation(ErrorCode, "Query nesting exceeds the maximum depth of 16.");
                }

                Position++;
                var innerError = ParseOrExpression(out var inner);
                Depth--;
                if (innerError is not null)
                {
                    return innerError;
                }

                if (Position >= Tokens.Count || Tokens[Position].Kind != AuditQueryTokenKind.RightParen)
                {
                    return Error.Validation(ErrorCode, $"Missing closing parenthesis for '(' at position {token.Position + 1}.");
                }

                Position++;
                node = inner;
                return null;
            }

            if (token.Kind == AuditQueryTokenKind.After || token.Kind == AuditQueryTokenKind.Before)
            {
                Position++;
                var dateError = ParseDateValue(token, out var dateNode);
                if (dateError is not null)
                {
                    return dateError;
                }

                node = dateNode;
                return null;
            }

            if (token.Kind == AuditQueryTokenKind.RightParen)
            {
                return Error.Validation(ErrorCode, $"Unexpected ')' at position {token.Position + 1}.");
            }

            if (token.Kind is AuditQueryTokenKind.And or AuditQueryTokenKind.Or or AuditQueryTokenKind.Not)
            {
                return Error.Validation(ErrorCode, $"Unexpected keyword '{token.Text}' at position {token.Position + 1}; a condition was expected.");
            }

            return ParseCondition(out node);
        }

        private Error? ParseDateValue(Token keyword, out AuditQueryNode? node)
        {
            node = null;
            if (Position >= Tokens.Count)
            {
                return Error.Validation(ErrorCode, $"Keyword '{keyword.Text}' at position {keyword.Position + 1} must be followed by a date value.");
            }

            var valueToken = Tokens[Position];
            var value = valueToken.Kind == AuditQueryTokenKind.Quoted ? $"\"{valueToken.Text}\"" : valueToken.Text;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            {
                return Error.Validation(ErrorCode, $"'{keyword.Text}' at position {keyword.Position + 1} requires an ISO-8601 date value, got '{valueToken.Text}'.");
            }

            Position++;
            var effectiveComparison = keyword.Kind == AuditQueryTokenKind.Before ? AuditQueryComparison.Lte : AuditQueryComparison.Gte;
            node = CreateCondition(
                AuditQueryFieldRegistry.Get(AuditQueryField.Timestamp),
                effectiveComparison,
                JsonSerializer.SerializeToElement(timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
            return null;
        }

        private Error? ParseCondition(out AuditQueryNode? node)
        {
            node = null;
            var fieldToken = Tokens[Position];
            var separatorIndex = fieldToken.Text.IndexOf(':');
            if (separatorIndex <= 0)
            {
                return Error.Validation(ErrorCode, $"Expected 'field:value' condition at position {fieldToken.Position + 1}, got '{fieldToken.Text}'.");
            }

            var fieldName = fieldToken.Text[..separatorIndex];
            var remainder = fieldToken.Text[(separatorIndex + 1)..];
            var spec = AuditQueryFieldRegistry.Resolve(fieldName);
            if (spec is null)
            {
                return Error.Validation(ErrorCode, $"Unknown field '{fieldName}' at position {fieldToken.Position + 1}. Known fields: {AuditQueryFieldRegistry.DescribeAllFields()}.");
            }

            AuditQueryComparison comparison;
            string rawValue;
            if (remainder.Length > 0 && remainder[0] == '[' && remainder.EndsWith("]", StringComparison.Ordinal))
            {
                comparison = AuditQueryComparison.In;
                rawValue = remainder;
            }
            else
            {
                (comparison, rawValue) = remainder switch
                {
                ['~', .. var rest] => (AuditQueryComparison.Contains, rest),
                ['!', '=', .. var rest] => (AuditQueryComparison.Ne, rest),
                ['!', .. var rest] => (AuditQueryComparison.Ne, rest),
                ['>', '=', .. var rest] => (AuditQueryComparison.Gte, rest),
                ['>', .. var rest] => (AuditQueryComparison.Gt, rest),
                ['<', '=', .. var rest] => (AuditQueryComparison.Lte, rest),
                ['<', .. var rest] => (AuditQueryComparison.Lt, rest),
                ['^', .. var rest] => (AuditQueryComparison.StartsWith, rest),
                    _ => (AuditQueryComparison.Eq, remainder)
                };
            }

            if (comparison == AuditQueryComparison.In)
            {
                var inner = rawValue[1..^1];
                var items = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (items.Length == 0)
                {
                    return Error.Validation(ErrorCode, $"Operator '[...]' (in) on '{fieldName}' requires at least one value.");
                }

                if (!spec.AllowedOperators.Contains(comparison))
                {
                    return Error.Validation(ErrorCode, $"Operator 'in' is not supported for field '{fieldName}'. Supported: {DescribeOperators(spec)}.");
                }

                var values = new List<JsonElement>();
                foreach (var item in items)
                {
                    var element = CoerceScalarToJson(spec, item, fieldToken, out var coerceError);
                    if (coerceError is not null)
                    {
                        return coerceError;
                    }

                    values.Add(element);
                }

                Position++;
                node = CreateCondition(spec, comparison, JsonSerializer.SerializeToElement(values));
                return null;
            }

            if (string.IsNullOrEmpty(rawValue) && fieldToken.Kind != AuditQueryTokenKind.Quoted)
            {
                // The value may live in the next token when written as "field:" "quoted value".
                if (Position + 1 < Tokens.Count && Tokens[Position + 1].Kind == AuditQueryTokenKind.Quoted)
                {
                    Position++;
                    rawValue = Tokens[Position].Text;
                }
            }

            if (rawValue.Length == 0)
            {
                return Error.Validation(ErrorCode, $"Condition on '{fieldName}' at position {fieldToken.Position + 1} is missing a value.");
            }

            if (!spec.AllowedOperators.Contains(comparison))
            {
                var operatorName = comparison switch
                {
                    AuditQueryComparison.Ne => "ne (!)",
                    AuditQueryComparison.Gt => "gt (>)",
                    AuditQueryComparison.Gte => "gte (>=)",
                    AuditQueryComparison.Lt => "lt (<)",
                    AuditQueryComparison.Lte => "lte (<=)",
                    AuditQueryComparison.Contains => "contains (~)",
                    AuditQueryComparison.StartsWith => "startswith (^)",
                    _ => comparison.ToString().ToLowerInvariant()
                };
                return Error.Validation(ErrorCode, $"Operator '{operatorName}' is not supported for field '{fieldName}'. Supported: {DescribeOperators(spec)}.");
            }

            var coerced = CoerceScalarToJson(spec, rawValue, fieldToken, out var error);
            if (error is not null)
            {
                return error;
            }

            Position++;
            node = CreateCondition(spec, comparison, coerced);
            return null;
        }

        private static JsonElement CoerceScalarToJson(AuditQueryFieldRegistry.FieldSpec spec, string rawValue, Token token, out Error? error)
        {
            error = null;
            switch (spec.ValueKind)
            {
                case AuditQueryFieldValueKind.Boolean:
                    if (bool.TryParse(rawValue, out var boolean))
                    {
                        return JsonSerializer.SerializeToElement(boolean);
                    }

                    error = Error.Validation(ErrorCode, $"Field '{spec.Names.First()}' at position {token.Position + 1} requires true or false, got '{rawValue}'.");
                    return default;

                case AuditQueryFieldValueKind.Guid:
                    if (Guid.TryParse(rawValue, out var guid))
                    {
                        return JsonSerializer.SerializeToElement(guid.ToString());
                    }

                    error = Error.Validation(ErrorCode, $"Field '{spec.Names.First()}' at position {token.Position + 1} requires a GUID value, got '{rawValue}'.");
                    return default;

                case AuditQueryFieldValueKind.DateTime:
                    if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
                    {
                        return JsonSerializer.SerializeToElement(timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    }

                    error = Error.Validation(ErrorCode, $"Field '{spec.Names.First()}' at position {token.Position + 1} requires an ISO-8601 date value, got '{rawValue}'.");
                    return default;

                case AuditQueryFieldValueKind.RiskLevel:
                    if (Enum.TryParse<AuditRiskLevel>(rawValue, ignoreCase: true, out var risk))
                    {
                        return JsonSerializer.SerializeToElement(risk.ToString());
                    }

                    error = Error.Validation(ErrorCode, $"Field '{spec.Names.First()}' at position {token.Position + 1} requires one of {string.Join(", ", Enum.GetNames<AuditRiskLevel>())}, got '{rawValue}'.");
                    return default;

                case AuditQueryFieldValueKind.SourceType:
                    if (Enum.TryParse<SecurityAuditSourceType>(rawValue, ignoreCase: true, out var source))
                    {
                        return JsonSerializer.SerializeToElement(source.ToString());
                    }

                    error = Error.Validation(ErrorCode, $"Field '{spec.Names.First()}' at position {token.Position + 1} requires one of {string.Join(", ", Enum.GetNames<SecurityAuditSourceType>())}, got '{rawValue}'.");
                    return default;

                default:
                    return JsonSerializer.SerializeToElement(rawValue);
            }
        }

        private static AuditQueryCondition CreateCondition(AuditQueryFieldRegistry.FieldSpec spec, AuditQueryComparison comparison, JsonElement value) =>
            new()
            {
                Field = spec.Field,
                Operator = comparison,
                Value = value
            };
    }
}
