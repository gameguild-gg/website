using System.Text.Json;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class ValueSnapshotRetentionServiceTests : ValueSnapshotServiceTestBase
{
    private ValueSnapshotRetentionService Service() => new(
        Actors.Object, Context, TimeProvider.System, NullLogger<ValueSnapshotRetentionService>.Instance);

    [Fact]
    public async Task DryRunRecordsItsDecisionWithoutRemovingSnapshotPayloads()
    {
        var snapshot = SeedSnapshot("Record", "r1", "Added", null, """{"Value":1}""", DateTimeOffset.UtcNow.AddDays(-90));
        await Service().ConfigurePolicyAsync(new ConfigureValueSnapshotRetentionRequest
        {
            RetentionDays = 30, ArchiveAfterDays = 7
        }, CancellationToken.None);

        var result = await Service().EnforceAsync(new EnforceValueSnapshotRetentionRequest { DryRun = true }, CancellationToken.None);

        Assert.True(result.DryRun);
        Assert.Equal(1, result.DeletedCount);
        Context.ChangeTracker.Clear();
        var preserved = await Context.Set<MutationValueSnapshot>().SingleAsync(item => item.Id == snapshot.Id);
        Assert.Equal(snapshot.AfterPayload, preserved.AfterPayload);
        Assert.Null(preserved.ArchivedAtUtc);
        Assert.True(await Context.Set<ValueSnapshotRetentionExecution>().AnyAsync(item => item.Id == result.Id && item.DryRun));
    }

    [Fact]
    public async Task PoliciesRequireValidBoundsAndAnAuthenticatedAdministrator()
    {
        await Assert.ThrowsAsync<ValueSnapshotValidationException>(() => Service().ConfigurePolicyAsync(
            new ConfigureValueSnapshotRetentionRequest { RetentionDays = 1, ArchiveAfterDays = 7 }, CancellationToken.None));
        Assert.Empty(await Context.Set<ValueSnapshotRetentionPolicy>().ToListAsync());
        Actors.SetupGet(accessor => accessor.ActorContext).Returns(ActorContext.Anonymous);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().GetPolicyAsync(CancellationToken.None));
    }
}

public sealed class ValueSnapshotRollbackServiceTests : ValueSnapshotServiceTestBase
{
    private ValueSnapshotRollbackService Service() => new(
        Actors.Object, Context, TimeProvider.System, NullLogger<ValueSnapshotRollbackService>.Instance);

    [Fact]
    public async Task PlanReconstructsOnlyTheStateAtTheRequestedInstant()
    {
        var initialTime = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var basis = SeedSnapshot("Record", "r1", "Added", null, """{"Title":"Original","Count":1}""", initialTime);
        SeedSnapshot("Record", "r1", "Modified", """{"Title":"Original"}""", """{"Title":"New"}""", initialTime.AddDays(1));

        var plan = await Service().CreatePlanAsync(new CreateValueSnapshotRollbackPlanRequest
        {
            EntityName = "Record", KeyId = "r1", AsOfUtc = initialTime.AddHours(1).UtcDateTime
        }, CancellationToken.None);

        Assert.Equal(basis.Id, plan.BasisSnapshotId);
        Assert.Equal("Proposed", plan.Status);
        using var state = JsonDocument.Parse(plan.BasisStateJson);
        Assert.Equal("Original", state.RootElement.GetProperty("Title").GetString());
        Assert.Equal(1, state.RootElement.GetProperty("Count").GetInt32());
        Assert.Equal(plan.BasisStateSha256, (await Service().GetPlanAsync(plan.Id, CancellationToken.None))!.BasisStateSha256);
        Assert.Equal(2, await Context.Set<MutationValueSnapshot>().CountAsync());
    }

    [Fact]
    public async Task PlanRejectsMissingHistoryAndAnonymousActors()
    {
        await Assert.ThrowsAsync<ValueSnapshotRollbackException>(() => Service().CreatePlanAsync(new CreateValueSnapshotRollbackPlanRequest
        {
            EntityName = "Record", KeyId = "missing", AsOfUtc = DateTime.UtcNow
        }, CancellationToken.None));
        Assert.Empty(await Context.Set<ValueSnapshotRollbackPlan>().ToListAsync());
        Actors.SetupGet(accessor => accessor.ActorContext).Returns(ActorContext.Anonymous);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().GetPlanAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
