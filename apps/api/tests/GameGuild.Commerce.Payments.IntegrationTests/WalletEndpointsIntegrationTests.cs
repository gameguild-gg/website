using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;

namespace GameGuild.Commerce.Payments.IntegrationTests;

/// <summary>
/// Integration tests for Wallet API endpoints
/// </summary>
public class WalletEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DatabaseName = $"PaymentsWalletTestDb_{Guid.NewGuid()}";

    /// <summary>
    ///     Matches the API's JSON contract (camelCase, enums as strings — the API registers
    ///     <c>JsonStringEnumConverter</c>), so typed reads of payloads carrying
    ///     <see cref="WalletTransaction"/> enums deserialize like the shipped client does.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();

    private static System.Text.Json.JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }

    public WalletEndpointsIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                // Remove all EF Core and Npgsql service registrations
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                                d.ServiceType == typeof(ApplicationDbContext) ||
                                d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                                d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
                // The certificate-protected EF key repository cannot run on the in-memory
                // provider (it opens an explicit transaction, which in-memory rejects, and
                // its key-creation retry then double-inserts on the request-scoped
                // context). Wallet flows do not assert key persistence — keep keys
                // ephemeral so the DataProtection pipeline never touches the test database.
                services.RemoveAll<Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository>();
                services.AddSingleton<Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository, EphemeralKeyXmlRepository>();
                services.AddDefaultTenantMembership();

                // Authorization matrix tests intentionally exercise permission claims from
                // the test token instead of loading grants from the application database.
                services.RemoveAll<IAuthorizationPermissionService>();
                services.AddScoped<IAuthorizationPermissionService, TestAuthorizationPermissionService>();
                services.RemoveAll<IAuthorizationTenantResolver>();
                services.AddScoped<IAuthorizationTenantResolver, TestAuthorizationTenantResolver>();
                services.RemoveAll<IRevenueAuditService>();
                services.AddSingleton<TestRevenueAuditService>();
                services.AddSingleton<IRevenueAuditService>(provider => provider.GetRequiredService<TestRevenueAuditService>());

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        });

        TestTenantMembershipServices.SeedDefaultTenant(_factory.Services);
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateWallet_ShouldDeriveTheOwnerFromTheAuthenticatedActor()
    {
        var subjectId = Guid.NewGuid();
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/wallet", subjectId);
        request.Content = JsonContent.Create(new { Currency = "USD" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(subjectId.ToString());
    }

    [Fact]
    public async Task CreateWallet_ShouldAllowOneWalletPerCurrencyButRejectDuplicates()
    {
        var subjectId = Guid.NewGuid();

        using var firstRequest = CreateRequest(HttpMethod.Post, "/api/v1/wallet", subjectId);
        firstRequest.Content = JsonContent.Create(new { Currency = "USD" });
        var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Issue #406: a user may hold one wallet in each currency concurrently.
        using var secondRequest = CreateRequest(HttpMethod.Post, "/api/v1/wallet", subjectId);
        secondRequest.Content = JsonContent.Create(new { Currency = "EUR" });
        var secondResponse = await _client.SendAsync(secondRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created, await secondResponse.Content.ReadAsStringAsync());

        // ... but never two wallets in the same currency.
        using var duplicateRequest = CreateRequest(HttpMethod.Post, "/api/v1/wallet", subjectId);
        duplicateRequest.Content = JsonContent.Create(new { Currency = "USD" });
        var duplicateResponse = await _client.SendAsync(duplicateRequest);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetWallet_ShouldReturnOnlyTheAuthenticatedActorsWallet()
    {
        var subjectId = Guid.NewGuid();
        await SeedWalletAsync(subjectId);
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/wallet", subjectId);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var wallet = await response.Content.ReadFromJsonAsync<UserWallet>(ApiJsonOptions);
        wallet!.UserId.Should().Be(subjectId);
    }

    [Fact]
    public async Task LegacyUserRoute_ShouldNotExposeAnotherUsersWallet()
    {
        var subjectId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await SeedWalletAsync(otherUserId);
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/users/{otherUserId}/wallet", subjectId);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TenantAdmin_ShouldNotGainCrossUserWalletAccess()
    {
        var tenantAdminId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await SeedWalletAsync(otherUserId);
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/users/{otherUserId}/wallet",
            tenantAdminId,
            roles: "TenantAdmin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlatformAdminEndpoint_ShouldRejectAnActorWithoutWalletAdminPermission()
    {
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(HttpMethod.Get, $"/api/v1/wallets/{wallet.Id}", Guid.NewGuid());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformAdminEndpoint_ShouldAllowAnActorWithWalletAdminPermission()
    {
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/wallets/{wallet.Id}",
            Guid.NewGuid(),
            permissions: "wallets:admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SelfServiceWallet_ShouldRejectMissingTenantContext()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/wallet", Guid.NewGuid(), omitTenant: true);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SelfServiceWallet_ShouldRejectAnUnauthenticatedRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/wallet");
        request.Headers.Add("X-Test-Unauthenticated", "true");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ArbitraryWalletCreationRoute_ShouldNotBePublic()
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/v1/wallets", Guid.NewGuid());
        request.Content = JsonContent.Create(new { UserId = Guid.NewGuid(), Currency = "USD" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("add-funds")]
    [InlineData("deduct-funds")]
    [InlineData("transfer")]
    public async Task LegacyUserScopedValueMutationRoutes_ShouldNotBeExposed(string operation)
    {
        // Issue #406: wallet value mutations moved to the admin-gated
        // POST wallets/{id}:adjust and POST wallets:transfer routes; the legacy
        // user-scoped routes stay unexposed so self-service actors cannot mint balances.
        var subjectId = Guid.NewGuid();
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/users/{subjectId}/wallet:{operation}",
            subjectId);
        request.Content = JsonContent.Create(new
        {
            Amount = 10m,
            Description = "untrusted mutation",
            ToUserId = Guid.NewGuid()
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task AdjustWallet_ShouldRequireWalletAdminPermission()
    {
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/wallets/{wallet.Id}:adjust",
            Guid.NewGuid(),
            roles: "TenantAdmin");
        request.Content = JsonContent.Create(new { Direction = "credit", Amount = 10m, Reason = "support credit" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdjustWallet_Credit_ShouldAdjustTheBalanceAndPersistTheAuditEvent()
    {
        var administratorId = Guid.NewGuid();
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/wallets/{wallet.Id}:adjust",
            administratorId,
            roles: "Admin",
            permissions: "wallets:admin");
        request.Content = JsonContent.Create(new { Direction = "credit", Amount = 25m, Reason = "goodwill credit" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var transaction = await response.Content.ReadFromJsonAsync<WalletTransaction>(ApiJsonOptions);
        transaction!.Amount.Should().Be(25m);
        transaction.BalanceAfter.Should().Be(25m);

        using var balanceRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/wallets/{wallet.Id}",
            Guid.NewGuid(),
            permissions: "wallets:admin");
        var balanceResponse = await _client.SendAsync(balanceRequest);
        balanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await balanceResponse.Content.ReadFromJsonAsync<UserWallet>(ApiJsonOptions);
        updated!.Balance.Should().Be(25m);

        var auditService = _factory.Services.GetRequiredService<TestRevenueAuditService>();
        var audit = auditService.Entries.SingleOrDefault(entry => entry.EntityId == wallet.Id);
        audit.Should().NotBeNull();
        audit!.ChangedBy.Should().Be(administratorId);
        audit.Reason.Should().Be("goodwill credit");
    }

    [Fact]
    public async Task AdjustWallet_DebitWithInsufficientBalance_ShouldReturnConflict()
    {
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/wallets/{wallet.Id}:adjust",
            Guid.NewGuid(),
            permissions: "wallets:admin");
        request.Content = JsonContent.Create(new { Direction = "debit", Amount = 100m, Reason = "correction" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AdjustWallet_ReplayWithTheSameReferenceId_ShouldBeIdempotent()
    {
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = CreateRequest(
                HttpMethod.Post,
                $"/api/v1/wallets/{wallet.Id}:adjust",
                Guid.NewGuid(),
                permissions: "wallets:admin");
            request.Content = JsonContent.Create(new { Direction = "credit", Amount = 15m, Reason = "webhook replay", ReferenceId = "charge_123" });

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var balanceRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/wallets/{wallet.Id}",
            Guid.NewGuid(),
            permissions: "wallets:admin");
        var balanceResponse = await _client.SendAsync(balanceRequest);
        var updated = await balanceResponse.Content.ReadFromJsonAsync<UserWallet>(ApiJsonOptions);
        updated!.Balance.Should().Be(15m, "a replayed reference must not double-credit the wallet");
    }

    [Fact]
    public async Task TransferWallets_ShouldRequireWalletAdminPermission()
    {
        var fromWallet = await SeedWalletAsync(Guid.NewGuid());
        var toWallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Post,
            "/api/v1/wallets:transfer",
            Guid.NewGuid(),
            roles: "TenantAdmin");
        request.Content = JsonContent.Create(new { FromWalletId = fromWallet.Id, ToWalletId = toWallet.Id, Amount = 5m, Description = "transfer" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TransferWallets_ShouldMoveFundsBetweenSameCurrencyWallets()
    {
        var fromWallet = await SeedWalletAsync(Guid.NewGuid(), balance: 50m);
        var toWallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Post,
            "/api/v1/wallets:transfer",
            Guid.NewGuid(),
            permissions: "wallets:admin");
        request.Content = JsonContent.Create(new { FromWalletId = fromWallet.Id, ToWalletId = toWallet.Id, Amount = 20m, Description = "consolidation" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var fromWalletAfter = await GetWalletAsync(fromWallet.Id);
        var toWalletAfter = await GetWalletAsync(toWallet.Id);
        fromWalletAfter!.Balance.Should().Be(30m);
        toWalletAfter!.Balance.Should().Be(20m);
    }

    [Fact]
    public async Task TransferWallets_WithCurrencyMismatch_ShouldReturnBadRequest()
    {
        var fromWallet = await SeedWalletAsync(Guid.NewGuid(), currency: "USD", balance: 50m);
        var toWallet = await SeedWalletAsync(Guid.NewGuid(), currency: "EUR");
        using var request = CreateRequest(
            HttpMethod.Post,
            "/api/v1/wallets:transfer",
            Guid.NewGuid(),
            permissions: "wallets:admin");
        request.Content = JsonContent.Create(new { FromWalletId = fromWallet.Id, ToWalletId = toWallet.Id, Amount = 20m, Description = "cross-currency" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WalletMetrics_ShouldRequireWalletAdminPermission()
    {
        using var deniedRequest = CreateRequest(HttpMethod.Get, "/api/v1/wallets/metrics", Guid.NewGuid(), roles: "TenantAdmin");
        var deniedResponse = await _client.SendAsync(deniedRequest);
        deniedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowedRequest = CreateRequest(HttpMethod.Get, "/api/v1/wallets/metrics", Guid.NewGuid(), permissions: "wallets:admin");
        var allowedResponse = await _client.SendAsync(allowedRequest);
        allowedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await allowedResponse.Content.ReadAsStringAsync();
        content.Should().Contain("balancesByCurrency");
    }

    [Fact]
    public async Task WalletReconciliation_ShouldReportConsistencyAndDetectDiscrepancies()
    {
        // Seed a wallet whose balance agrees with its completed transaction history.
        var consistentWallet = await SeedWalletAsync(Guid.NewGuid(), balance: 0m);
        using var adjustRequest = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/wallets/{consistentWallet.Id}:adjust",
            Guid.NewGuid(),
            permissions: "wallets:admin");
        adjustRequest.Content = JsonContent.Create(new { Direction = "credit", Amount = 40m, Reason = "seed" });
        (await _client.SendAsync(adjustRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Corrupt a second wallet's balance so the report-only check has something to find.
        var corruptedWallet = await SeedWalletAsync(Guid.NewGuid(), balance: 0m);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await dbContext.Set<UserWallet>().SingleAsync(w => w.Id == corruptedWallet.Id);
            stored.Balance = 999m; // no matching transactions
            await dbContext.SaveChangesAsync();
        }

        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/v1/wallets/reconciliation",
            Guid.NewGuid(),
            permissions: "wallets:admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<WalletReconciliationReportResponse>();
        report!.WalletsChecked.Should().BeGreaterThanOrEqualTo(2);
        report.DiscrepanciesFound.Should().BeGreaterThanOrEqualTo(1);
        var consistent = report.Items.Single(item => item.WalletId == consistentWallet.Id);
        consistent.BalanceMatchesLatest.Should().BeTrue();
        consistent.BalanceMatchesTransactionSum.Should().BeTrue();
        var corrupted = report.Items.Single(item => item.WalletId == corruptedWallet.Id);
        corrupted.BalanceMatchesTransactionSum.Should().BeFalse();
    }

    [Fact]
    public async Task ListWallets_ShouldRequireWalletAdminPermission()
    {
        using var deniedRequest = CreateRequest(HttpMethod.Get, "/api/v1/wallets", Guid.NewGuid(), roles: "TenantAdmin");
        using var allowedRequest = CreateRequest(
            HttpMethod.Get,
            "/api/v1/wallets",
            Guid.NewGuid(),
            permissions: "wallets:admin");

        var deniedResponse = await _client.SendAsync(deniedRequest);
        var allowedResponse = await _client.SendAsync(allowedRequest);

        deniedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FreezeWallet_ShouldPersistThePrivilegedActionAuditEvent()
    {
        var administratorId = Guid.NewGuid();
        var wallet = await SeedWalletAsync(Guid.NewGuid());
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/wallets/{wallet.Id}:freeze",
            administratorId,
            roles: "Admin",
            permissions: "wallets:admin");
        request.Content = JsonContent.Create(new { Reason = "manual fraud review" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var auditService = _factory.Services.GetRequiredService<TestRevenueAuditService>();
        var audit = auditService.Entries.SingleOrDefault(entry => entry.EntityId == wallet.Id);
        audit.Should().NotBeNull();
        audit!.ChangedBy.Should().Be(administratorId);
        audit.Reason.Should().Be("manual fraud review");
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        Guid subjectId,
        string? roles = null,
        string? permissions = null,
        bool omitTenant = false)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-Subject", subjectId.ToString());
        if (!string.IsNullOrWhiteSpace(roles))
        {
            request.Headers.Add("X-Test-Roles", roles);
        }

        if (!string.IsNullOrWhiteSpace(permissions))
        {
            request.Headers.Add("X-Test-Permissions", permissions);
        }

        if (!omitTenant)
        {
            request.Headers.Add("X-Tenant-Id", TestAuthHandler.DefaultTenantId.ToString());
        }

        if (omitTenant)
        {
            request.Headers.Add("X-Test-No-Tenant", "true");
        }

        return request;
    }

    private async Task<UserWallet> SeedWalletAsync(Guid userId, string currency = "USD", decimal balance = 0m)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = new UserWallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Currency = currency,
            Balance = balance,
            IsActive = true
        };
        dbContext.Set<UserWallet>().Add(wallet);
        await dbContext.SaveChangesAsync();
        return wallet;
    }

    private async Task<UserWallet?> GetWalletAsync(Guid walletId)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/v1/wallets/{walletId}",
            Guid.NewGuid(),
            permissions: "wallets:admin");
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<UserWallet>(ApiJsonOptions);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    ///     Non-persisting DataProtection key store: keeps the certificate-protected EF key
    ///     repository away from the in-memory database (explicit transactions and its
    ///     key-creation retry both break there).
    /// </summary>
    private sealed class EphemeralKeyXmlRepository : Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<System.Xml.Linq.XElement> _elements = new();

        public IReadOnlyCollection<System.Xml.Linq.XElement> GetAllElements() => _elements.ToArray();

        public void StoreElement(System.Xml.Linq.XElement element, string? friendlyName) => _elements.Enqueue(element);
    }
}
