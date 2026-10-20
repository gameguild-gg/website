using System.Text.Json.Nodes;
using FluentAssertions;
using GameGuild.Identity.Provisioning.Scim;
using GameGuild.Identity.Provisioning.Scim.Patch;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests.Patch;

/// <summary>
///     RFC 7644 §3.5.2 PATCH semantics for Users: add/replace/remove on simple paths,
/// dotted name paths, pathless object values, and the invalidPath/noTarget/mutability
/// error contract.
/// </summary>
public sealed class ScimUserPatchApplierTests
{
    private static ScimPatchOperation Operation(string op, string? path, JsonNode? value)
        => new() { Op = op, Path = path, Value = value };

    [Fact]
    public void Add_SimplePath_SetsTheValue()
    {
        var state = new ScimUserMutableState();
        var operations = new[]
        {
            Operation("add", "userName", JsonValue.Create("bjensen")),
            Operation("add", "active", JsonValue.Create(true))
        };

        ScimUserPatchApplier.Apply(state, operations);

        state.UserName.Should().Be("bjensen");
        state.Active.Should().BeTrue();
    }

    [Fact]
    public void Replace_IsCaseInsensitiveOnOp()
    {
        var state = new ScimUserMutableState { DisplayName = "Old" };

        ScimUserPatchApplier.Apply(state, [Operation("RePlAcE", "displayName", JsonValue.Create("New"))]);

        state.DisplayName.Should().Be("New");
    }

    [Fact]
    public void DisplayNamePatch_MarksTheValueExplicit()
    {
        // Captured state derives display value and name parts from the stored name, so
        // without the explicit flag a patched displayName would be recomposed from the
        // stale parts on materialization and the patch would silently not apply.
        var state = new ScimUserMutableState { DisplayName = "Barbara Jensen", GivenName = "Barbara", FamilyName = "Jensen" };

        ScimUserPatchApplier.Apply(state, [Operation("replace", "displayName", JsonValue.Create("Barbara J. Jensen"))]);

        state.DisplayName.Should().Be("Barbara J. Jensen");
        state.DisplayNameExplicit.Should().BeTrue("an explicitly patched displayName must survive recomposition from name parts");
    }

    [Fact]
    public void NameFormattedPatch_MarksTheValueExplicit()
    {
        var state = new ScimUserMutableState { DisplayName = "Barbara Jensen", GivenName = "Barbara", FamilyName = "Jensen" };

        ScimUserPatchApplier.Apply(state, [Operation("replace", "name.formatted", JsonValue.Create("Babs J"))]);

        state.DisplayName.Should().Be("Babs J");
        state.DisplayNameExplicit.Should().BeTrue();
    }

    [Fact]
    public void NamePartsPatch_DoesNotMarkDisplayNameExplicit()
    {
        var state = new ScimUserMutableState { DisplayName = "Barbara Jensen", GivenName = "Barbara", FamilyName = "Jensen" };

        ScimUserPatchApplier.Apply(state, [Operation("replace", "name.givenName", JsonValue.Create("Babs"))]);

        state.DisplayNameExplicit.Should().BeFalse("patching name parts composes the display value instead");
    }

    [Fact]
    public void PathlessObject_AppliesEveryAttribute()
    {
        var value = new JsonObject
        {
            ["userName"] = "bjensen",
            ["displayName"] = "Barbara Jensen",
            ["active"] = false,
            ["externalId"] = "ext-9"
        };

        var state = new ScimUserMutableState();
        ScimUserPatchApplier.Apply(state, [Operation("replace", null, value)]);

        state.UserName.Should().Be("bjensen");
        state.DisplayName.Should().Be("Barbara Jensen");
        state.Active.Should().BeFalse();
        state.ExternalId.Should().Be("ext-9");
    }

    [Fact]
    public void NameParts_ArePatchedIndependently()
    {
        var state = new ScimUserMutableState { GivenName = "Barbara", FamilyName = "Jensen" };

        ScimUserPatchApplier.Apply(state, [Operation("replace", "name.familyName", JsonValue.Create("Jensen-Smith"))]);

        state.FamilyName.Should().Be("Jensen-Smith");
        state.GivenName.Should().Be("Barbara");
    }

    [Fact]
    public void NameObject_MergesOnlyProvidedParts()
    {
        var value = new JsonObject { ["givenName"] = "Babs" };

        var state = new ScimUserMutableState { GivenName = "Barbara", FamilyName = "Jensen" };
        ScimUserPatchApplier.Apply(state, [Operation("add", "name", value)]);

        state.GivenName.Should().Be("Babs");
        state.FamilyName.Should().Be("Jensen", "unspecified parts are untouched");
    }

    [Fact]
    public void Emails_ArrayWithSingleEntry_SetsPrimaryEmail()
    {
        var value = new JsonArray(new JsonObject { ["value"] = "new@example.com", ["primary"] = true });

        var state = new ScimUserMutableState { PrimaryEmail = "old@example.com" };
        ScimUserPatchApplier.Apply(state, [Operation("replace", "emails", value)]);

        state.PrimaryEmail.Should().Be("new@example.com");
    }

    [Fact]
    public void Emails_ArrayWithMultipleEntries_IsRejected()
    {
        var value = new JsonArray(
            new JsonObject { ["value"] = "a@example.com" },
            new JsonObject { ["value"] = "b@example.com" });

        var act = () => ScimUserPatchApplier.Apply(new ScimUserMutableState(), [Operation("replace", "emails", value)]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidValue");
    }

    [Theory]
    [InlineData("emails[type eq \"work\"]")]
    [InlineData("emails[type eq \"work\"].value")]
    public void ValueFilterPaths_AreRejected(string path)
    {
        var act = () => ScimUserPatchApplier.Apply(
            new ScimUserMutableState(),
            [Operation("replace", path, JsonValue.Create("x@example.com"))]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidPath");
    }

    [Fact]
    public void Remove_ClearsOptionalAttributes()
    {
        var state = new ScimUserMutableState
        {
            DisplayName = "Barbara Jensen",
            GivenName = "Barbara",
            FamilyName = "Jensen",
            PhoneNumber = "+15551234"
        };

        ScimUserPatchApplier.Apply(state,
        [
            Operation("remove", "displayName", null),
            Operation("remove", "name.givenName", null),
            Operation("remove", "phoneNumbers", null)
        ]);

        state.DisplayName.Should().BeNull();
        state.GivenName.Should().BeNull();
        state.PhoneNumber.Should().BeNull();
    }

    [Theory]
    [InlineData("userName")]
    [InlineData("emails")]
    [InlineData("externalId")]
    public void Remove_RequiredAttributes_IsMutabilityError(string path)
    {
        var act = () => ScimUserPatchApplier.Apply(
            new ScimUserMutableState { UserName = "bjensen", PrimaryEmail = "b@example.com", ExternalId = "ext" },
            [Operation("remove", path, null)]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("mutability");
    }

    [Fact]
    public void Remove_WithoutPath_IsInvalidPath()
    {
        var act = () => ScimUserPatchApplier.Apply(
            new ScimUserMutableState(),
            [Operation("remove", null, null)]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidPath");
    }

    [Fact]
    public void Remove_WithValue_IsInvalidValue()
    {
        var act = () => ScimUserPatchApplier.Apply(
            new ScimUserMutableState { PhoneNumber = "+15551234" },
            [Operation("remove", "phoneNumbers", JsonValue.Create("+15551234"))]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidValue");
    }

    [Theory]
    [InlineData("nickName")]
    [InlineData("title")]
    [InlineData("enterprise.employeeNumber")]
    public void UnknownPath_IsInvalidPath(string path)
    {
        var act = () => ScimUserPatchApplier.Apply(
            new ScimUserMutableState(),
            [Operation("add", path, JsonValue.Create("x"))]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidPath");
    }

    [Fact]
    public void UnknownOp_IsInvalidValue()
    {
        var act = () => ScimUserPatchApplier.Apply(
            new ScimUserMutableState(),
            [Operation("merge", "displayName", JsonValue.Create("x"))]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidValue");
    }

    [Fact]
    public void WrongValueType_IsInvalidValue()
    {
        var act = () => ScimUserPatchApplier.Apply(
            new ScimUserMutableState(),
            [Operation("replace", "active", JsonValue.Create("yes-maybe"))]);

        act.Should().Throw<ScimException>()
            .Which.ScimType.Should().Be("invalidValue");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "displayName")]
    [InlineData(null, "name.givenName")]
    [InlineData("", null)]
    [InlineData("", "displayName")]
    [InlineData("", "name.givenName")]
    [InlineData(" ", null)]
    [InlineData(" ", "displayName")]
    [InlineData(" ", "name.givenName")]
    [InlineData("merge", null)]
    [InlineData("merge", "displayName")]
    [InlineData("merge", "name.givenName")]
    [InlineData("UpDaTe", null)]
    [InlineData("UpDaTe", "displayName")]
    [InlineData("UpDaTe", "name.givenName")]
    public void UnsupportedOperation_RejectsWithoutMutatingState(string? op, string? path)
    {
        var state = new ScimUserMutableState
        {
            UserName = "existing",
            DisplayName = "Existing",
            GivenName = "Original"
        };
        var operation = new ScimPatchOperation { Op = op, Path = path, Value = JsonValue.Create("Rejected") };

        var act = () => ScimUserPatchApplier.Apply(state, [operation]);

        act.Should().Throw<ScimException>().Which.ScimType.Should().Be("invalidValue");
        state.UserName.Should().Be("existing");
        state.DisplayName.Should().Be("Existing");
        state.GivenName.Should().Be("Original");
        state.DisplayNameExplicit.Should().BeFalse();
    }
}
