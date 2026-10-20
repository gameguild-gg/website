using GameGuild.API.Core.Integration;
using GameGuild.API.Database;
using GameGuild.API.Security;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

/// <summary>Controller guard tests with an InMemory application model. Real JWT/PostgreSQL HTTP acceptance is separate.</summary>
public sealed class CertificateEvmEvidenceControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnonymousRequestsCannotReachTheEvidenceReader(bool fresh)
    {
        await using var h = await Harness.Create(ActorContext.Anonymous);
        var result = await h.Read(fresh);
        Assert.IsType<UnauthorizedResult>(result.Result); h.Reader.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherOwnerInTheSameTenantCannotReachTheEvidenceReader(bool fresh)
    {
        var owner = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await using var h = await Harness.Create(Actor(Guid.NewGuid(), tenant, false));
        await h.Seed(owner, tenant, false);
        Assert.IsType<NotFoundResult>((await h.Read(fresh)).Result); h.Reader.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwningTheDocumentDoesNotAllowReadingItThroughAnotherTenant(bool fresh)
    {
        var owner = Guid.NewGuid();
        await using var h = await Harness.Create(Actor(owner, Guid.NewGuid(), false));
        await h.Seed(owner, Guid.NewGuid(), false);
        Assert.IsType<NotFoundResult>((await h.Read(fresh)).Result); h.Reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ASoftDeletedDocumentIsNotExposedEvenToAnAdministrator()
    {
        await using var h = await Harness.Create(Actor(Guid.NewGuid(), Guid.NewGuid(), true));
        await h.Seed(Guid.NewGuid(), Guid.NewGuid(), true);
        Assert.IsType<NotFoundResult>((await h.Read(false)).Result); h.Reader.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheOwnerReceivesOnlyThePersistedDocumentScope(bool fresh)
    {
        var owner = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await using var h = await Harness.Create(Actor(owner, tenant, false)); await h.Seed(owner, tenant, false);
        h.AllowReader();
        Assert.IsType<OkObjectResult>((await h.Read(fresh)).Result);
        h.Reader.Verify(r => r.ReadAsync(It.Is<CertificateEvmDocument>(d => d.Id == h.Id && d.OwnerUserId == owner && d.TenantId == tenant),
            fresh, It.IsAny<CancellationToken>()), Times.Once);
        h.Reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnExplicitSystemAdministratorCanReadThePersistedForeignScope()
    {
        var owner = Guid.NewGuid(); var tenant = Guid.NewGuid();
        await using var h = await Harness.Create(Actor(Guid.NewGuid(), Guid.NewGuid(), true)); await h.Seed(owner, tenant, false);
        h.AllowReader(); Assert.IsType<OkObjectResult>((await h.Read(true)).Result);
        h.Reader.Verify(r => r.ReadAsync(It.Is<CertificateEvmDocument>(d => d.OwnerUserId == owner && d.TenantId == tenant),
            true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AGlobalCertificateRequiresTheOwnersExactNullTenant()
    {
        var owner = Guid.NewGuid(); await using var h = await Harness.Create(Actor(owner, null, false));
        await h.Seed(owner, null, false); h.AllowReader();
        Assert.IsType<OkObjectResult>((await h.Read(false)).Result);
    }

    [Fact]
    public void BothRoutesDeclareAuthorizationAndPreventResponseCaching()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(CertificateEvmEvidenceController), typeof(AuthorizeAttribute)));
        var cache = Assert.IsType<ResponseCacheAttribute>(Attribute.GetCustomAttribute(typeof(CertificateEvmEvidenceController), typeof(ResponseCacheAttribute)));
        Assert.True(cache.NoStore); Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }

    private static ActorContext Actor(Guid owner, Guid? tenant, bool admin) => ActorContext.Anonymous with
    {
        ActorKind = ActorKind.User, SubjectId = owner.ToString(), IsAuthenticated = true, TenantId = tenant,
        Roles = new HashSet<string>(admin ? new[] { "SystemAdmin" } : new[] { "Member" })
    };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ApplicationDbContext context;
        private readonly CertificateEvmEvidenceController controller;
        public Guid Id { get; } = Guid.NewGuid();
        public Mock<ICertificateEvmEvidenceReader> Reader { get; } = new(MockBehavior.Strict);
        private Harness(ActorContext actor)
        {
            context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
                enableEntityChangeAuditCapture: false);
            var actors = new Mock<IActorContextAccessor>(); actors.SetupGet(a => a.ActorContext).Returns(actor);
            controller = new CertificateEvmEvidenceController(context, actors.Object, Reader.Object);
        }
        public static Task<Harness> Create(ActorContext actor) => Task.FromResult(new Harness(actor));
        public async Task Seed(Guid owner, Guid? tenant, bool deleted)
        {
            var certificate = Certificate.Issue(Guid.NewGuid(), Guid.NewGuid(), owner, Guid.NewGuid(), "Private recipient", "Private course", null, tenant);
            certificate.Id = Id; if (deleted) { certificate.DeletedAt = DateTime.UtcNow; }
            context.Add(certificate); await context.SaveChangesAsync();
        }
        public Task<ActionResult<CertificateEvmEvidenceResponse>> Read(bool fresh) => fresh
            ? controller.GetCertificateEvmProof(Id, CancellationToken.None) : controller.GetCertificateEvmEvidence(Id, CancellationToken.None);
        public void AllowReader()
        {
            Reader.Setup(r => r.ReadAsync(It.IsAny<CertificateEvmDocument>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns((CertificateEvmDocument document, bool fresh, CancellationToken ct) =>
                    new DisabledCertificateEvmEvidenceReader(TimeProvider.System).ReadAsync(document, fresh, ct));
        }
        public ValueTask DisposeAsync() => context.DisposeAsync();
    }
}
