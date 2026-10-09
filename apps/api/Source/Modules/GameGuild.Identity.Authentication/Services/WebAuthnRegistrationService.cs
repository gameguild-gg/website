using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles WebAuthn/FIDO2 credential registration (attestation) flows.
/// </summary>
public class WebAuthnRegistrationService(
    IFido2 fido2,
    IWebAuthnCredentialRepository credentialRepository,
    ILogger<WebAuthnRegistrationService> logger) : IWebAuthnRegistrationService
{
    public async Task<WebAuthnRegistrationOptionsResult> BeginRegistrationAsync(
        Guid userId,
        string userEmail,
        string displayName,
        WebAuthnAuthenticatorType? preferredType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get existing credentials to exclude
            var existingCredentials = await credentialRepository
                .GetActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
            var excludeCredentials = existingCredentials
                .Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.CredentialId)))
                .ToList();

            // Create Fido2 user
            var user = new Fido2User
            {
                Id = userId.ToByteArray(),
                Name = userEmail,
                DisplayName = displayName
            };

            // Set authenticator selection criteria
            var authenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
                UserVerification = UserVerificationRequirement.Preferred
            };

            if (preferredType.HasValue)
            {
                authenticatorSelection.AuthenticatorAttachment = preferredType.Value switch
                {
                    WebAuthnAuthenticatorType.Platform => AuthenticatorAttachment.Platform,
                    WebAuthnAuthenticatorType.CrossPlatform => AuthenticatorAttachment.CrossPlatform,
                    _ => null
                };
            }

            // Generate registration options
            var options = fido2.RequestNewCredential(new RequestNewCredentialParams
            {
                User = user,
                ExcludeCredentials = excludeCredentials,
                AuthenticatorSelection = authenticatorSelection,
                AttestationPreference = AttestationConveyancePreference.None
            });

            // Store session for later verification
            var sessionId = WebAuthnSessionStore.Store(new WebAuthnSessionStore.WebAuthnSession
            {
                UserId = userId,
                Challenge = options.Challenge,
                RegistrationOptions = options
            });

            return new WebAuthnRegistrationOptionsResult
            {
                Success = true,
                SessionId = sessionId,
                Options = options,
                OptionsJson = options.ToJson()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to begin WebAuthn registration for user {UserId}", userId);
            return new WebAuthnRegistrationOptionsResult
            {
                Success = false,
                Error = "Failed to initiate WebAuthn registration"
            };
        }
    }

    public async Task<WebAuthnRegistrationResult> CompleteRegistrationAsync(
        Guid userId,
        string attestationResponse,
        string? friendlyName = null,
        bool isPasswordless = false,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse the attestation response
            var attestationResponseObj = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponse);
            if (attestationResponseObj == null)
            {
                return new WebAuthnRegistrationResult { Success = false, Error = "Invalid attestation response" };
            }

            // Find the session by searching for matching userId
            var session = WebAuthnSessionStore.FindByUser(userId, s => s.RegistrationOptions != null);
            if (session?.RegistrationOptions == null)
            {
                return new WebAuthnRegistrationResult { Success = false, Error = "Registration session not found or expired" };
            }

            // Verify the credential
            var credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponseObj,
                OriginalOptions = session.RegistrationOptions,
                IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                {
                    var existing = await credentialRepository.GetByCredentialIdAsync(
                        Convert.ToBase64String(args.CredentialId), ct).ConfigureAwait(false);
                    return existing == null;
                }
            }, cancellationToken);

            // Remove the session
            WebAuthnSessionStore.RemoveByUser(userId);

            // Determine authenticator type
            var authenticatorType = attestationResponseObj.Response.Transports?.Contains(AuthenticatorTransport.Internal) == true
                ? WebAuthnAuthenticatorType.Platform
                : WebAuthnAuthenticatorType.CrossPlatform;

            // Save the credential
            var webAuthnCredential = new UserWebAuthnCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CredentialId = Convert.ToBase64String(credential.Id),
                PublicKey = Convert.ToBase64String(credential.PublicKey),
                AaGuid = credential.AaGuid.ToString(),
                SignatureCounter = credential.SignCount,
                FriendlyName = friendlyName ?? GetDefaultFriendlyName(authenticatorType, userAgent),
                AuthenticatorType = authenticatorType,
                Transports = attestationResponseObj.Response.Transports != null
                    ? string.Join(",", attestationResponseObj.Response.Transports)
                    : null,
                IsPasswordless = isPasswordless,
                UserVerified = true,
                BackedUp = credential.IsBackedUp,
                RegisteredFromIp = ipAddress,
                RegisteredUserAgent = userAgent,
                IsActive = true
            };

            await credentialRepository.CreateAsync(webAuthnCredential, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "WebAuthn credential registered for user {UserId}, Credential: {CredentialId}",
                userId, webAuthnCredential.Id);

            return new WebAuthnRegistrationResult
            {
                Success = true,
                CredentialId = webAuthnCredential.Id,
                FriendlyName = webAuthnCredential.FriendlyName
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to complete WebAuthn registration for user {UserId}", userId);
            return new WebAuthnRegistrationResult
            {
                Success = false,
                Error = "Failed to complete WebAuthn registration"
            };
        }
    }

    private static string GetDefaultFriendlyName(WebAuthnAuthenticatorType type, string? userAgent)
    {
        if (userAgent != null)
        {
            if (userAgent.Contains("Windows"))
            {
                return type == WebAuthnAuthenticatorType.Platform ? "Windows Hello" : "Security Key";
            }

            if (userAgent.Contains("Mac"))
            {
                return type == WebAuthnAuthenticatorType.Platform ? "Touch ID" : "Security Key";
            }

            if (userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            {
                return "Face ID / Touch ID";
            }

            if (userAgent.Contains("Android"))
            {
                return type == WebAuthnAuthenticatorType.Platform ? "Android Biometric" : "Security Key";
            }
        }

        return type == WebAuthnAuthenticatorType.Platform ? "Built-in Authenticator" : "Security Key";
    }
}
