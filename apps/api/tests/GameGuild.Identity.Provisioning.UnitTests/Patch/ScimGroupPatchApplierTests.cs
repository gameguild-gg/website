using System.Text.Json.Nodes;
using FluentAssertions;
using GameGuild.Identity.Provisioning.Scim;
using GameGuild.Identity.Provisioning.Scim.Patch;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests.Patch;

/// <summary>
///     RFC 7644 §3.5.2 PATCH semantics for Groups: displayName/externalId scalars and
/// the members paths used by provisioning engines.
/// </summary>
public sealed class ScimGroupPatchApplierTests
{
    private static ScimPatchOperation Operation(string op, string? path, JsonNode? value)
        => new() { Op = op, Path = path, Value = value };

    private static JsonNode Member(Guid userId)
        => new JsonObject { ["value"] = userId.ToString() };

    [Fact]
    public void AddMembers_AppendsWithoutDuplicates()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var state = new ScimGroupMutableState();
        state.MemberUserIds.Add(first.ToString());

        ScimGroupPatchApplier.Apply(state,
        [
            Operation("add", "members", new JsonArray(Member(first), Member(second)))
        ]);

        state.MemberUserIds.Should().BeEquivalentTo([first.ToString(), second.ToString()]);
    }

    [Fact]
    public void ReplaceMembers_ReplacesTheWholeList()
    {
        var keep = Guid.NewGuid();
        var drop = Guid.NewGuid();
        var add = Guid.NewGuid();
        var state = new ScimGroupMutableState();
        state.MemberUserIds.AddRange([keep.ToString(), drop.ToString()]);

        ScimGroupPatchApplier.Apply(state, [Operation("replace", "members", new JsonArray(Member(add)))]);

        state.MemberUserIds.Should().BeEquivalentTo([add.ToString()]);
    }

    [Fact]
    public void RemoveMembersWithValueFilter_RemovesOnlyThatMember()
    {
        var keep = Guid.NewGuid();
        var drop = Guid.NewGuid();
        var state = new ScimGroupMutableState();
        state.MemberUserIds.AddRange([keep.ToString(), drop.ToString()]);

        ScimGroupPatchApplier.Apply(state, [Operation("remove", $"members[value eq \"{drop}\"]", null)]);

        state.MemberUserIds.Should().BeEquivalentTo([keep.ToString()]);
    }

    [Fact]
    public void RemoveMembersWithoutFilter_ClearsAllMembers()
    {
        var state = new ScimGroupMutableState();
        state.MemberUserIds.AddRange([Guid.NewGuid().ToString(), Guid.NewGuid().ToString()]);

        ScimGroupPatchApplier.Apply(state, [Operation("remove", "members", null)]);

        state.MemberUserIds.Should().BeEmpty();
    }

    [Fact]
    public void ValueFilterOnNonValueAttribute_IsInvalidPath()
    {
        var act = () => ScimGroupPatchApplier.Apply(
            new ScimGroupMutableState(),
            [Operation("remove", "members[display eq \"x\"]", null)]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidPath");
    }

    [Fact]
    public void PathlessObject_PatchesDisplayNameAndMembers()
    {
        var member = Guid.NewGuid();
        var value = new JsonObject
        {
            ["displayName"] = "Engineering",
            ["members"] = new JsonArray(Member(member))
        };

        var state = new ScimGroupMutableState { DisplayName = "Old" };
        ScimGroupPatchApplier.Apply(state, [Operation("replace", null, value)]);

        state.DisplayName.Should().Be("Engineering");
        state.MemberUserIds.Should().BeEquivalentTo([member.ToString()]);
    }

    [Fact]
    public void DisplayAndExternalId_ScalarsBehaveLikeUserScalars()
    {
        var state = new ScimGroupMutableState { DisplayName = "Eng", ExternalId = "grp-1" };

        ScimGroupPatchApplier.Apply(state,
        [
            Operation("replace", "displayName", JsonValue.Create("Engineering")),
            Operation("replace", "externalId", JsonValue.Create("grp-2"))
        ]);

        state.DisplayName.Should().Be("Engineering");
        state.ExternalId.Should().Be("grp-2");
    }

    [Fact]
    public void RemoveDisplayName_IsMutabilityError()
    {
        var act = () => ScimGroupPatchApplier.Apply(
            new ScimGroupMutableState { DisplayName = "Eng" },
            [Operation("remove", "displayName", null)]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("mutability");
    }

    [Fact]
    public void MemberEntryWithoutValue_IsInvalidValue()
    {
        var value = new JsonArray(new JsonObject { ["display"] = "someone" });

        var act = () => ScimGroupPatchApplier.Apply(
            new ScimGroupMutableState(),
            [Operation("add", "members", value)]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidValue");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "displayName")]
    [InlineData(null, "members")]
    [InlineData("", null)]
    [InlineData("", "displayName")]
    [InlineData("", "members")]
    [InlineData(" ", null)]
    [InlineData(" ", "displayName")]
    [InlineData(" ", "members")]
    [InlineData("merge", null)]
    [InlineData("merge", "displayName")]
    [InlineData("merge", "members")]
    [InlineData("UpDaTe", null)]
    [InlineData("UpDaTe", "displayName")]
    [InlineData("UpDaTe", "members")]
    public void UnsupportedOperation_RejectsWithoutMutatingState(string? op, string? path)
    {
        var member = Guid.NewGuid().ToString();
        var state = new ScimGroupMutableState { DisplayName = "Existing", ExternalId = "original" };
        state.MemberUserIds.Add(member);
        var operation = new ScimPatchOperation { Op = op, Path = path, Value = JsonValue.Create("Rejected") };

        var act = () => ScimGroupPatchApplier.Apply(state, [operation]);

        act.Should().Throw<ScimException>().Which.ScimType.Should().Be("invalidValue");
        state.DisplayName.Should().Be("Existing");
        state.ExternalId.Should().Be("original");
        state.MemberUserIds.Should().BeEquivalentTo([member]);
    }
}
