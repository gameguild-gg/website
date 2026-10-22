using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using GameGuild.Configuration.PresentationLayer.Authentication;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     SAML 2.0 service-provider federation for enterprise identity providers (issue #268).
///     The identity provider is fully described by configuration (entity ID, signing
///     certificates, endpoint URLs); no IdP metadata is fetched over the network, so the
///     trust base is exactly what the deployment supplies. Responses are validated
///     fail-closed: XML-DSig signatures must verify against at least one currently-valid
///     configured certificate (unsigned responses are refused), issuer/audience/destination
///     must match exactly, lifetimes are clock-skew-bounded, <c>InResponseTo</c> must
///     correlate with a single-use server-issued AuthnRequest (unless the provider explicitly
///     allows IdP-initiated SSO), and assertion IDs are single-use through
///     <see cref="ISamlProtocolStateStore" />.
/// </summary>
public sealed class SamlFederationService(
    ISamlProtocolStateStore protocolStateStore,
    AuthenticationOptions? authenticationOptions = null,
    ILogger<SamlFederationService>? logger = null
) : ISamlFederationService
{
    /// <summary>How long an issued AuthnRequest ID stays correlatable.</summary>
    public static readonly TimeSpan AuthnRequestLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Minimum retention for consumed assertion IDs (replay protection).</summary>
    public static readonly TimeSpan AssertionReplayFloor = TimeSpan.FromMinutes(5);

    private const string SamlpNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";
    private const string SamlNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
    private const string MetadataNamespace = "urn:oasis:names:tc:SAML:2.0:metadata";
    private const string StatusSuccess = "urn:oasis:names:tc:SAML:2.0:status:Success";
    private const string BearerConfirmationMethod = "urn:oasis:names:tc:SAML:2.0:cm:bearer";
    private const string HttpPostBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";

    public async Task<SamlSignInChallenge> BuildSignInRedirectAsync(string slug, string? relayState, CancellationToken cancellationToken = default)
    {
        var options = RequireEnabledProvider(slug);

        var requestId = "_" + Guid.NewGuid().ToString("N");
        var issueInstant = XmlConvert.ToString(SystemClock.UtcNow, XmlDateTimeSerializationMode.Utc);
        var authnRequest = new StringBuilder()
            .Append("<samlp:AuthnRequest xmlns:samlp=\"").Append(SamlpNamespace).Append("\"")
            .Append(" xmlns:saml=\"").Append(SamlNamespace).Append("\"")
            .Append(" ID=\"").Append(requestId).Append("\"")
            .Append(" Version=\"2.0\"")
            .Append(" IssueInstant=\"").Append(issueInstant).Append("\"")
            .Append(" Destination=\"").Append(Escape(options.IdpSignInServiceUrl!)).Append("\"")
            .Append(" AssertionConsumerServiceURL=\"").Append(Escape(options.AssertionConsumerServiceUrl!)).Append("\"")
            .Append(" ProtocolBinding=\"").Append(HttpPostBinding).Append("\">")
            .Append("<saml:Issuer>").Append(Escape(options.SpEntityId!)).Append("</saml:Issuer>");
        if (!string.IsNullOrWhiteSpace(options.AuthnContextClassRef))
        {
            authnRequest
                .Append("<samlp:RequestedAuthnContext Comparison=\"exact\">")
                .Append("<saml:AuthnContextClassRef>").Append(Escape(options.AuthnContextClassRef)).Append("</saml:AuthnContextClassRef>")
                .Append("</samlp:RequestedAuthnContext>");
        }

        authnRequest.Append("</samlp:AuthnRequest>");

        await protocolStateStore.SavePendingAuthnRequestAsync(slug, requestId, AuthnRequestLifetime, cancellationToken).ConfigureAwait(false);

        var effectiveRelayState = string.IsNullOrWhiteSpace(relayState) ? Guid.NewGuid().ToString("N") : relayState;
        var redirectUrl = $"{options.IdpSignInServiceUrl}" +
                          $"?SAMLRequest={Uri.EscapeDataString(DeflateEncode(authnRequest.ToString()))}" +
                          $"&RelayState={Uri.EscapeDataString(effectiveRelayState)}";

        logger?.LogInformation("SAML AuthnRequest issued for provider {Slug}", slug);
        return new SamlSignInChallenge { RedirectUrl = redirectUrl, RequestId = requestId, RelayState = effectiveRelayState };
    }

    public async Task<SamlFederatedIdentity> ConsumeAssertionAsync(string slug, string samlResponse, CancellationToken cancellationToken = default)
    {
        var options = RequireEnabledProvider(slug);

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(samlResponse);
        }
        catch (FormatException)
        {
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' is not valid Base64.");
        }

        var xml = Encoding.UTF8.GetString(decoded);
        if (xml.Contains("<!DOCTYPE", StringComparison.Ordinal) || xml.Contains("<!ENTITY", StringComparison.Ordinal))
        {
            // Fail closed: entity expansion has no legitimate role in a SAML response.
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' contains a DTD and is rejected.");
        }

        XmlDocument document;
        try
        {
            document = new XmlDocument { PreserveWhitespace = true };
            document.LoadXml(xml);
        }
        catch (XmlException)
        {
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' is malformed.");
        }

        var response = document.DocumentElement;
        if (response is null || response.LocalName != "Response" || response.NamespaceURI != SamlpNamespace)
        {
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' must be a samlp:Response document.");
        }

        if (response.SelectSingleNode("saml:EncryptedAssertion", Ns(document)) is not null)
        {
            throw new UnauthorizedAccessException($"SAML encrypted assertions are not supported for provider '{slug}'.");
        }

        var validCertificates = LoadValidCertificates(options, slug);
        if (validCertificates.Count == 0)
        {
            logger?.LogError("SAML provider {Slug} has no currently-valid signing certificate; failing closed", slug);
            throw new UnauthorizedAccessException($"SAML signing certificates for provider '{slug}' are expired.");
        }

        // Destination must be present and exact: an ACS-bound response aimed anywhere else
        // (or mirrored from another service) is refused.
        var destination = response.GetAttribute("Destination");
        if (!string.Equals(destination, options.AssertionConsumerServiceUrl, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' has a missing or mismatched Destination.");
        }

        var inResponseTo = response.GetAttribute("InResponseTo");
        if (string.IsNullOrEmpty(inResponseTo))
        {
            if (!options.AllowIdpInitiated)
            {
                throw new UnauthorizedAccessException($"SAML response for provider '{slug}' lacks InResponseTo correlation (IdP-initiated SSO is disabled).");
            }
        }
        else
        {
            var correlated = await protocolStateStore.ConsumePendingAuthnRequestAsync(slug, inResponseTo, cancellationToken).ConfigureAwait(false);
            if (!correlated)
            {
                throw new UnauthorizedAccessException($"SAML response for provider '{slug}' correlates with no outstanding AuthnRequest.");
            }
        }

        ValidateStatus(document, slug);
        ValidateIssuer(document, response, options, slug);

        var assertions = response.SelectNodes("saml:Assertion", Ns(document))!;
        if (assertions.Count != 1)
        {
            // Exactly one assertion: multiple assertions in one response are a classic
            // signature-wrapping vector, and no supported profile needs them.
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' must carry exactly one assertion.");
        }

        var assertion = (XmlElement)assertions[0]!;
        ValidateSignatures(document, response, assertion, validCertificates, slug);

        var assertionId = assertion.GetAttribute("ID");
        if (string.IsNullOrEmpty(assertionId))
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has no ID.");
        }

        var notOnOrAfter = ValidateConditions(document, assertion, options, slug);
        var providerKey = ValidateSubject(document, assertion, options, slug);
        var attributes = ReadAttributes(assertion, options);
        var authnContexts = ReadAuthnContextClassRefs(assertion);

        // Replay protection: the assertion ID is single-use for as long as the assertion
        // could still be inside its validity window.
        var replayRetention = TimeSpan.FromTicks(Math.Max(
            AssertionReplayFloor.Ticks,
            (notOnOrAfter + Skew(options) - SystemClock.UtcNow).Ticks));
        var reserved = await protocolStateStore.TryReserveAssertionIdAsync(slug, assertionId, replayRetention, cancellationToken).ConfigureAwait(false);
        if (!reserved)
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' was already consumed (replay).");
        }

        var identity = new SamlFederatedIdentity
        {
            Slug = slug,
            ProviderKey = providerKey,
            Email = attributes.Email,
            EmailVerified = options.TrustIdpEmailVerification,
            Name = attributes.Name,
            AuthnContextClassRefs = authnContexts,
            HasMfaProof = authnContexts.Any(context => options.MfaAuthnContextClassRefs.Contains(context, StringComparer.Ordinal)),
            TenantAllowlist = options.TenantIds
        };

        EnforceEmailDomainGate(options, identity);

        logger?.LogInformation(
            "SAML response validated for provider {Slug} (assertion {AssertionId}, InResponseTo {InResponseTo})",
            slug, assertionId, inResponseTo is null ? "<idp-initiated>" : inResponseTo);
        return identity;
    }

    public string BuildServiceProviderMetadata(string slug)
    {
        var options = RequireEnabledProvider(slug);

        var nameIdFormat = string.IsNullOrWhiteSpace(options.NameIdFormat)
            ? "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"
            : options.NameIdFormat!;

        return new StringBuilder()
            .Append("<md:EntityDescriptor xmlns:md=\"").Append(MetadataNamespace).Append("\"")
            .Append(" entityID=\"").Append(Escape(options.SpEntityId!)).Append("\">")
            .Append("<md:SPSSODescriptor AuthnRequestsSigned=\"false\" WantAssertionsSigned=\"true\" protocolSupportEnumeration=\"").Append(SamlpNamespace).Append("\">")
            .Append("<md:NameIDFormat>").Append(Escape(nameIdFormat)).Append("</md:NameIDFormat>")
            .Append("<md:AssertionConsumerService Binding=\"").Append(HttpPostBinding).Append("\"")
            .Append(" Location=\"").Append(Escape(options.AssertionConsumerServiceUrl!)).Append("\"")
            .Append(" Index=\"0\" IsDefault=\"true\"/>")
            .Append("</md:SPSSODescriptor>")
            .Append("</md:EntityDescriptor>")
            .ToString();
    }

    public string? BuildLogoutRedirectUrl(string slug, string? postLogoutRedirectUri)
    {
        var options = RequireEnabledProvider(slug);
        if (string.IsNullOrWhiteSpace(options.IdpSingleLogoutServiceUrl))
        {
            return null;
        }

        var requestId = "_" + Guid.NewGuid().ToString("N");
        var issueInstant = XmlConvert.ToString(SystemClock.UtcNow, XmlDateTimeSerializationMode.Utc);
        var logoutRequest = new StringBuilder()
            .Append("<samlp:LogoutRequest xmlns:samlp=\"").Append(SamlpNamespace).Append("\"")
            .Append(" xmlns:saml=\"").Append(SamlNamespace).Append("\"")
            .Append(" ID=\"").Append(requestId).Append("\"")
            .Append(" Version=\"2.0\"")
            .Append(" IssueInstant=\"").Append(issueInstant).Append("\"")
            .Append(" Destination=\"").Append(Escape(options.IdpSingleLogoutServiceUrl!)).Append("\">")
            .Append("<saml:Issuer>").Append(Escape(options.SpEntityId!)).Append("</saml:Issuer>")
            .Append("</samlp:LogoutRequest>");

        var url = $"{options.IdpSingleLogoutServiceUrl}?SAMLRequest={Uri.EscapeDataString(DeflateEncode(logoutRequest.ToString()))}";
        if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri))
        {
            url += $"&RelayState={Uri.EscapeDataString(postLogoutRedirectUri)}";
        }

        return url;
    }

    /// <inheritdoc />
    public IReadOnlyList<OidcDiscoveredProvider> FindProvidersForEmailDomain(string emailDomain)
    {
        if (string.IsNullOrWhiteSpace(emailDomain))
        {
            return [];
        }

        var normalized = NormalizeEmailDomain(emailDomain);
        var providers = authenticationOptions?.ExternalProviders?.Saml;
        if (providers is null)
        {
            return [];
        }

        return providers
            .Where(entry => entry.Value?.Enabled == true &&
                            entry.Value.EmailDomains.Any(domain =>
                                domain.Trim().TrimStart('@').Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => new OidcDiscoveredProvider
            {
                Slug = entry.Key,
                DisplayName = string.IsNullOrWhiteSpace(entry.Value.DisplayName) ? entry.Key : entry.Value.DisplayName!,
                Protocol = "saml"
            })
            .OrderBy(provider => provider.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── Provider registry (fail closed) ────────────────────────────────────────

    /// <summary>
    ///     Accepts either a bare domain or a full email address (the domain after the
    ///     last <c>@</c>), so both the handler's pre-extracted domain and a raw address
    ///     resolve to the same case-insensitive comparison key.
    /// </summary>
    private static string NormalizeEmailDomain(string emailDomain)
    {
        var trimmed = emailDomain.Trim();
        var atIndex = trimmed.LastIndexOf('@');
        if (atIndex >= 0 && atIndex < trimmed.Length - 1)
        {
            trimmed = trimmed[(atIndex + 1)..];
        }

        return trimmed.TrimStart('@').ToLowerInvariant();
    }

    private SamlProviderOptions RequireEnabledProvider(string slug)
    {
        var providers = authenticationOptions?.ExternalProviders?.Saml;
        var options = providers?.FirstOrDefault(entry => entry.Key.Equals(slug, StringComparison.OrdinalIgnoreCase)).Value;

        if (options is null)
        {
            throw new InvalidOperationException($"SAML federation provider '{slug}' is not configured.");
        }

        if (!options.Enabled)
        {
            throw new InvalidOperationException($"SAML federation provider '{slug}' is disabled.");
        }

        return options;
    }

    private static void EnforceEmailDomainGate(SamlProviderOptions options, SamlFederatedIdentity identity)
    {
        if (options.EmailDomains.Count == 0 || string.IsNullOrWhiteSpace(identity.Email))
        {
            return;
        }

        var atIndex = identity.Email.LastIndexOf('@');
        var domain = atIndex >= 0 && atIndex < identity.Email.Length - 1 ? identity.Email[(atIndex + 1)..] : string.Empty;
        if (options.EmailDomains.All(allowed =>
                !allowed.Trim().TrimStart('@').Equals(domain, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException($"Email domain '{domain}' is not served by SAML provider '{identity.Slug}'.");
        }
    }

    // ── Signature verification ─────────────────────────────────────────────────

    private static List<X509Certificate2> LoadValidCertificates(SamlProviderOptions options, string slug)
    {
        var skew = Skew(options);
        var now = SystemClock.UtcNow;
        var valid = new List<X509Certificate2>();
        foreach (var encoded in options.IdpSigningCertificates)
        {
            X509Certificate2 certificate;
            try
            {
                certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(encoded));
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
                continue;
            }

            if (certificate.NotBefore <= now + skew && certificate.NotAfter > now - skew)
            {
                valid.Add(certificate);
            }
        }

        return valid;
    }

    /// <summary>
    ///     Verifies the response and/or assertion XML-DSig signatures against the configured
    ///     certificates. At least one signature must be present and valid (unsigned responses
    ///     are refused); when both are present, both must verify. Each signature must anchor
    ///     its single reference to the signed element's own ID — a mismatched (wrapping)
    ///     reference is rejected even when the cryptographic signature itself is valid.
    /// </summary>
    private void ValidateSignatures(
        XmlDocument document,
        XmlElement response,
        XmlElement assertion,
        IReadOnlyList<X509Certificate2> validCertificates,
        string slug)
    {
        var responseSignature = response.SelectSingleNode("ds:Signature", Ns(document)) as XmlElement;
        var assertionSignature = assertion.SelectSingleNode("ds:Signature", Ns(document)) as XmlElement;

        if (responseSignature is null && assertionSignature is null)
        {
            logger?.LogWarning("SAML response for provider {Slug} arrived unsigned; refusing", slug);
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' is unsigned and was refused.");
        }

        if (responseSignature is not null && !VerifyAnchoredSignature(document, response, responseSignature, validCertificates))
        {
            throw new UnauthorizedAccessException($"SAML response signature for provider '{slug}' failed verification.");
        }

        if (assertionSignature is not null && !VerifyAnchoredSignature(document, assertion, assertionSignature, validCertificates))
        {
            throw new UnauthorizedAccessException($"SAML assertion signature for provider '{slug}' failed verification.");
        }
    }

    private bool VerifyAnchoredSignature(
        XmlDocument document,
        XmlElement signedElement,
        XmlElement signatureElement,
        IReadOnlyList<X509Certificate2> validCertificates)
    {
        SignedXml signedXml;
        try
        {
            signedXml = new SignedXml(signedElement);
            signedXml.LoadXml(signatureElement);
        }
        catch (CryptographicException)
        {
            return false;
        }

        if (signedXml.SignedInfo?.References.Count != 1)
        {
            return false;
        }

        // Signature wrapping defense: the (single) reference must point at the signed
        // element's own ID, and that ID must be unique in the document (safe element scan —
        // never interpolate the ID into an XPath expression).
        var id = signedElement.GetAttribute("ID");
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var reference = (Reference)signedXml.SignedInfo.References[0]!;
        if (!string.Equals(reference.Uri, "#" + id, StringComparison.Ordinal))
        {
            return false;
        }

        var idOccurrences = 0;
        foreach (XmlElement element in document.GetElementsByTagName("*"))
        {
            if (element.GetAttribute("ID") == id && ++idOccurrences > 1)
            {
                return false;
            }
        }

        return validCertificates.Any(certificate => signedXml.CheckSignature(certificate, verifySignatureOnly: true));
    }

    // ── Assertion validation ───────────────────────────────────────────────────

    private static void ValidateStatus(XmlDocument document, string slug)
    {
        var statusCode = document.SelectSingleNode("/samlp:Response/samlp:Status/samlp:StatusCode", Ns(document)) as XmlElement;
        if (statusCode is null || !string.Equals(statusCode.GetAttribute("Value"), StatusSuccess, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"SAML response for provider '{slug}' does not report success.");
        }
    }

    private static void ValidateIssuer(XmlDocument document, XmlElement response, SamlProviderOptions options, string slug)
    {
        var responseIssuer = response.SelectSingleNode("saml:Issuer", Ns(document))?.InnerText?.Trim();
        if (responseIssuer is not null && !string.Equals(responseIssuer, options.IdpEntityId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"SAML response issuer for provider '{slug}' does not match the configured identity provider.");
        }

        var assertionIssuer = response.SelectSingleNode("saml:Assertion/saml:Issuer", Ns(document))?.InnerText?.Trim();
        if (!string.Equals(assertionIssuer, options.IdpEntityId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"SAML assertion issuer for provider '{slug}' does not match the configured identity provider.");
        }
    }

    private static DateTimeOffset ValidateConditions(
        XmlDocument document,
        XmlElement assertion,
        SamlProviderOptions options,
        string slug)
    {
        var conditions = assertion.SelectSingleNode("saml:Conditions", Ns(document)) as XmlElement
            ?? throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has no Conditions.");

        var now = SystemClock.UtcNow;
        var skew = Skew(options);

        var notBeforeRaw = conditions.GetAttribute("NotBefore");
        if (notBeforeRaw.Length > 0)
        {
            var notBefore = ParseSamlDateTime(notBeforeRaw, slug);
            if (notBefore - skew > now)
            {
                throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' is not yet valid (NotBefore).");
            }
        }

        var notOnOrAfterRaw = conditions.GetAttribute("NotOnOrAfter");
        if (notOnOrAfterRaw.Length == 0)
        {
            // Fail closed: an assertion without an upper validity bound is replayable forever.
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has no NotOnOrAfter.");
        }

        var notOnOrAfter = ParseSamlDateTime(notOnOrAfterRaw, slug);
        if (notOnOrAfter + skew <= now)
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has expired (NotOnOrAfter).");
        }

        var audiences = conditions.SelectNodes("saml:AudienceRestriction/saml:Audience", Ns(document))!;
        if (audiences.Count == 0)
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' carries no audience restriction.");
        }

        for (var index = 0; index < audiences.Count; index++)
        {
            if (string.Equals(audiences[index]!.InnerText.Trim(), options.SpEntityId, StringComparison.Ordinal))
            {
                return notOnOrAfter;
            }
        }

        throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' is not addressed to this service provider (audience mismatch).");
    }

    private static string ValidateSubject(XmlDocument document, XmlElement assertion, SamlProviderOptions options, string slug)
    {
        var now = SystemClock.UtcNow;
        var skew = Skew(options);

        var nameId = assertion.SelectSingleNode("saml:Subject/saml:NameID", Ns(document)) as XmlElement
            ?? throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has no subject NameID.");

        var nameIdFormat = nameId.GetAttribute("Format");
        if (!string.IsNullOrWhiteSpace(options.NameIdFormat) &&
            !string.Equals(nameIdFormat, options.NameIdFormat, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' carries an unexpected NameID format.");
        }

        var bearer = assertion.SelectSingleNode($"saml:Subject/saml:SubjectConfirmation[@Method='{BearerConfirmationMethod}']", Ns(document)) as XmlElement
            ?? throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has no bearer subject confirmation.");

        var confirmationData = bearer.SelectSingleNode("saml:SubjectConfirmationData", Ns(document)) as XmlElement
            ?? throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has no subject confirmation data.");

        var recipient = confirmationData.GetAttribute("Recipient");
        if (!string.Equals(recipient, options.AssertionConsumerServiceUrl, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has a missing or mismatched Recipient.");
        }

        var notOnOrAfterRaw = confirmationData.GetAttribute("NotOnOrAfter");
        if (notOnOrAfterRaw.Length == 0)
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has no subject-confirmation NotOnOrAfter.");
        }

        if (ParseSamlDateTime(notOnOrAfterRaw, slug) + skew <= now)
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has an expired bearer confirmation.");
        }

        var subject = nameId.InnerText.Trim();
        if (subject.Length == 0)
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' has an empty NameID.");
        }

        return subject;
    }

    private static (string? Email, string? Name) ReadAttributes(XmlElement assertion, SamlProviderOptions options)
    {
        string? ReadMapped(string logicalName)
        {
            var attributeName = options.ClaimMapping.TryGetValue(logicalName, out var mapped) ? mapped : logicalName;
            var attributes = assertion.SelectNodes("saml:AttributeStatement/saml:Attribute", Ns(assertion.OwnerDocument!))!;
            for (var index = 0; index < attributes.Count; index++)
            {
                if (!string.Equals(((XmlElement)attributes[index]!).GetAttribute("Name"), attributeName, StringComparison.Ordinal))
                {
                    continue;
                }

                var values = attributes[index]!.SelectNodes("saml:AttributeValue", Ns(assertion.OwnerDocument!))!;
                for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
                {
                    var value = values[valueIndex]!.InnerText.Trim();
                    if (value.Length > 0)
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        return (ReadMapped("email"), ReadMapped("name"));
    }

    private static IReadOnlyList<string> ReadAuthnContextClassRefs(XmlElement assertion) =>
        assertion
            .SelectNodes("saml:AuthnStatement/saml:AuthnContext/saml:AuthnContextClassRef", Ns(assertion.OwnerDocument!))!
            .Cast<XmlNode>()
            .Select(node => node.InnerText.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static TimeSpan Skew(SamlProviderOptions options) => TimeSpan.FromSeconds(options.AllowedClockSkewSeconds);

    private static DateTimeOffset ParseSamlDateTime(string value, string slug)
    {
        try
        {
            return XmlConvert.ToDateTimeOffset(value);
        }
        catch (FormatException)
        {
            throw new UnauthorizedAccessException($"SAML assertion for provider '{slug}' carries an unparseable instant '{value}'.");
        }
    }

    private static XmlNamespaceManager Ns(XmlDocument document)
    {
        var manager = new XmlNamespaceManager(document.NameTable);
        manager.AddNamespace("samlp", SamlpNamespace);
        manager.AddNamespace("saml", SamlNamespace);
        manager.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
        return manager;
    }

    private static string DeflateEncode(string payload)
    {
        using var compressed = new MemoryStream();
        using (var deflater = new DeflateStream(compressed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            deflater.Write(Encoding.UTF8.GetBytes(payload), 0, payload.Length);
        }

        return Convert.ToBase64String(compressed.ToArray());
    }

    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? value;
}
