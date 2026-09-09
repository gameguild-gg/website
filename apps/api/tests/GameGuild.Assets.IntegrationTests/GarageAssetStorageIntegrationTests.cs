using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Assets.IntegrationTests;

public sealed class GarageAssetStorageIntegrationTests
{
    private static readonly string Endpoint = Environment.GetEnvironmentVariable("S3_SERVICE_URL") ?? "http://localhost:3900";
    private static readonly string AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY") ?? "GK333333333333333333333333";
    private static readonly string SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY") ?? "2222222222222222222222222222222222222222222222222222222222222222";
    private static readonly string Region = Environment.GetEnvironmentVariable("S3_REGION") ?? "garage";
    private static readonly string BucketName = Environment.GetEnvironmentVariable("S3_BUCKET") ?? "assets";
    private static readonly string AdminEndpoint = Environment.GetEnvironmentVariable("GARAGE_ADMIN_URL") ?? "http://localhost:3903";
    private static readonly string AdminToken = Environment.GetEnvironmentVariable("GARAGE_ADMIN_TOKEN") ?? "development-garage-admin-token";

    [Fact]
    public async Task AssetStorageService_RoundTripsContentThroughLocalGarage()
    {
        await AssertGarageIsAvailableAsync();

        var options = Options.Create(new AssetStorageOptions
        {
            BucketName = BucketName,
            ServiceUrl = Endpoint,
            AccessKey = AccessKey,
            SecretKey = SecretKey,
            Region = Region,
            ForcePathStyle = true
        });

        using var s3Client = CreateGarageClient();
        await EnsureBucketAccessAsync();
        var storage = new AssetStorageService(s3Client, options);
        var payload = "garage-storage-smoke-" + Guid.NewGuid().ToString("N");
        var bytes = Encoding.UTF8.GetBytes(payload);
        var contentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        StorageUploadResult upload;
        await using (var uploadStream = new MemoryStream(bytes))
        {
            upload = await storage.UploadAsync(uploadStream, contentHash, "text/plain");
        }

        upload.BucketName.Should().Be(BucketName);
        upload.ObjectKey.Should().EndWith(".txt");

        (await storage.ExistsAsync(upload.BucketName, upload.ObjectKey)).Should().BeTrue();

        var metadata = await storage.GetMetadataAsync(upload.BucketName, upload.ObjectKey);
        metadata.Should().NotBeNull();
        metadata!.SizeBytes.Should().Be(bytes.Length);
        metadata.MimeType.Should().Be("text/plain");

        await using (var downloadStream = await storage.DownloadAsync(upload.BucketName, upload.ObjectKey))
        using (var reader = new StreamReader(downloadStream, Encoding.UTF8))
        {
            var downloaded = await reader.ReadToEndAsync();
            downloaded.Should().Be(payload);
        }

        var downloadUrl = await storage.GeneratePresignedUrlAsync(
            upload.BucketName,
            upload.ObjectKey,
            TimeSpan.FromMinutes(5));

        downloadUrl.Should().StartWith(Endpoint);

        using var http = new HttpClient();
        var presignedResponse = await http.GetAsync(downloadUrl);
        presignedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await presignedResponse.Content.ReadAsStringAsync()).Should().Be(payload);

        await storage.DeleteAsync(upload.BucketName, upload.ObjectKey);

        (await storage.ExistsAsync(upload.BucketName, upload.ObjectKey)).Should().BeFalse();
    }

    private static AmazonS3Client CreateGarageClient()
    {
        return new AmazonS3Client(
            AccessKey,
            SecretKey,
            new AmazonS3Config
            {
                ServiceURL = Endpoint,
                AuthenticationRegion = Region,
                ForcePathStyle = true,
                UseHttp = true
            });
    }

    private static async Task EnsureBucketAccessAsync()
    {
        using var admin = new HttpClient { BaseAddress = new Uri(AdminEndpoint) };
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken);

        using var createResponse = await admin.PostAsJsonAsync(
            "/v2/CreateBucket",
            new { globalAlias = BucketName });

        string? bucketId = null;
        if (createResponse.IsSuccessStatusCode)
        {
            using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
            bucketId = created.RootElement.GetProperty("id").GetString();
        }

        if (string.IsNullOrWhiteSpace(bucketId))
        {
            using var infoResponse = await admin.GetAsync($"/v2/GetBucketInfo?globalAlias={Uri.EscapeDataString(BucketName)}");
            infoResponse.EnsureSuccessStatusCode();
            using var existing = JsonDocument.Parse(await infoResponse.Content.ReadAsStringAsync());
            bucketId = existing.RootElement.GetProperty("id").GetString();
        }

        bucketId.Should().NotBeNullOrWhiteSpace();

        using var accessResponse = await admin.PostAsJsonAsync(
            "/v2/AllowBucketKey",
            new
            {
                accessKeyId = AccessKey,
                bucketId,
                permissions = new { read = true, write = true, owner = true }
            });
        accessResponse.EnsureSuccessStatusCode();
    }

    private static async Task AssertGarageIsAvailableAsync()
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        try
        {
            using var response = await http.GetAsync(Endpoint);
            response.StatusCode.Should().NotBe(0);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Local Garage is required for this integration test. Start it from web-development with: docker compose --profile storage up -d garage garage-init",
                ex);
        }
    }
}
