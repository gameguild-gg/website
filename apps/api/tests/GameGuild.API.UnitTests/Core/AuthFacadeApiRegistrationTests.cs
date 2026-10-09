using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.UnitTests.Core;

public sealed class AuthFacadeApiRegistrationTests(AuthFacadeApiFactory factory) : IClassFixture<AuthFacadeApiFactory>
{
    public static TheoryData<Type, Type> Services => new()
    {
        { typeof(IAuthService), typeof(AuthService) },
        { typeof(ILocalAuthService), typeof(LocalAuthService) },
        { typeof(IOAuthAuthService), typeof(OAuthAuthService) },
        { typeof(IPasswordService), typeof(PasswordService) },
        { typeof(IWeb3AuthService), typeof(Web3AuthService) }
    };

    public static TheoryData<Type, Type> Consumers => new()
    {
        { typeof(IRequestHandler<LocalSignInCommand, SignInResponse>), typeof(LocalSignInHandler) },
        { typeof(IRequestHandler<LocalSignUpCommand, SignInResponse>), typeof(LocalSignUpHandler) },
        { typeof(IRequestHandler<RefreshTokenCommand, SignInResponse>), typeof(RefreshTokenHandler) },
        { typeof(IRequestHandler<RevokeTokenCommand, Unit>), typeof(RevokeTokenHandler) },
        { typeof(IRequestHandler<SocialSignInCommand, SignInResponse>), typeof(SocialSignInHandler) },
        { typeof(IRequestHandler<PolymorphicSignInCommand, SignInResponse>), typeof(PolymorphicSignInHandler) },
        { typeof(IRequestHandler<GoogleIdTokenSignInCommand, SignInResponse>), typeof(GoogleIdTokenSignInHandler) },
        { typeof(IRequestHandler<VerifyWeb3SignatureCommand, SignInResponse>), typeof(VerifyWeb3SignatureHandler) }
    };

    [Theory]
    [MemberData(nameof(Services))]
    public void CompleteApi_ResolvesRealServicesWithScopedLifetime(Type contract, Type implementation)
    {
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();

        var instance = first.ServiceProvider.GetRequiredService(contract);

        instance.GetType().Should().Be(implementation);
        first.ServiceProvider.GetRequiredService(contract).Should().BeSameAs(instance);
        second.ServiceProvider.GetRequiredService(contract).Should().NotBeSameAs(instance);
        var descriptor = factory.Registrations.Last(registration => registration.ServiceType == contract);
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
        descriptor.ImplementationType.Should().Be(implementation);
    }

    [Theory]
    [MemberData(nameof(Consumers))]
    public void CompleteApi_ResolvesCurrentFacadeConsumers(Type contract, Type implementation)
    {
        using var scope = factory.Services.CreateScope();

        var consumer = scope.ServiceProvider.GetRequiredService(contract);

        consumer.GetType().Should().Be(implementation);
        implementation.GetConstructors().Single().GetParameters()
            .Should().Contain(parameter => parameter.ParameterType == typeof(IAuthService));
    }

    [Fact]
    public void CompleteApi_ResolvesRequiredSelfRevocationCollaborators()
    {
        using var scope = factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<RevokeAllUserTokensCommand, int>>();
        handler.Should().BeOfType<RevokeAllUserTokensHandler>();
        var parameters = typeof(RevokeAllUserTokensHandler).GetConstructors().Single().GetParameters();
        parameters.Should().OnlyContain(parameter => !parameter.IsOptional);
        parameters.Select(parameter => parameter.ParameterType).Should().BeEquivalentTo(new[]
        {
            typeof(IActorContextAccessor), typeof(IUserRepository), typeof(IRefreshTokenRepository),
            typeof(ISessionManagementService), typeof(IVersionedUserTokenRevocationService), typeof(IRefreshTokenLifecycleRecorder)
        });
        foreach (var parameter in parameters)
        {
            scope.ServiceProvider.GetRequiredService(parameter.ParameterType).Should().NotBeNull();
        }
    }
}

public sealed class AuthFacadeApiFactory : WebApplicationFactory<Program>
{
    public AuthFacadeApiFactory() => TestDataProtectionEnvironment.EnsureConfigured();

    public IReadOnlyList<ServiceDescriptor> Registrations { get; private set; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Database:RunStartupInitialization"] = "false" }));
        builder.ConfigureTestServices(services =>
        {
            // Exercise the complete API composition; only its database provider is replaced.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"AuthFacadeRegistration_{Guid.NewGuid():N}"));
            services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            services.AddHttpLogging(_ => { });
            Registrations = services.ToArray();
        });
    }
}
