using FluentAssertions;
using GameGuild.Assets.Deduplication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ImageMagick;
using Xunit;

namespace GameGuild.Assets.UnitTests.Services;

public class DeduplicationServiceTests
{
    private readonly Mock<IAssetContentRepository> _contentRepositoryMock;
    private readonly DeduplicationOptions _options;
    private readonly DeduplicationService _service;

    public DeduplicationServiceTests()
    {
        _contentRepositoryMock = new Mock<IAssetContentRepository>();
        Mock<ILogger<DeduplicationService>> loggerMock = new Mock<ILogger<DeduplicationService>>();
        _options = new DeduplicationOptions { Enabled = true, EnablePerceptualHashing = true };
        var optionsMock = Options.Create(_options);
        _service = new DeduplicationService(_contentRepositoryMock.Object, optionsMock, loggerMock.Object);
    }

    [Fact]
    public async Task ComputeContentHashAsync_ComputesSHA256Hash()
    {
        // Arrange
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

        // Act
        var hash = await _service.ComputeContentHashAsync(content);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64); // SHA-256 produces 64 hex characters
        hash.Should().MatchRegex("^[a-f0-9]+$"); // Only lowercase hex
    }

    [Fact]
    public async Task ComputeContentHashAsync_ResetsStreamPosition()
    {
        // Arrange
        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));

        // Act
        await _service.ComputeContentHashAsync(content);

        // Assert
        content.Position.Should().Be(0);
    }

    [Fact]
    public async Task ComputeContentHashAsync_ProducesSameHashForSameContent()
    {
        // Arrange
        var content1 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("identical content"));
        var content2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("identical content"));

        // Act
        var hash1 = await _service.ComputeContentHashAsync(content1);
        var hash2 = await _service.ComputeContentHashAsync(content2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task ComputeContentHashAsync_ProducesDifferentHashForDifferentContent()
    {
        // Arrange
        var content1 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content one"));
        var content2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content two"));

        // Act
        var hash1 = await _service.ComputeContentHashAsync(content1);
        var hash2 = await _service.ComputeContentHashAsync(content2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_ReturnsNull_WhenDisabled()
    {
        // Arrange
        _options.EnablePerceptualHashing = false;
        var content = new MemoryStream();

        // Act
        var hash = await _service.ComputePerceptualHashAsync(content, "image/png");

        // Assert
        hash.Should().BeNull();
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_ReturnsNull_ForNonImageMimeType()
    {
        // Arrange
        var content = new MemoryStream();

        // Act
        var hash = await _service.ComputePerceptualHashAsync(content, "application/pdf");

        // Assert
        hash.Should().BeNull();
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_DecodesBenignPngAndPreservesDeterminismAndStreamPosition()
    {
        using var content = CreateBenignPng(false);
        var first = await _service.ComputePerceptualHashAsync(content, "image/png");
        first.Should().NotBeNullOrEmpty();
        first.Should().MatchRegex("^[a-f0-9]{16}$");
        first.Should().Be("f0f0f0f0f0f0f0f0");
        content.Position.Should().Be(0);

        var repeated = await _service.ComputePerceptualHashAsync(content, "image/png");
        repeated.Should().Be(first);
        content.Position.Should().Be(0);
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_DistinguishesMirroredImagePatterns()
    {
        using var left = CreateBenignPng(false);
        using var right = CreateBenignPng(true);
        var first = await _service.ComputePerceptualHashAsync(left, "image/png");
        var second = await _service.ComputePerceptualHashAsync(right, "image/png");
        first.Should().NotBeNullOrEmpty();
        second.Should().NotBeNullOrEmpty().And.NotBe(first);
        left.Position.Should().Be(0);
        right.Position.Should().Be(0);
    }

    private static MemoryStream CreateBenignPng(bool mirror)
        => CreateBenignRaster(mirror, MagickFormat.Png);

    private static MemoryStream CreateBenignRaster(bool mirror, MagickFormat format, uint size = 8)
    {
        using var image = new MagickImage(MagickColors.Black, size, size);
        image.ColorType = ColorType.TrueColor;
        image.Depth = 8;
        image.Quality = 100;
        using var pixels = image.GetPixels();
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var value = (column < size / 2) == mirror ? byte.MaxValue : byte.MinValue;
                pixels.SetPixel(column, row, new[] { value, value, value });
            }
        }
        var stream = new MemoryStream();
        image.Write(stream, format);
        stream.Position = 0;
        return stream;
    }

    [Theory]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.Tga)]
    [InlineData(MagickFormat.Pbm)]
    [InlineData(MagickFormat.Qoi)]
    [InlineData(MagickFormat.Ico)]
    public async Task ComputePerceptualHashAsync_PreservesCanonicalHashAcrossRasterFormats(MagickFormat format)
    {
        using var content = CreateBenignRaster(false, format);
        var hash = await _service.ComputePerceptualHashAsync(content, "image/raster");
        hash.Should().Be("f0f0f0f0f0f0f0f0");
        content.Position.Should().Be(0);
    }

    [Theory]
    [InlineData(16u)]
    [InlineData(32u)]
    [InlineData(64u)]
    public async Task ComputePerceptualHashAsync_RetainsHashWhenResizingTheSamePattern(uint size)
    {
        using var content = CreateBenignRaster(false, MagickFormat.Png, size);
        content.Position = 3;
        var hash = await _service.ComputePerceptualHashAsync(content, "image/png");
        hash.Should().Be("f0f0f0f0f0f0f0f0");
        content.Position.Should().Be(0);
    }

    [Theory]
    [InlineData("corrupt image data")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.test/image.png'/></svg>")]
    [InlineData("%PDF-1.7")]
    [InlineData("push graphic-context")]
    public async Task ComputePerceptualHashAsync_RejectsMalformedAndNonRasterInput(string data)
    {
        using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
        var hash = await _service.ComputePerceptualHashAsync(content, "image/png");
        hash.Should().BeNull();
        content.Position.Should().Be(0);
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_RetainsTheOptionalHashCancellationContract()
    {
        using var content = CreateBenignPng(false);
        content.Position = 3;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var hash = await _service.ComputePerceptualHashAsync(content, "image/png", cancellation.Token);
        hash.Should().BeNull();
        content.Position.Should().Be(0);
    }

    [Theory]
    [InlineData(MagickFormat.Png, false)]
    [InlineData(MagickFormat.Jpeg, false)]
    [InlineData(MagickFormat.Tiff, false)]
    [InlineData(MagickFormat.Jpeg, true)]
    [InlineData(MagickFormat.Tiff, true)]
    public async Task ComputePerceptualHashAsync_UsesRec709RgbBrightness(MagickFormat format, bool cmyk)
    {
        using var content = CreateColorRaster(format, cmyk);
        if (cmyk)
        {
            using var decoded = new MagickImage(content);
            decoded.ColorSpace.Should().Be(ColorSpace.CMYK);
            content.Position = 0;
        }

        var hash = await _service.ComputePerceptualHashAsync(content, "image/raster");
        // R/G/B/C/M/Y/black/white columns have Rec.709 brightness
        // 54/182/18/201/73/237/0/255. Only G/C/Y/white exceed the mean.
        hash.Should().Be("aaaaaaaaaaaaaaaa");
        content.Position.Should().Be(0);
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_ReadsNonSeekableStream()
    {
        using var encoded = CreateColorRaster(MagickFormat.Png, false);
        using var content = new NonSeekableMemoryStream(encoded.ToArray());
        var hash = await _service.ComputePerceptualHashAsync(content, "image/png");
        hash.Should().Be("aaaaaaaaaaaaaaaa");
    }

    [Fact]
    public async Task ComputePerceptualHashAsync_UsesFirstAnimationFrame()
    {
        using var first = CreateBenignPng(false);
        using var second = CreateBenignPng(true);
        using var frames = new MagickImageCollection();
        frames.Add(new MagickImage(first));
        frames.Add(new MagickImage(second));
        using var content = new MemoryStream();
        frames.Write(content, MagickFormat.Gif);
        content.Position = 0;
        var hash = await _service.ComputePerceptualHashAsync(content, "image/gif");
        hash.Should().Be("f0f0f0f0f0f0f0f0");
        content.Position.Should().Be(0);
    }

    private static MemoryStream CreateColorRaster(MagickFormat format, bool cmyk)
    {
        using var image = new MagickImage(MagickColors.Black, 8, 8);
        image.ColorType = ColorType.TrueColor;
        image.Depth = 8;
        image.Quality = 100;
        byte[][] colors =
        [
            [255, 0, 0], [0, 255, 0], [0, 0, 255], [0, 255, 255],
            [255, 0, 255], [255, 255, 0], [0, 0, 0], [255, 255, 255]
        ];
        using (var pixels = image.GetPixels())
        {
            for (var row = 0; row < 8; row++)
            {
                for (var column = 0; column < 8; column++)
                {
                    pixels.SetPixel(column, row, colors[column]);
                }
            }
        }
        if (cmyk)
        {
            image.ColorSpace = ColorSpace.CMYK;
        }
        var content = new MemoryStream();
        image.Write(content, format);
        content.Position = 0;
        return content;
    }

    private sealed class NonSeekableMemoryStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override bool CanSeek => false;
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }

    [Fact]
    public async Task FindExistingContentAsync_ReturnsNull_WhenNoMatchFound()
    {
        // Arrange
        var contentHash = "abc123";
        _contentRepositoryMock.Setup(r => r.GetByContentHashAsync(contentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetContent?)null);

        // Act
        var result = await _service.FindExistingContentAsync(contentHash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindExistingContentAsync_ReturnsAssetId_WhenMatchFound()
    {
        // Arrange
        var contentHash = "abc123";
        var assetContent = new AssetContent(
            "test-bucket",
            "test/key.jpg",
            contentHash,
            "image/jpeg",
            1024,
            800,
            600);
        
        _contentRepositoryMock.Setup(r => r.GetByContentHashAsync(contentHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assetContent);

        // Act
        var result = await _service.FindExistingContentAsync(contentHash);

        // Assert
        result.Should().Be(assetContent.Id);
    }
}
