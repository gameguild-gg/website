using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication.UnitTests;

/// <summary>Admission-only double; existing password/risk unit scenarios retain their own assertions.</summary>
internal sealed class PasswordSignInAdmissionStub : IPasswordSignInAdmissionService
{
    public List<string> Identifiers { get; } = [];
    public AuthenticationTimingOrigin? Origin { get; private set; }
    public HttpContext? Context { get; private set; }
    public CancellationToken Cancellation { get; private set; }
    public UnauthorizedAccessException? Denial { get; set; }
    public bool LeaseDisposed { get; private set; }

    public Task<IAsyncDisposable> AdmitAsync(string identifier, HttpContext? context,
        AuthenticationTimingOrigin timingOrigin, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Identifiers.Add(identifier);
        Origin = timingOrigin;
        Context = context;
        Cancellation = cancellationToken;
        if (Denial is not null)
        {
            throw Denial;
        }
        return Task.FromResult<IAsyncDisposable>(new Lease(this));
    }

    private sealed class Lease(PasswordSignInAdmissionStub owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.LeaseDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
