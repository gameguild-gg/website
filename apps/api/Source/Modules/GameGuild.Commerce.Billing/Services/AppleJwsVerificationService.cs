using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Handles JWS (JSON Web Signature) decoding and X.509 certificate chain verification
///     for Apple App Store Server API signed payloads.
///     See: https://developer.apple.com/documentation/appstoreserverapi/jwstransaction
/// </summary>
public class AppleJwsVerificationService(
    ILogger<AppleJwsVerificationService> logger) : IAppleJwsVerificationService
{
    /// <inheritdoc />
    public AppleTransactionInfo? DecodeSignedTransaction(string signedTransaction)
    {
        try
        {
            var parts = signedTransaction.Split('.');
            if (parts.Length != 3)
            {
                logger.LogWarning("Invalid JWS format: expected 3 parts, got {Count}", parts.Length);
                return null;
            }

            // Decode header to get the certificate chain
            var headerJson = Base64UrlDecode(parts[0]);
            var header = JsonSerializer.Deserialize<AppleJwsHeader>(headerJson);

            if (header?.X5c == null || header.X5c.Length == 0)
            {
                logger.LogWarning("JWS header missing x5c certificate chain");
                return null;
            }

            // Verify the certificate chain against Apple's root CA
            if (!VerifyAppleCertificateChain(header.X5c))
            {
                logger.LogWarning("Apple certificate chain verification failed");
                return null;
            }

            // Extract the leaf certificate's public key for signature verification
            var leafCertBytes = Convert.FromBase64String(header.X5c[0]);
            using var leafCert = X509CertificateLoader.LoadCertificate(leafCertBytes);

            // Verify the JWS signature
            if (!VerifyJwsSignature(parts, leafCert, header.Alg))
            {
                logger.LogWarning("JWS signature verification failed");
                return null;
            }

            // Decode the verified payload
            var payloadJson = Base64UrlDecode(parts[1]);
            return JsonSerializer.Deserialize(payloadJson, AppleJsonContext.Default.AppleTransactionInfo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decode signed transaction");
            return null;
        }
    }

    /// <inheritdoc />
    public AppleNotificationPayload? DecodeSignedNotification(string signedPayload)
    {
        try
        {
            var parts = signedPayload.Split('.');
            if (parts.Length != 3)
            {
                logger.LogWarning("Invalid JWS format: expected 3 parts, got {Count}", parts.Length);
                return null;
            }

            // Decode header to get the certificate chain
            var headerJson = Base64UrlDecode(parts[0]);
            var header = JsonSerializer.Deserialize<AppleJwsHeader>(headerJson);

            if (header?.X5c == null || header.X5c.Length == 0)
            {
                logger.LogWarning("JWS header missing x5c certificate chain");
                return null;
            }

            // Verify the certificate chain against Apple's root CA
            if (!VerifyAppleCertificateChain(header.X5c))
            {
                logger.LogWarning("Apple certificate chain verification failed");
                return null;
            }

            // Extract the leaf certificate's public key for signature verification
            var leafCertBytes = Convert.FromBase64String(header.X5c[0]);
            using var leafCert = X509CertificateLoader.LoadCertificate(leafCertBytes);

            // Verify the JWS signature
            if (!VerifyJwsSignature(parts, leafCert, header.Alg))
            {
                logger.LogWarning("JWS signature verification failed");
                return null;
            }

            // Decode the verified payload
            var payloadJson = Base64UrlDecode(parts[1]);
            return JsonSerializer.Deserialize(payloadJson, AppleJsonContext.Default.AppleNotificationPayload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decode signed notification");
            return null;
        }
    }

    /// <summary>
    ///     Verifies the Apple certificate chain against the known Apple Root CA.
    ///     The chain should be: [0] Leaf → [1] Intermediate → [2] Root (Apple Root CA - G3)
    /// </summary>
    private bool VerifyAppleCertificateChain(string[] x5cChain)
    {
        if (x5cChain.Length < 2)
        {
            logger.LogWarning("Certificate chain too short: expected at least 2 certificates");
            return false;
        }

        try
        {
            // Build certificate chain
            var certificates = x5cChain
                .Select(certBase64 => X509CertificateLoader.LoadCertificate(
                    Convert.FromBase64String(certBase64)))
                .ToArray();

            // The leaf certificate should be issued by Apple
            var leafCert = certificates[0];

            // Verify certificate is not expired
            var now = SystemClock.UtcNow;
            if (now < leafCert.NotBefore || now > leafCert.NotAfter)
            {
                logger.LogWarning("Leaf certificate is expired or not yet valid. NotBefore={NotBefore}, NotAfter={NotAfter}",
                    leafCert.NotBefore, leafCert.NotAfter);
                return false;
            }

            // Build and validate certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            // Add intermediate certificates to the extra store
            foreach (var cert in certificates.Skip(1))
            {
                chain.ChainPolicy.ExtraStore.Add(cert);
            }

            // Build the chain
            var isValid = chain.Build(leafCert);

            // A partial chain means the extra-store certificates never linked to the leaf:
            // Build() alone returns true for an unrelated self-signed leaf under
            // AllowUnknownCertificateAuthority, so an explicit linkage failure must reject.
            if (chain.ChainStatus.Any(status => status.Status == X509ChainStatusFlags.PartialChain))
            {
                logger.LogWarning("Certificate chain incomplete: the presented intermediates did not issue the leaf");
                foreach (var cert in certificates)
                {
                    cert.Dispose();
                }

                return false;
            }

            if (!isValid)
            {
                foreach (var status in chain.ChainStatus)
                {
                    logger.LogWarning("Certificate chain status: {Status} - {StatusInformation}",
                        status.Status, status.StatusInformation);
                }
            }

            // Verify the issuer contains "Apple"
            if (!leafCert.Issuer.Contains("Apple", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Certificate issuer does not contain 'Apple': {Issuer}", leafCert.Issuer);
                return false;
            }

            // Clean up
            foreach (var cert in certificates)
            {
                cert.Dispose();
            }

            return isValid;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to verify certificate chain");
            return false;
        }
    }

    /// <summary>
    ///     Verifies the JWS signature using the leaf certificate's public key.
    /// </summary>
    private bool VerifyJwsSignature(string[] parts, X509Certificate2 certificate, string algorithm)
    {
        try
        {
            // Get the signing input (header.payload)
            var signingInput = $"{parts[0]}.{parts[1]}";
            var signingInputBytes = Encoding.UTF8.GetBytes(signingInput);

            // Decode the signature
            var signatureBytes = Base64UrlDecodeBytes(parts[2]);

            // Get the public key and verify
            using var ecdsa = certificate.GetECDsaPublicKey();
            if (ecdsa == null)
            {
                logger.LogWarning("Certificate does not have an ECDSA public key");
                return false;
            }

            // Determine hash algorithm based on JWS algorithm
            var hashAlgorithm = algorithm switch
            {
                "ES256" => HashAlgorithmName.SHA256,
                "ES384" => HashAlgorithmName.SHA384,
                "ES512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256
            };

            return ecdsa.VerifyData(signingInputBytes, signatureBytes, hashAlgorithm);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to verify JWS signature");
            return false;
        }
    }

    /// <summary>
    ///     Decodes a Base64Url-encoded string to bytes.
    /// </summary>
    private static byte[] Base64UrlDecodeBytes(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    ///     Decodes a Base64Url-encoded string.
    /// </summary>
    private static string Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }
}
