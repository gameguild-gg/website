using System.Security.Cryptography;
using System.Text;
using GameGuild.CQRS;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EmailTokenSingleUseSecurityTests
{
    private const string Email = "token-owner@example.test";
    private const string TokenPrefix = "emailverify:token:";

    [Theory]
    [InlineData("verification")]
    [InlineData("reset")]
    [InlineData("magic")]
    public async Task GeneratedTokens_StoreOnlyTheirDigestAsTheCacheKey(string kind)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var token = await Generate(CreateService(cache), kind, Guid.NewGuid());
        var key = Assert.Single(cache.Keys).ToString()!;

        Assert.DoesNotContain(token, key, StringComparison.Ordinal);
        Assert.Equal(TokenPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), key);
    }

    [Theory]
    [InlineData("verification", false)]
    [InlineData("verification", true)]
    [InlineData("verification-with-user", false)]
    [InlineData("verification-with-user", true)]
    [InlineData("reset", false)]
    [InlineData("reset", true)]
    [InlineData("magic", false)]
    [InlineData("magic", true)]
    public async Task ConcurrentConsumersAcrossServiceScopes_AllowExactlyOneSuccess(string kind, bool separateServices)
    {
        using var cache = new SynchronizedReadCache();
        var userId = Guid.NewGuid();
        var service = CreateService(cache);
        var token = await Generate(service, kind, userId);
        cache.SynchronizeNextTokenReads();
        var consumers = Enumerable.Range(0, 4).Select(_ => Task.Factory.StartNew(
            () => Consume(separateServices ? CreateService(cache) : service, kind, userId, token),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap()).ToArray();

        var results = await Task.WhenAll(consumers);

        Assert.False(cache.SynchronizationFailed);
        var result = Assert.Single(results, value => value.Success);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(Email, result.Email);
        Assert.False(await service.IsTokenValidAsync(token));
    }

    [Theory]
    [InlineData("verification")]
    [InlineData("reset")]
    [InlineData("magic")]
    public async Task ClaimedToken_IsAlreadyInvalidWhileCacheRemovalIsPending(string kind)
    {
        using var cache = new SuspendedRemovalCache();
        var userId = Guid.NewGuid();
        var service = CreateService(cache);
        var token = await Generate(service, kind, userId);
        cache.SuspendRemoval = true;
        var consumption = Task.Factory.StartNew(() => Consume(service, kind, userId, token),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        bool valid;
        try
        {
            Assert.True(cache.RemovalStarted.Wait(TimeSpan.FromSeconds(10)));
            valid = await CreateService(cache).IsTokenValidAsync(token);
        }
        finally
        {
            cache.AllowRemoval.Set();
        }
        Assert.True((await consumption).Success);
        Assert.False(valid);
    }

    [Theory]
    [InlineData("verification")]
    [InlineData("reset")]
    [InlineData("magic")]
    public async Task SequentialConsumption_PreservesIdentityAndRejectsReplay(string kind)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var userId = Guid.NewGuid();
        var service = CreateService(cache);
        var token = await Generate(service, kind, userId);

        Assert.True(await service.IsTokenValidAsync(token));
        var consumed = await Consume(service, kind, userId, token);
        Assert.True(consumed.Success);
        Assert.Equal(userId, consumed.UserId);
        Assert.Equal(Email, consumed.Email);
        Assert.False((await Consume(service, kind, userId, token)).Success);
        Assert.False(await service.IsTokenValidAsync(token));
    }

    [Fact]
    public async Task WrongPurpose_DoesNotConsumeTheValidToken()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache);
        var token = await service.GenerateMagicLinkTokenAsync(Guid.NewGuid(), Email);

        Assert.False((await service.VerifyPasswordResetTokenAsync(token)).Success);
        Assert.True(await service.IsTokenValidAsync(token));
        Assert.True((await service.VerifyMagicLinkTokenAsync(token)).Success);
    }

    [Fact]
    public async Task WrongUser_DoesNotConsumeTheValidToken()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache);
        var userId = Guid.NewGuid();
        var token = await service.GenerateVerificationTokenAsync(userId, Email);

        Assert.False(await service.VerifyEmailTokenAsync(Guid.NewGuid(), token));
        Assert.True(await service.IsTokenValidAsync(token));
        Assert.True(await service.VerifyEmailTokenAsync(userId, token));
    }

    private static EmailVerificationService CreateService(IMemoryCache cache) =>
        new(NullLogger<EmailVerificationService>.Instance, cache, Mock.Of<IPublisher>());

    private static Task<string> Generate(EmailVerificationService service, string kind, Guid userId) => kind switch
    {
        "verification" or "verification-with-user" => service.GenerateVerificationTokenAsync(userId, Email),
        "reset" => service.GeneratePasswordResetTokenAsync(userId, Email),
        "magic" => service.GenerateMagicLinkTokenAsync(userId, Email),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static async Task<TokenValidationResult> Consume(EmailVerificationService service, string kind, Guid userId, string token) => kind switch
    {
        "verification" => await service.VerifyEmailTokenAsync(token),
        "verification-with-user" => new(await service.VerifyEmailTokenAsync(userId, token), userId, Email),
        "reset" => await service.VerifyPasswordResetTokenAsync(token),
        "magic" => await service.VerifyMagicLinkTokenAsync(token),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed class SynchronizedReadCache : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        private readonly Barrier _barrier = new(4);
        private int _readCount;
        private int _synchronizationFailed;
        private bool _synchronize;
        public bool SynchronizationFailed => Volatile.Read(ref _synchronizationFailed) != 0;
        public void SynchronizeNextTokenReads() => _synchronize = true;
        public ICacheEntry CreateEntry(object key) => _inner.CreateEntry(key);
        public void Remove(object key) => _inner.Remove(key);
        public bool TryGetValue(object key, out object? value)
        {
            var found = _inner.TryGetValue(key, out value);
            if (_synchronize && key is string text && text.StartsWith(TokenPrefix, StringComparison.Ordinal) &&
                Interlocked.Increment(ref _readCount) <= 4 && !_barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
            {
                Interlocked.Exchange(ref _synchronizationFailed, 1);
            }
            return found;
        }
        public void Dispose()
        {
            _barrier.Dispose();
            _inner.Dispose();
        }
    }

    private sealed class SuspendedRemovalCache : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        public ManualResetEventSlim RemovalStarted { get; } = new();
        public ManualResetEventSlim AllowRemoval { get; } = new();
        public bool SuspendRemoval { get; set; }
        public ICacheEntry CreateEntry(object key) => _inner.CreateEntry(key);
        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);
        public void Remove(object key)
        {
            if (SuspendRemoval && key is string text && text.StartsWith(TokenPrefix, StringComparison.Ordinal))
            {
                RemovalStarted.Set();
                if (!AllowRemoval.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("The test did not release suspended token removal.");
                }
            }
            _inner.Remove(key);
        }
        public void Dispose()
        {
            AllowRemoval.Set();
            AllowRemoval.Dispose();
            RemovalStarted.Dispose();
            _inner.Dispose();
        }
    }
}

