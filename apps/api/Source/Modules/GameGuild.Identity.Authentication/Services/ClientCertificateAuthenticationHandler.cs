using System.Formats.Asn1;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using GameGuild.Configuration.PresentationLayer.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Authentication handler for X.509 client-certificate (mTLS) authentication.
/// </summary>
/// <remarks>
///     <para>
///         SECURITY: fail-closed semantics. A client certificate authenticates only when
///         (1) the scheme is configured with a non-empty CA allowlist, (2) the certificate
///         chains to that allowlist and carries the client-authentication use, and
///         (3) the certificate (thumbprint or SPKI key pin) is bound to an active,
///         unlocked, unexpired service account that allows the caller IP. Validated
///         certificates authenticate as <c>ActorKind.Service</c>.
///     </para>
/// </remarks>
public sealed class ClientCertificateAuthenticationHandler : AuthenticationHandler<ClientCertificateAuthenticationOptions>
{
    /// <summary>OID for the TLS client-authentication extended key usage.</summary>
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    private readonly IApplicationDbContext _dbContext;
    private readonly IAuthenticationAuditEventSink? _auditEventSink;

    public ClientCertificateAuthenticationHandler(
        IOptionsMonitor<ClientCertificateAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApplicationDbContext dbContext)
        : this(options, logger, encoder, dbContext, null)
    {
    }

    public ClientCertificateAuthenticationHandler(
        IOptionsMonitor<ClientCertificateAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApplicationDbContext dbContext,
        IAuthenticationAuditEventSink? auditEventSink)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
        _auditEventSink = auditEventSink;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // SECURITY: fail closed when the CA allowlist is unconfigured. Options validation
        // should already prevent this at startup; this is defense in depth.
        if (Options.TrustedCertificateAuthorities.Count == 0)
        {
            return await FailAsync(
                "Client certificate authentication is not configured with a CA allowlist.",
                "CertificateAllowlistUnconfigured").ConfigureAwait(false);
        }

        var clientCertificate = Context.Connection.ClientCertificate;
        if (clientCertificate is null)
        {
            // No certificate negotiated; let other schemes authenticate the request.
            return AuthenticateResult.NoResult();
        }

        if (!IsChainTrusted(clientCertificate))
        {
            return await FailAsync(
                "Client certificate is not issued by a trusted CA.",
                "CertificateChainUntrusted").ConfigureAwait(false);
        }

        if (!IsClientAuthenticationUsageAllowed(clientCertificate))
        {
            return await FailAsync(
                "Client certificate does not allow the client-authentication use.",
                "CertificateUseNotAllowed").ConfigureAwait(false);
        }

        var thumbprint = ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(clientCertificate);
        var spkiSha256 = ClientCertificateAuthenticationUtilities.ComputeSpkiSha256Hex(clientCertificate);

        List<ServiceAccount> matches;
        try
        {
            matches = await _dbContext.Set<ServiceAccount>()
                .Where(account => account.CertificateThumbprint == thumbprint
                                  || account.CertificateSpkiSha256 == spkiSha256)
                .ToListAsync(Context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error during client certificate authentication lookup");
            return await FailAsync("Authentication error", "CertificateAuthenticationError").ConfigureAwait(false);
        }

        // SECURITY: ambiguous bindings (same certificate bound to more than one account)
        // fail closed instead of picking a winner.
        if (matches.Count > 1)
        {
            Logger.LogWarning(
                "Client certificate {Thumbprint} is bound to {Count} service accounts; denying",
                thumbprint, matches.Count);
            return await FailAsync(
                "Client certificate binding is ambiguous.",
                "CertificateBindingAmbiguous").ConfigureAwait(false);
        }

        var serviceAccount = matches.FirstOrDefault();
        if (serviceAccount is null)
        {
            Logger.LogWarning("Client certificate {Thumbprint} is not bound to any service account", thumbprint);
            return await FailAsync(
                "Client certificate is not bound to a service account.",
                "CertificateNotBound").ConfigureAwait(false);
        }

        if (!serviceAccount.CanAuthenticate)
        {
            Logger.LogWarning(
                "Authentication failed: service account {ServiceAccountId} cannot authenticate (IsActive={IsActive}, IsLocked={IsLocked}, Expired={Expired})",
                serviceAccount.Id, serviceAccount.IsActive, serviceAccount.IsLocked,
                serviceAccount.ExpiresAt.HasValue && serviceAccount.ExpiresAt <= SystemClock.UtcNow);
            return await FailAsync(
                "Service account cannot authenticate.",
                "ServiceAccountInactive", serviceAccount).ConfigureAwait(false);
        }

        var clientIp = Context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(serviceAccount.AllowedIpAddresses) && !IsIpAllowed(clientIp, serviceAccount.AllowedIpAddresses))
        {
            Logger.LogWarning(
                "Authentication failed: IP {IpAddress} not in allowed list for service account {ServiceAccountId}",
                clientIp, serviceAccount.Id);
            serviceAccount.RecordFailedAuthentication();
            await SaveAccountAsync(serviceAccount).ConfigureAwait(false);
            return await FailAsync(
                "Client certificate not authorized from this IP address.",
                "ServiceAccountIpNotAllowed", serviceAccount).ConfigureAwait(false);
        }

        serviceAccount.RecordSuccessfulAuthentication(clientIp);
        await SaveAccountAsync(serviceAccount).ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new("sub", serviceAccount.Id.ToString()),
            new("client_id", serviceAccount.ClientId),
            new("service_name", serviceAccount.Name),
            new("tenant_id", serviceAccount.TenantId?.ToString() ?? string.Empty),
            new("actor_kind", "Service"),
            new("grant_type", "client_credentials"),
            new("auth_method", "client_certificate"),
            new("cert_thumbprint", thumbprint)
        };

        foreach (var scope in serviceAccount.GetScopesSet())
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation(
            "Client certificate authentication successful for service account {ServiceAccountId}, thumbprint {Thumbprint}",
            serviceAccount.Id, thumbprint);

        await RecordAuthenticationAuditEventAsync(success: true, serviceAccount).ConfigureAwait(false);

        return AuthenticateResult.Success(ticket);
    }

    private bool IsChainTrusted(X509Certificate2 certificate)
    {
        using var chain = new X509Chain
        {
            ChainPolicy =
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = Options.CheckCertificateRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.NoFlag,
                VerificationTime = DateTime.UtcNow
            }
        };

        foreach (var authority in Options.TrustedCertificateAuthorities)
        {
            chain.ChainPolicy.CustomTrustStore.Add(authority);
        }

        return chain.Build(certificate);
    }

    private static bool IsClientAuthenticationUsageAllowed(X509Certificate2 certificate)
    {
        var enhancedKeyUsages = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (enhancedKeyUsages is null)
        {
            // No EKU extension restricts the certificate; accept it (matches the
            // platform certificate-authentication ValidateCertificateUse default).
            return true;
        }

        foreach (var oid in enhancedKeyUsages.EnhancedKeyUsages)
        {
            if (string.Equals(oid.Value, ClientAuthenticationOid, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIpAllowed(string? ipAddress, string allowedIpAddresses)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return false;
        }

        var allowed = allowedIpAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed.Any(entry => entry.Equals(ipAddress, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SaveAccountAsync(ServiceAccount serviceAccount)
    {
        try
        {
            await _dbContext.SaveChangesAsync(Context.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Could not persist service account authentication bookkeeping");
        }
    }

    private async Task<AuthenticateResult> FailAsync(
        string message,
        string auditReason,
        ServiceAccount? serviceAccount = null)
    {
        await RecordAuthenticationAuditEventAsync(false, serviceAccount, auditReason).ConfigureAwait(false);
        return AuthenticateResult.Fail(message);
    }

    private async Task RecordAuthenticationAuditEventAsync(
        bool success,
        ServiceAccount? serviceAccount = null,
        string? auditReason = null)
    {
        if (_auditEventSink is null)
        {
            return;
        }

        try
        {
            await _auditEventSink.RecordAsync(new AuthenticationAuditEvent(
                success ? "Authentication.Succeeded" : "Authentication.Failed",
                serviceAccount?.Id,
                success,
                "ClientCertificate",
                Context.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                TenantId: serviceAccount?.TenantId,
                ErrorMessage: auditReason),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Could not record client certificate authentication audit event");
        }
    }
}

/// <summary>
///     Options for X.509 client-certificate authentication.
/// </summary>
public class ClientCertificateAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ClientCertificate";

    /// <summary>
    ///     CA allowlist for client-certificate chain validation. Fail closed: the scheme
    ///     rejects every certificate while the allowlist is empty.
    /// </summary>
    public X509Certificate2Collection TrustedCertificateAuthorities { get; } = [];

    /// <summary>
    ///     Whether certificate revocation is checked online during chain validation.
    /// </summary>
    public bool CheckCertificateRevocation { get; set; }

    public override void Validate()
    {
        base.Validate();

        if (TrustedCertificateAuthorities.Count == 0)
        {
            throw new InvalidOperationException(
                "Client certificate authentication requires at least one trusted CA certificate in the allowlist.");
        }
    }
}

/// <summary>
///     Helpers for normalizing and hashing client certificates.
/// </summary>
public static class ClientCertificateAuthenticationUtilities
{
    /// <summary>
    ///     Returns the certificate's SHA-1 thumbprint normalized to uppercase hex without separators.
    /// </summary>
    public static string GetNormalizedThumbprint(X509Certificate2 certificate)
    {
        var thumbprint = certificate.Thumbprint ?? string.Empty;
        Span<char> compact = stackalloc char[thumbprint.Length];
        var length = 0;
        foreach (var character in thumbprint)
        {
            if (character is ':' or ' ' or '\t' or '\r' or '\n')
            {
                continue;
            }

            compact[length++] = char.ToUpperInvariant(character);
        }

        return new string(compact[..length]);
    }

    /// <summary>
    ///     Computes the SHA-256 hex hash of the certificate's Subject Public Key Info (SPKI).
    ///     The SPKI pin survives certificate re-issuance with the same key.
    /// </summary>
    public static string ComputeSpkiSha256Hex(X509Certificate2 certificate)
    {
        var der = certificate.Export(X509ContentType.Cert);
        var certificateReader = new AsnReader(der, AsnEncodingRules.DER);
        var outer = certificateReader.ReadSequence();
        var tbsCertificate = outer.ReadSequence();

        var versionTag = new Asn1Tag(TagClass.ContextSpecific, 0);
        if (tbsCertificate.PeekTag().HasSameClassAndValue(versionTag))
        {
            tbsCertificate.ReadSequence(versionTag);
        }

        tbsCertificate.ReadInteger();     // serialNumber
        tbsCertificate.ReadSequence();    // signature AlgorithmIdentifier
        tbsCertificate.ReadSequence();    // issuer
        tbsCertificate.ReadSequence();    // validity
        tbsCertificate.ReadSequence();    // subject
        var subjectPublicKeyInfo = tbsCertificate.ReadEncodedValue().Span.ToArray();

        return Convert.ToHexString(SHA256.HashData(subjectPublicKeyInfo));
    }
}

/// <summary>
///     Extension methods for X.509 client-certificate authentication.
/// </summary>
public static class ClientCertificateAuthenticationExtensions
{
    /// <summary>
    ///     Registers the client-certificate scheme configured from <see cref="ClientCertificateAuthenticationSettings"/>.
    ///     Invalid or empty CA allowlist entries fail closed at registration time.
    /// </summary>
    public static AuthenticationBuilder AddClientCertificateAuthentication(
        this AuthenticationBuilder builder,
        ClientCertificateAuthenticationSettings settings,
        Action<ClientCertificateAuthenticationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Parse the allowlist eagerly so an unparseable CA entry fails closed at
        // registration time instead of surfacing as a deferred options failure on
        // the first authentication attempt.
        var trustedCertificateAuthorities = new X509Certificate2Collection();
        foreach (var pem in settings.TrustedCaCertificates)
        {
            X509Certificate2 certificate;
            try
            {
                certificate = X509Certificate2.CreateFromPem(pem);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Client certificate authentication trusted CA entries must be valid PEM certificates.", exception);
            }

            trustedCertificateAuthorities.Add(certificate);
        }

        return builder.AddClientCertificateAuthentication(settings.SchemeName, options =>
        {
            options.CheckCertificateRevocation = settings.CheckCertificateRevocation;
            foreach (var certificateAuthority in trustedCertificateAuthorities)
            {
                options.TrustedCertificateAuthorities.Add(certificateAuthority);
            }

            configure?.Invoke(options);
        });
    }

    public static AuthenticationBuilder AddClientCertificateAuthentication(
        this AuthenticationBuilder builder,
        Action<ClientCertificateAuthenticationOptions>? configure) =>
        builder.AddClientCertificateAuthentication(ClientCertificateAuthenticationOptions.SchemeName, configure);

    public static AuthenticationBuilder AddClientCertificateAuthentication(
        this AuthenticationBuilder builder,
        string schemeName,
        Action<ClientCertificateAuthenticationOptions>? configure)
    {
        return builder.AddScheme<ClientCertificateAuthenticationOptions, ClientCertificateAuthenticationHandler>(
            schemeName,
            options =>
            {
                configure?.Invoke(options);
                options.Validate();
            });
    }
}
