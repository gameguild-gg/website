using FluentAssertions;
using GameGuild.API.Teams;
using GameGuild.Identity.Context.Actors;
using GameGuild.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.API.UnitTests.Teams;

/// <summary>
/// Universal audit coverage for team mutations (issues #180/#181/#184): every team endpoint
/// command emits one teams audit event, and an audit emission failure rolls the mutation back
/// inside the owning transaction (fail closed).
/// </summary>
public sealed class TeamEndpointCommandAuditTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private IActorContextAccessor ActorAccessor() =>
        Mock.Of<IActorContextAccessor>(accessor => accessor.ActorContext == new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = _actorId.ToString(),
            TenantId = _tenantId,
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });

    [Fact]
    public async Task EveryTeamCommandEmitsItsAuditEvent()
    {
        await using var context = CreateSqliteContext();
        var sink = new RecordingTeamsAuditEventSink();
        var handler = new TeamEndpointCommandHandler(context, sink, ActorAccessor());

        var team = Team.Create(_tenantId, "Studio", "studio", _actorId);
        await handler.Handle(new CreateTeamEndpointCommand(team), CancellationToken.None);
        team.Description = "updated";
        await handler.Handle(new UpdateTeamEndpointCommand(team), CancellationToken.None);
        var member = team.AddMember(Guid.NewGuid(), TeamMemberAuthority.Member);
        await handler.Handle(new AddTeamMemberEndpointCommand(member, IsNew: true), CancellationToken.None);
        member.Authority = TeamMemberAuthority.Maintainer;
        await handler.Handle(new ChangeTeamMemberEndpointCommand(member), CancellationToken.None);
        var invitation = TeamInvitation.Create(_tenantId, team.Id, _actorId, InvitedEmail: "invitee@example.com", TeamMemberAuthority.Member, token: "invitation-token", ExpiresAt: SystemClock.UtcNow.AddDays(3));
        await handler.Handle(new CreateTeamInvitationEndpointCommand(invitation), CancellationToken.None);
        await handler.Handle(new RevokeTeamInvitationEndpointCommand(invitation), CancellationToken.None);
        var acceptedMember = team.AddMember(Guid.NewGuid(), TeamMemberAuthority.Member);
        await handler.Handle(new AcceptTeamInvitationEndpointCommand(team, acceptedMember, IsNewMember: true), CancellationToken.None);
        team.Archive();
        await handler.Handle(new ArchiveTeamEndpointCommand(team), CancellationToken.None);
        team.Restore();
        await handler.Handle(new RestoreTeamEndpointCommand(team), CancellationToken.None);
        team.RemoveMember(team.Members.Single(member => member.UserId != _actorId).UserId);
        await handler.Handle(new RemoveTeamMemberEndpointCommand(team), CancellationToken.None);

        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().Equal(
            "Teams.TeamCreated",
            "Teams.TeamUpdated",
            "Teams.TeamMemberAdded",
            "Teams.TeamMemberChanged",
            "Teams.TeamInvitationCreated",
            "Teams.TeamInvitationRevoked",
            "Teams.TeamInvitationAccepted",
            "Teams.TeamArchived",
            "Teams.TeamRestored",
            "Teams.TeamMemberRemoved");
        sink.Events.Should().OnlyContain(auditEvent => auditEvent.ActorUserId == _actorId);
        sink.Events.Should().OnlyContain(auditEvent => auditEvent.TenantId == _tenantId);
    }

    [Fact]
    public async Task AuditEmissionFailureRollsBackTheTeamCreation()
    {
        await using var context = CreateSqliteContext();
        var handler = new TeamEndpointCommandHandler(context, new ThrowingTeamsAuditEventSink(), ActorAccessor());

        var team = Team.Create(_tenantId, "Must Not Exist", "must-not-exist", _actorId);
        var act = () => handler.Handle(new CreateTeamEndpointCommand(team), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.Set<Team>().AsNoTracking().CountAsync()).Should().Be(0);
    }

    private TeamAuditSqliteContext CreateSqliteContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TeamAuditSqliteContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TeamAuditSqliteContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class TeamAuditSqliteContext(DbContextOptions<TeamAuditSqliteContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);
    }
}

public sealed class RecordingTeamsAuditEventSink : ITeamsAuditEventSink
{
    public List<TeamsAuditEvent> Events { get; } = [];

    public Task RecordAsync(TeamsAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

public sealed class NoopTeamsAuditEventSink : ITeamsAuditEventSink
{
    public Task RecordAsync(TeamsAuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ThrowingTeamsAuditEventSink : ITeamsAuditEventSink
{
    public Task RecordAsync(TeamsAuditEvent auditEvent, CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Synthetic teams audit persistence failure."));
}
