using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>Builds and verifies bounded evidence ZIPs. Verification trusts configured signing keys, never keys supplied by the archive.</summary>
public sealed class ComplianceArtifactBuilder(ComplianceEvidenceValidationEngine validation, ICryptographicSigningService signing)
{
    public static int MaximumArchiveBytes { get; } = 41943040;
    private const int MaximumEntries = 220;
    private const string FormatVersion = "compliance-evidence-v1";
    private const string Algorithm = "ECDSA-SHA256-P1363";

    public CompliancePackageArtifact Build(Guid packageId, Guid tenantId, Guid preparedByUserId, DateTime capturedAtUtc,
        ComplianceFrameworkTemplate template, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyList<ComplianceEvidenceDataset> datasets) =>
        Build(packageId, tenantId, preparedByUserId, capturedAtUtc, template, request, documents, datasets, CancellationToken.None);

    public CompliancePackageArtifact Build(Guid packageId, Guid tenantId, Guid preparedByUserId, DateTime capturedAtUtc,
        ComplianceFrameworkTemplate template, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyList<ComplianceEvidenceDataset> datasets,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(packageId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(preparedByUserId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(datasets);
        cancellationToken.ThrowIfCancellationRequested();
        ComplianceEvidenceValidationEngine.ValidateInputs(template, request, documents, datasets, capturedAtUtc);
        // Freeze mutable arrays and dictionaries before validating and sealing the capture.
        template = template with
        {
            Sources = template.Sources.ToArray(),
            Controls = template.Controls.Select(item => item with { AutomaticEvidence = item.AutomaticEvidence.ToArray(), RequiredDocumentTypes = item.RequiredDocumentTypes.ToArray() }).ToArray(),
            Documents = template.Documents.Select(item => item with { RequiredFields = item.RequiredFields.ToArray() }).ToArray()
        };
        request = request with { DocumentIds = request.DocumentIds.ToList(), Exclusions = request.Exclusions.Select(item => item with { }).ToList() };
        documents = documents.Select(item => item with
        {
            Content = item.Content.ToArray(), ControlIds = item.ControlIds.ToArray(),
            ValidationFields = new Dictionary<string, string>(item.ValidationFields, StringComparer.Ordinal)
        }).ToArray();
        datasets = datasets.Select(item => item with
        {
            Content = item.Content.ToArray(), ObservedDatesUtc = item.ObservedDatesUtc.ToArray(), ValidationErrors = item.ValidationErrors.ToArray()
        }).ToArray();
        var report = validation.Inspect(template, request, documents, datasets, capturedAtUtc, cancellationToken);
        var payloads = new SortedDictionary<string, Payload>(StringComparer.Ordinal)
        {
            ["request.json"] = new("application/json", CompliancePackagingEncoding.Serialize(request)),
            ["template.json"] = new("application/json", CompliancePackagingEncoding.Serialize(template)),
            ["validation.json"] = new("application/json", CompliancePackagingEncoding.Serialize(report)),
            ["evidence/collection.json"] = new("application/json", CompliancePackagingEncoding.Serialize(datasets.Select(item => new
            {
                item.Kind, item.Source, Path = ComplianceEvidenceValidationEngine.EvidencePath(item.Kind), item.RecordCount,
                item.FirstObservedUtc, item.LastObservedUtc, item.ObservedDatesUtc, item.ValidationErrors
            }))),
            ["index.json"] = new("application/json", CompliancePackagingEncoding.Serialize(report.Controls)),
            ["index.csv"] = new("text/csv", Encoding.UTF8.GetBytes(BuildIndexCsv(report)))
        };
        foreach (var dataset in datasets)
        {
            payloads.Add(ComplianceEvidenceValidationEngine.EvidencePath(dataset.Kind), new("application/json", dataset.Content));
        }
        foreach (var document in documents)
        {
            var extension = document.MediaType == "application/pdf" ? "pdf" : document.MediaType == "text/plain" ? "txt" : "json";
            payloads.Add($"documents/{document.Id:D}/content.{extension}", new(document.MediaType, document.Content));
            payloads.Add($"documents/{document.Id:D}/metadata.json", new("application/json", CompliancePackagingEncoding.Serialize(new
            {
                document.Id, document.TemplateId, document.Name, document.Type, document.MediaType, document.ContentSha256,
                document.SourceUri, document.ValidFromUtc, document.ValidUntilUtc, document.ControlIds,
                document.ValidationFields, document.Review, document.UploadedByUserId,
                document.ReviewedByUserId, document.ReviewedAtUtc, document.Revision
            })));
        }
        foreach (var file in ComplianceReviewerFormats.Build(template, documents, report))
        {
            var mediaType = file.Key.EndsWith(".json", StringComparison.Ordinal) ? "application/json" :
                file.Key.EndsWith(".txt", StringComparison.Ordinal) ? "text/plain" : "text/csv";
            payloads.Add(file.Key, new(mediaType, file.Value));
        }
        var entries = payloads.Select(item => new ComplianceArtifactEntry(item.Key, item.Value.MediaType,
            item.Value.Content.Length, CompliancePackagingEncoding.Hash(item.Value.Content))).ToArray();
        var manifest = new ComplianceArtifactManifest(FormatVersion, packageId, tenantId, preparedByUserId, capturedAtUtc,
            request.Name, request.PeriodStartUtc.UtcDateTime, request.PeriodEndUtc.UtcDateTime,
            CompliancePackagingEncoding.Hash(payloads["template.json"].Content), template, entries, report);
        var manifestBytes = CompliancePackagingEncoding.Serialize(manifest);
        var manifestHash = CompliancePackagingEncoding.Hash(manifestBytes);
        var keyId = signing.GetActiveKeyId();
        if (!IsSafeKeyId(keyId)) { throw new InvalidOperationException("The configured audit signing key identifier is invalid."); }
        var seal = new ComplianceArtifactSeal(Algorithm, keyId, manifestHash, signing.SignData(SigningStatement(keyId, manifestHash), keyId));
        payloads.Add("manifest.json", new("application/json", manifestBytes));
        payloads.Add("seal.json", new("application/json", CompliancePackagingEncoding.Serialize(seal)));
        if (payloads.Count > MaximumEntries || payloads.Values.Any(item => item.Content.Length > ComplianceEvidenceValidationEngine.MaximumDatasetBytes) ||
            payloads.Values.Sum(item => (long)item.Content.Length) > MaximumArchiveBytes - 65536)
        {
            throw new CompliancePackagingValidationException(new Dictionary<string, string[]> { ["Artifact"] = ["The signed artifact exceeds the entry or size limit."] });
        }
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var item in payloads)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.CreateEntry(item.Key, CompressionLevel.NoCompression);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = entry.Open();
                stream.Write(item.Value.Content);
            }
        }
        var zip = output.ToArray();
        if (zip.Length > MaximumArchiveBytes) { throw new InvalidOperationException("The ZIP exceeds the archive size limit."); }
        return new(zip, CompliancePackagingEncoding.Hash(zip), manifest, seal);
    }

    public ComplianceArtifactVerification Verify(byte[] zipContent, Guid expectedTenantId, Guid expectedPackageId) =>
        Verify(zipContent, expectedTenantId, expectedPackageId, CancellationToken.None);

    public ComplianceArtifactVerification Verify(byte[] zipContent, Guid expectedTenantId, Guid expectedPackageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(zipContent);
        cancellationToken.ThrowIfCancellationRequested();
        if (zipContent.Length == 0 || zipContent.Length > MaximumArchiveBytes || expectedTenantId == Guid.Empty || expectedPackageId == Guid.Empty)
        {
            return Invalid("The archive size or expected identity is invalid.");
        }
        try
        {
            using var archive = new ZipArchive(new MemoryStream(zipContent, false), ZipArchiveMode.Read);
            if (archive.Entries.Count is < 7 or > MaximumEntries ||
                archive.Entries.Any(entry => !IsSafePath(entry.FullName) || entry.Length > ComplianceEvidenceValidationEngine.MaximumDatasetBytes) ||
                archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count ||
                archive.Entries.Sum(entry => entry.Length) > MaximumArchiveBytes)
            {
                return Invalid("The ZIP contains unsafe, duplicate or oversized entries.");
            }
            var content = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long bytesRead = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = entry.Open();
                using var output = new MemoryStream();
                var buffer = new byte[8192];
                int read;
                while ((read = source.Read(buffer)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bytesRead += read;
                    if (bytesRead > MaximumArchiveBytes || output.Length + read > ComplianceEvidenceValidationEngine.MaximumDatasetBytes)
                    {
                        return Invalid("Decompressed evidence exceeds the size limit.");
                    }
                    output.Write(buffer, 0, read);
                }
                if (output.Length != entry.Length) { return Invalid("A ZIP entry length is inconsistent."); }
                content.Add(entry.FullName, output.ToArray());
            }
            if (!content.TryGetValue("manifest.json", out var manifestBytes) || !content.TryGetValue("seal.json", out var sealBytes))
            {
                return Invalid("The manifest or seal is missing.");
            }
            using var manifestJson = CompliancePackagingEncoding.Parse(manifestBytes);
            using var sealJson = CompliancePackagingEncoding.Parse(sealBytes);
            var manifest = manifestJson.RootElement.Deserialize<ComplianceArtifactManifest>(CompliancePackagingEncoding.JsonOptions);
            var seal = sealJson.RootElement.Deserialize<ComplianceArtifactSeal>(CompliancePackagingEncoding.JsonOptions);
            if (manifest is null || seal is null || manifest.FormatVersion != FormatVersion || manifest.TenantId != expectedTenantId ||
                manifest.PackageId != expectedPackageId || manifest.PreparedByUserId == Guid.Empty || manifest.Template is null || manifest.Validation is null ||
                manifest.Entries is null || manifest.Entries.Count != content.Count - 2 || seal.Algorithm != Algorithm ||
                !IsSafeKeyId(seal.KeyId) || string.IsNullOrEmpty(seal.Signature) || seal.Signature.Length > 256 ||
                seal.ManifestSha256 != CompliancePackagingEncoding.Hash(manifestBytes))
            {
                return Invalid("The manifest identity, format or seal is inconsistent.");
            }
            if (!signing.VerifySignature(SigningStatement(seal.KeyId, seal.ManifestSha256), seal.Signature, seal.KeyId))
            {
                return Invalid("The signature cannot be verified with a configured trusted key.");
            }
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.Entries)
            {
                if (entry is null || !IsSafePath(entry.Path) || entry.Path is "manifest.json" or "seal.json" || !listed.Add(entry.Path) ||
                    !content.TryGetValue(entry.Path, out var bytes) || entry.Length != bytes.Length || entry.Sha256 != CompliancePackagingEncoding.Hash(bytes))
                {
                    return Invalid("Evidence is missing, unlisted or differs from its signed hash.");
                }
            }
            if (!listed.IsSupersetOf(["request.json", "template.json", "validation.json", "evidence/collection.json", "index.json", "index.csv"]) ||
                !listed.IsSupersetOf(ComplianceReviewerFormats.RequiredPaths(manifest.Template.Id)) ||
                manifest.TemplateSha256 != CompliancePackagingEncoding.Hash(content["template.json"]) ||
                !content["template.json"].AsSpan().SequenceEqual(CompliancePackagingEncoding.Serialize(manifest.Template)) ||
                !content["validation.json"].AsSpan().SequenceEqual(CompliancePackagingEncoding.Serialize(manifest.Validation)))
            {
                return Invalid("The template, validation report or mandatory indexes do not match the signed manifest.");
            }
            return new(true, []);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException or
            ArgumentException or InvalidOperationException or CryptographicException)
        {
            return Invalid("The archive is malformed or the configured signing key cannot verify it.");
        }
    }

    private static string SigningStatement(string keyId, string hash) => $"{FormatVersion}\n{Algorithm}\n{keyId}\n{hash}";
    private static ComplianceArtifactVerification Invalid(string error) => new(false, [error]);
    private static bool IsSafeKeyId(string? keyId) => keyId is { Length: > 0 and <= 128 } &&
        keyId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    private static bool IsSafePath(string? path) => path is { Length: > 0 and <= 200 } &&
        path.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '/') &&
        path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");

    private static string BuildIndexCsv(CompliancePackageValidationReport report)
    {
        var output = new StringBuilder("controlId,status,evidencePaths,documentIds,gapCodes\r\n");
        foreach (var control in report.Controls)
        {
            var cells = new[] { control.ControlId, control.Status, string.Join(';', control.EvidencePaths),
                string.Join(';', control.DocumentIds), string.Join(';', control.Gaps.Select(item => item.Code)) };
            output.AppendJoin(',', cells.Select(CsvCell)).Append("\r\n");
        }
        return output.ToString();
    }

    internal static string CsvCell(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@') { value = "'" + value; }
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private sealed record Payload(string MediaType, byte[] Content);
}
