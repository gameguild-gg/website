using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class UsernameSignUpPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("Matheus Martins", "matheus-martins")]
    [InlineData("MátHeus Martíns", "matheus-martins")]
    [InlineData("User.Name_1", "user.name_1")]
    public async Task AnonymousSignupPersistsCanonicalHandleAndReturnsItOverHttp(string input, string canonical)
    {
        var marker = Guid.NewGuid().ToString("N");
        var display = $"{input}-{marker}";
        var email = $"signup-{marker}@example.test";
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-up", new
        {
            email, password = CreateSyntheticPassword(), username = display
        });
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {response.StatusCode}: {payload}");
        using var json = JsonDocument.Parse(payload);
        Assert.Equal($"{canonical}-{marker}", json.RootElement.GetProperty("user").GetProperty("username").GetString());
        var userId = json.RootElement.GetProperty("userId").GetGuid();
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("refreshToken").GetString()));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Set<User>().SingleAsync(user => user.Id == userId);
        Assert.Equal($"{canonical}-{marker}", persisted.Username);
        Assert.Equal(display, persisted.Name);
        Assert.Equal(email, persisted.Email);
    }

    [Theory]
    [InlineData("---")]
    [InlineData("東京")]
    [InlineData("  ab  ")]
    public async Task UnusableCanonicalHandleIsRejectedBeforeCreatingAnAccount(string username)
    {
        var email = $"invalid-{Guid.NewGuid():N}@example.test";
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-up", new
        {
            email, password = CreateSyntheticPassword(), username
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("username", payload, StringComparison.OrdinalIgnoreCase);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<User>().AnyAsync(user => user.Email == email));
    }

    [Fact]
    public async Task ChosenUsernameCollisionReturnsValidationAndDoesNotCreateASecondAccount()
    {
        var marker = Guid.NewGuid().ToString("N");
        var existing = User.CreateWithPassword($"existing-{marker}@example.test", "Existing", "synthetic-hash", $"Chosen {marker}");
        using (var setup = fixture.Factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<User>().Add(existing);
            await db.SaveChangesAsync();
        }

        var email = $"collision-{marker}@example.test";
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/sign-up", new
        {
            email, password = CreateSyntheticPassword(), username = $"CHOSEN {marker}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("username", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var scope = fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.Set<User>().AnyAsync(user => user.Email == email));
        Assert.Equal($"chosen-{marker}", (await context.Set<User>().SingleAsync(user => user.Id == existing.Id)).Username);
    }

    private static string CreateSyntheticPassword() => $"Synthetic1!{Guid.NewGuid():N}";
}
