using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class MfaSubjectRequirementPolicyTests
{
    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("Admin")]
    [InlineData("TenantAdmin")]
    [InlineData("Owner")]
    [InlineData("oWnEr")]
    public async Task SubjectElevatedRoleRequiresMfaWithoutAnAuthenticatedCaller(string role)
    {
        var fixture = new PolicyFixture();
        fixture.SetMemberships(fixture.Membership with { Role = role });
        var result = await fixture.EvaluateAsync();
        Assert.True(result.RequiresMfa);
        Assert.Equal(fixture.SubjectId, result.SubjectId);
        Assert.Equal(fixture.TenantId, result.TenantId);
        fixture.VerifyExactSubjectQuery();
    }

    [Fact]
    public async Task OrdinarySubjectDoesNotAcquireAnotherSubjectsAdminPolicy()
    {
        var fixture = new PolicyFixture();
        var otherSubjectId = Guid.NewGuid();
        fixture.Sender.Setup(sender => sender.Send(It.Is<GetUserMembershipsQuery>(q => q.UserId == otherSubjectId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse { Memberships = [fixture.Membership with { Role = "Owner" }] });
        var result = await fixture.EvaluateAsync();
        Assert.False(result.RequiresMfa);
        fixture.Sender.Verify(sender => sender.Send(It.Is<GetUserMembershipsQuery>(q => q.UserId == otherSubjectId), It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyExactSubjectQuery();
    }

    [Fact]
    public async Task SelectedTenantRolesFollowTheIssuersAuthoritativeSelection()
    {
        var fixture = new PolicyFixture();
        var secondary = fixture.Membership with { TenantId = Guid.NewGuid(), TenantIsDefault = false, Role = "Owner" };
        fixture.SetMemberships(fixture.Membership, secondary);
        var defaultResult = await fixture.EvaluateAsync();
        var ownerResult = await fixture.EvaluateAsync(secondary.TenantId);
        Assert.False(defaultResult.RequiresMfa);
        Assert.Equal(fixture.TenantId, defaultResult.TenantId);
        Assert.True(ownerResult.RequiresMfa);
        Assert.Equal(secondary.TenantId, ownerResult.TenantId);
        Assert.NotEqual(defaultResult.PolicyFingerprint, ownerResult.PolicyFingerprint);
    }

    [Fact]
    public async Task ReservedSystemAdminFromNonDefaultTenantIsNotTrusted()
    {
        var fixture = new PolicyFixture();
        var secondary = fixture.Membership with { TenantId = Guid.NewGuid(), TenantIsDefault = false, Role = "SystemAdmin" };
        fixture.SetMemberships(fixture.Membership, secondary);
        var result = await fixture.EvaluateAsync(secondary.TenantId);
        Assert.False(result.RequiresMfa);
        Assert.DoesNotContain("SYSTEMADMIN", result.Roles);
    }

    [Fact]
    public async Task DefaultTenantSystemAdminPolicySurvivesSelectingAnOrdinaryTenant()
    {
        var fixture = new PolicyFixture();
        var secondary = fixture.Membership with { TenantId = Guid.NewGuid(), TenantIsDefault = false };
        fixture.SetMemberships(fixture.Membership with { Role = "SystemAdmin" }, secondary);
        var result = await fixture.EvaluateAsync(secondary.TenantId);
        Assert.True(result.RequiresMfa);
        Assert.Contains("SYSTEMADMIN", result.Roles);
    }

    [Theory]
    [InlineData("inactive-member")]
    [InlineData("inactive-tenant")]
    [InlineData("left")]
    [InlineData("pending")]
    [InlineData("cancelled")]
    [InlineData("empty-tenant")]
    [InlineData("foreign-tenant")]
    [InlineData("missing")]
    public async Task UnavailableMembershipCannotProducePolicyEvidence(string mode)
    {
        var fixture = new PolicyFixture();
        var membership = mode switch
        {
            "inactive-member" => fixture.Membership with { IsActive = false },
            "inactive-tenant" => fixture.Membership with { TenantIsActive = false },
            "left" => fixture.Membership with { LeftAt = DateTime.UtcNow.AddDays(-1) },
            "pending" => fixture.Membership with { InviteStatus = "Pending" },
            "cancelled" => fixture.Membership with { InviteStatus = "Cancelled" },
            "empty-tenant" => fixture.Membership with { TenantId = Guid.Empty },
            _ => fixture.Membership
        };
        fixture.SetMemberships(mode == "missing" ? [] : [membership]);
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => fixture.EvaluateAsync(mode == "foreign-tenant" ? Guid.NewGuid() : null));
    }

    [Fact]
    public async Task InactiveElevatedMembershipDoesNotGrantARequiredRoleToAnActiveMember()
    {
        var fixture = new PolicyFixture();
        fixture.SetMemberships(fixture.Membership, fixture.Membership with { TenantId = Guid.NewGuid(), Role = "Owner", IsActive = false });
        var result = await fixture.EvaluateAsync();
        Assert.False(result.RequiresMfa);
        Assert.DoesNotContain("OWNER", result.Roles);
    }

    [Fact]
    public async Task AcceptedInviteCanSupplyTheSubjectsCurrentRole()
    {
        var fixture = new PolicyFixture();
        fixture.SetMemberships(fixture.Membership with { Role = "TenantAdmin", InviteStatus = "aCcEpTeD" });
        Assert.True((await fixture.EvaluateAsync()).RequiresMfa);
    }

    [Fact]
    public async Task DefaultRequirementAppliesToOrdinarySubject()
    {
        var fixture = new PolicyFixture(new MfaOptions { RequireMfaByDefault = true });
        Assert.True((await fixture.EvaluateAsync()).RequiresMfa);
    }

    [Fact]
    public async Task DisabledMfaStillCannotAuthorizeForeignTenant()
    {
        var fixture = new PolicyFixture(new MfaOptions { Enabled = false });
        fixture.SetMemberships(fixture.Membership with { Role = "Owner" });
        Assert.False((await fixture.EvaluateAsync()).RequiresMfa);
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => fixture.EvaluateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ContradictoryOrInvalidConfigurationDoesNotProducePolicyEvidence()
    {
        var fixture = new PolicyFixture(new MfaOptions { Enabled = false, RequireMfaByDefault = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.EvaluateAsync());
        fixture.Sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MembershipStorageFailurePropagatesInsteadOfReturningOptionalMfa()
    {
        var fixture = new PolicyFixture();
        fixture.Sender.Setup(sender => sender.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Synthetic membership storage failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.EvaluateAsync());
    }

    [Fact]
    public async Task MissingMembershipResponseDoesNotProducePolicyEvidence()
    {
        var fixture = new PolicyFixture();
        fixture.Sender.Setup(sender => sender.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUserMembershipsResponse)null!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.EvaluateAsync());
    }

    [Fact]
    public async Task EmptySubjectCannotDispatchAQuery()
    {
        var fixture = new PolicyFixture();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Policy.EvaluateAsync(Guid.Empty, null));
        fixture.Sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancelledEvaluationDoesNotDispatchAQuery()
    {
        var fixture = new PolicyFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Policy.EvaluateAsync(fixture.SubjectId, null, cancellation.Token));
        fixture.Sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationAfterMembershipLookupCannotProduceEvidence()
    {
        var fixture = new PolicyFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Sender.Setup(sender => sender.Send(It.IsAny<GetUserMembershipsQuery>(), cancellation.Token))
            .Returns(() => { cancellation.Cancel(); return Task.FromResult(new GetUserMembershipsResponse { Memberships = [fixture.Membership] }); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Policy.EvaluateAsync(fixture.SubjectId, null, cancellation.Token));
    }

    [Fact]
    public async Task FingerprintIsStableForSameSubjectAndChangesWithPolicyOrRole()
    {
        var fixture = new PolicyFixture();
        var first = await fixture.EvaluateAsync();
        Assert.Equal(first.PolicyFingerprint, (await fixture.EvaluateAsync()).PolicyFingerprint);
        Assert.Matches("^[a-f0-9]{64}$", first.PolicyFingerprint);
        fixture.Options.RequireMfaByDefault = true;
        var required = await fixture.EvaluateAsync();
        Assert.NotEqual(first.PolicyFingerprint, required.PolicyFingerprint);
        fixture.SetMemberships(fixture.Membership with { Role = "Owner" });
        Assert.NotEqual(required.PolicyFingerprint, (await fixture.EvaluateAsync()).PolicyFingerprint);
    }

    [Fact]
    public async Task SubjectBindingChangesFingerprintEvenForIdenticalMemberships()
    {
        var fixture = new PolicyFixture();
        fixture.Sender.Setup(sender => sender.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse { Memberships = [fixture.Membership] });
        var first = await fixture.EvaluateAsync();
        var other = await fixture.Policy.EvaluateAsync(Guid.NewGuid(), null);
        Assert.NotEqual(first.SubjectId, other.SubjectId);
        Assert.NotEqual(first.PolicyFingerprint, other.PolicyFingerprint);
    }

    [Fact]
    public async Task DecisionRoleCollectionCannotBeMutatedAfterEvaluation()
    {
        var fixture = new PolicyFixture();
        var result = await fixture.EvaluateAsync();
        var collection = Assert.IsAssignableFrom<IList<string>>(result.Roles);
        Assert.Throws<NotSupportedException>(() => collection.Add("OWNER"));
        Assert.False(result.RequiresMfa);
    }

    [Fact]
    public async Task FingerprintEscapesUntrustedRoleSeparators()
    {
        var fixture = new PolicyFixture();
        fixture.SetMemberships(fixture.Membership with { Role = "Member,Owner\nAdmin" });
        var unusual = await fixture.EvaluateAsync();
        Assert.False(unusual.RequiresMfa);
        fixture.SetMemberships(fixture.Membership with { Role = "Owner" });
        var elevated = await fixture.EvaluateAsync();
        Assert.True(elevated.RequiresMfa);
        Assert.NotEqual(unusual.PolicyFingerprint, elevated.PolicyFingerprint);
    }

    private sealed class PolicyFixture
    {
        public Guid SubjectId { get; } = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public Mock<ISender> Sender { get; } = new(MockBehavior.Strict);
        public MfaOptions Options { get; }
        public MfaSubjectRequirementPolicy Policy { get; }
        public UserMembershipDto Membership => new()
        {
            MembershipId = Guid.NewGuid(), TenantId = TenantId, TenantIsDefault = true,
            TenantIsActive = true, IsActive = true, Role = "Member"
        };

        public PolicyFixture(MfaOptions? options = null)
        {
            Options = options ?? new MfaOptions();
            Policy = new MfaSubjectRequirementPolicy(Sender.Object, Options);
            SetMemberships(Membership);
        }

        public void SetMemberships(params UserMembershipDto[] memberships) =>
            Sender.Setup(sender => sender.Send(It.Is<GetUserMembershipsQuery>(query => query.UserId == SubjectId && query.IncludeInactive), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetUserMembershipsResponse { Memberships = memberships, TotalCount = memberships.Length });
        public Task<MfaRequirementDecision> EvaluateAsync(Guid? requestedTenantId = null) =>
            Policy.EvaluateAsync(SubjectId, requestedTenantId);
        public void VerifyExactSubjectQuery() =>
            Sender.Verify(sender => sender.Send(It.Is<GetUserMembershipsQuery>(query => query.UserId == SubjectId && query.IncludeInactive), CancellationToken.None), Times.Once);
    }
}
