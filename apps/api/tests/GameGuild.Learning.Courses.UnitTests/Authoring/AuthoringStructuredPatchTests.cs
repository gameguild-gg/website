using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Authoring;

public sealed class AuthoringStructuredPatchTests
{
    [Fact]
    public void Apply_LexicalTextReplacement_PreservesUnknownInteractiveNodes()
    {
        const string source = """
            {"root":{"type":"root","version":1,"children":[
              {"type":"paragraph","version":1,"children":[{"type":"text","version":1,"text":"Before","custom":"keep"}]},
              {"type":"game-embed","version":7,"gameId":"game-1","customPayload":{"difficulty":"hard"}}
            ]}}
            """;
        const string patch = """
            {"operations":[{"op":"replace","path":"/root/children/0/children/0/text","value":"After"}]}
            """;

        var result = JsonNode.Parse(AuthoringStructuredPatch.Apply(source, patch, AiProposalKind.LexicalPatch))!;

        result["root"]!["children"]![0]!["children"]![0]!["text"]!.GetValue<string>().Should().Be("After");
        result["root"]!["children"]![0]!["children"]![0]!["custom"]!.GetValue<string>().Should().Be("keep");
        result["root"]!["children"]![1]!["customPayload"]!["difficulty"]!.GetValue<string>().Should().Be("hard");
    }

    [Fact]
    public void Apply_LexicalPatchThatChangesNodeType_IsRejected()
    {
        const string source = """{"root":{"type":"root","version":1,"children":[]}}""";
        const string patch = """{"operations":[{"op":"replace","path":"/root/type","value":"paragraph"}]}""";

        var act = () => AuthoringStructuredPatch.Apply(source, patch, AiProposalKind.LexicalPatch);

        act.Should().Throw<ArgumentException>().WithMessage("*identity and type metadata*");
    }

    [Fact]
    public void Apply_LexicalPatchCannotRemoveUnknownInteractiveNode()
    {
        const string source = """{"root":{"type":"root","version":1,"children":[{"type":"game-embed","version":1,"gameId":"g"}]}}""";
        const string patch = """{"operations":[{"op":"remove","path":"/root/children/0"}]}""";

        var act = () => AuthoringStructuredPatch.Apply(source, patch, AiProposalKind.LexicalPatch);

        act.Should().Throw<ArgumentException>().WithMessage("*interactive or unknown*");
    }

    [Fact]
    public void Apply_QuizPatch_ChangesOnlyAddressedField()
    {
        const string source = """{"items":[{"id":"q1","prompt":"Before","answer":"A","extension":{"rubric":"keep"}}],"grading":{"enabled":true}}""";
        const string patch = """{"operations":[{"op":"replace","path":"/items/0/prompt","value":"After"}]}""";

        var result = JsonNode.Parse(AuthoringStructuredPatch.Apply(source, patch, AiProposalKind.QuizPatch))!;

        result["items"]![0]!["prompt"]!.GetValue<string>().Should().Be("After");
        result["items"]![0]!["extension"]!["rubric"]!.GetValue<string>().Should().Be("keep");
        result["grading"]!["enabled"]!.GetValue<bool>().Should().BeTrue();
    }
}
