using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

var pairs = JsonSerializer.Deserialize<List<Input>>(Console.In.ReadToEnd()) ?? [];
var output = pairs.Select(pair =>
{
    var left = Parse(pair.Left);
    var right = Parse(pair.Right);
    return new { pair.Id, Equal = left.Key.SequenceEqual(right.Key), LeftErrors = left.Errors, RightErrors = right.Errors };
});
Console.WriteLine(JsonSerializer.Serialize(output));

static (string[] Key, string[] Errors) Parse(string source)
{
    var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
    var root = tree.GetRoot();
    // Preserve token kind/value, directives and disabled code. Whitespace inside
    // literals and preprocessor branches must never disappear as "formatting".
    var key = root.DescendantTokens().SelectMany(token =>
            token.LeadingTrivia.Where(Important).Select(trivia => $"leading:{trivia.ToFullString()}")
                .Append($"{token.RawKind}:{token.ValueText}")
                .Concat(token.TrailingTrivia.Where(Important).Select(trivia => $"trailing:{trivia.ToFullString()}")))
        .ToArray();
    var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
        .Select(d => d.ToString()).ToArray();
    return (key, errors);
}

static bool Important(SyntaxTrivia trivia) => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia);

record Input(string Id, string Left, string Right);
