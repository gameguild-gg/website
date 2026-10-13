namespace GameGuild.Identity.Provisioning.Scim.Filtering;

/// <summary>
///     Recursive-descent parser for the supported SCIM filter subset. Throws
///     <see cref="ScimException"/> with <c>invalidFilter</c> for malformed filters and
///     for operators this provider does not implement.
/// </summary>
public static class ScimFilterParser
{
    public static ScimFilter Parse(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            throw ScimException.InvalidFilter("The filter expression must not be empty.");
        }

        var tokens = Tokenize(filter);
        var reader = new TokenReader(tokens);
        var parsed = reader.ParseOrExpression();
        if (!reader.IsAtEnd)
        {
            throw ScimException.InvalidFilter($"Unexpected token '{reader.Peek().Text}' in the filter expression.");
        }

        return parsed;
    }

    internal static IReadOnlyList<FilterToken> Tokenize(string filter)
    {
        var tokens = new List<FilterToken>();
        var position = 0;
        while (position < filter.Length)
        {
            var character = filter[position];
            if (char.IsWhiteSpace(character))
            {
                position++;
                continue;
            }

            if (character == '(')
            {
                tokens.Add(new FilterToken(FilterTokenKind.OpenParen, "(", position));
                position++;
                continue;
            }

            if (character == ')')
            {
                tokens.Add(new FilterToken(FilterTokenKind.CloseParen, ")", position));
                position++;
                continue;
            }

            if (character == '"')
            {
                var closing = filter.IndexOf('"', position + 1);
                if (closing < 0)
                {
                    throw ScimException.InvalidFilter("Unterminated string literal in the filter expression.");
                }

                tokens.Add(new FilterToken(FilterTokenKind.String, filter[(position + 1)..closing], position));
                position = closing + 1;
                continue;
            }

            if (character == '[')
            {
                throw ScimException.InvalidFilter(
                    "Grouping filters over sub-attributes (for example emails[type eq \"work\"]) are not supported by this service provider.");
            }

            var wordEnd = position;
            while (wordEnd < filter.Length
                   && !char.IsWhiteSpace(filter[wordEnd])
                   && filter[wordEnd] is not ('(' or ')' or '[' or '"'))
            {
                wordEnd++;
            }

            var word = filter[position..wordEnd];
            var kind = word.ToLowerInvariant() switch
            {
                "and" => FilterTokenKind.And,
                "or" => FilterTokenKind.Or,
                "not" => FilterTokenKind.Not,
                "eq" => FilterTokenKind.Eq,
                "ne" => FilterTokenKind.Ne,
                "co" => FilterTokenKind.Co,
                "sw" => FilterTokenKind.Sw,
                "ew" => FilterTokenKind.Ew,
                "pr" => FilterTokenKind.Pr,
                "gt" or "lt" or "ge" or "le" => throw ScimException.InvalidFilter(
                    $"The '{word}' comparison operator is not supported by this service provider."),
                _ => FilterTokenKind.Word
            };

            tokens.Add(new FilterToken(kind, word, position));
            position = wordEnd;
        }

        return tokens;
    }

    private sealed class TokenReader(IReadOnlyList<FilterToken> tokens)
    {
        private int _index;

        public bool IsAtEnd => _index >= tokens.Count;

        public FilterToken Peek() => tokens[Math.Min(_index, tokens.Count - 1)];

        private FilterToken Next()
        {
            var token = tokens[_index];
            _index++;
            return token;
        }

        private bool TryAccept(FilterTokenKind kind)
        {
            if (!IsAtEnd && tokens[_index].Kind == kind)
            {
                _index++;
                return true;
            }

            return false;
        }

        public ScimFilter ParseOrExpression()
        {
            var left = ParseAndExpression();
            while (TryAccept(FilterTokenKind.Or))
            {
                var right = ParseAndExpression();
                left = new ScimFilter.Or(left, right);
            }

            return left;
        }

        private ScimFilter ParseAndExpression()
        {
            var left = ParseUnaryExpression();
            while (TryAccept(FilterTokenKind.And))
            {
                var right = ParseUnaryExpression();
                left = new ScimFilter.And(left, right);
            }

            return left;
        }

        private ScimFilter ParseUnaryExpression()
        {
            if (TryAccept(FilterTokenKind.Not))
            {
                return new ScimFilter.Not(ParsePrimaryExpression());
            }

            return ParsePrimaryExpression();
        }

        private ScimFilter ParsePrimaryExpression()
        {
            if (TryAccept(FilterTokenKind.OpenParen))
            {
                var inner = ParseOrExpression();
                if (!TryAccept(FilterTokenKind.CloseParen))
                {
                    throw ScimException.InvalidFilter("A closing parenthesis is missing in the filter expression.");
                }

                return inner;
            }

            var attribute = Next();
            if (attribute.Kind is not (FilterTokenKind.Word or FilterTokenKind.String))
            {
                throw ScimException.InvalidFilter($"Expected an attribute path but found '{attribute.Text}'.");
            }

            if (attribute.Kind == FilterTokenKind.String)
            {
                throw ScimException.InvalidFilter("Attribute paths must not be quoted.");
            }

            if (IsAtEnd)
            {
                throw ScimException.InvalidFilter("The filter expression ended after the attribute path.");
            }

            var @operator = Next();
            switch (@operator.Kind)
            {
                case FilterTokenKind.Pr:
                    return new ScimFilter.Compare(attribute.Text, ScimFilterOperator.Pr, null);
                case FilterTokenKind.Eq:
                case FilterTokenKind.Ne:
                case FilterTokenKind.Co:
                case FilterTokenKind.Sw:
                case FilterTokenKind.Ew:
                    if (IsAtEnd)
                    {
                        throw ScimException.InvalidFilter($"The '{@operator.Text}' comparison is missing its value.");
                    }

                    var value = Next();
                    if (value.Kind is not (FilterTokenKind.String or FilterTokenKind.Word))
                    {
                        throw ScimException.InvalidFilter($"The '{@operator.Text}' comparison value must be a literal.");
                    }

                    return new ScimFilter.Compare(
                        attribute.Text,
                        KindToOperator(@operator.Kind),
                        value.Text);
                default:
                    throw ScimException.InvalidFilter(
                        $"Unsupported operator '{@operator.Text}'. Supported operators: eq, ne, co, sw, ew, pr, and, or, not.");
            }
        }
    }

    private static ScimFilterOperator KindToOperator(FilterTokenKind kind) => kind switch
    {
        FilterTokenKind.Eq => ScimFilterOperator.Eq,
        FilterTokenKind.Ne => ScimFilterOperator.Ne,
        FilterTokenKind.Co => ScimFilterOperator.Co,
        FilterTokenKind.Sw => ScimFilterOperator.Sw,
        FilterTokenKind.Ew => ScimFilterOperator.Ew,
        _ => throw ScimException.InvalidFilter("Unsupported comparison operator.")
    };
}

internal enum FilterTokenKind
{
    Word,
    String,
    And,
    Or,
    Not,
    Eq,
    Ne,
    Co,
    Sw,
    Ew,
    Pr,
    OpenParen,
    CloseParen
}

internal sealed record FilterToken(FilterTokenKind Kind, string Text, int Position);
