using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Entities;

/// <summary>
/// Unit tests for TamperEvidentAuditLog entity
/// </summary>
public class TamperEvidentAuditLogTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var previousHash = "prev-hash-123";

        // Act
        var log = TamperEvidentAuditLog.Create(
            tenantId,
            userId,
            "User.Login",
            "User",
            Guid.NewGuid(),
            null,
            "{\"status\":\"logged_in\"}",
            "{\"field\":\"status\",\"old\":null,\"new\":\"logged_in\"}",
            "Medium",
            "192.168.1.1",
            "Mozilla/5.0",
            "US",
            "California",
            "San Francisco",
            previousHash,
            1);

        // Assert
        log.Should().NotBeNull();
        log.TenantId.Should().Be(tenantId);
        log.UserId.Should().Be(userId);
        log.Action.Should().Be("User.Login");
        log.EntityType.Should().Be("User");
        log.RiskLevel.Should().Be("Medium");
        log.IpAddress.Should().Be("192.168.1.1");
        log.PreviousHash.Should().Be(previousHash);
        log.SequenceNumber.Should().Be(1);
        log.IsVerified.Should().BeFalse();
        log.ForwardedToSiem.Should().BeFalse();
        log.IsPartOfEvidence.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNullUserId_ShouldCreateInstance()
    {
        // Act
        var log = TamperEvidentAuditLog.Create(
            Guid.NewGuid(),
            null,
            "System.Startup",
            "System",
            null,
            null,
            null,
            "{}",
            "Low",
            "127.0.0.1",
            "SystemAgent",
            null,
            null,
            null,
            "genesis",
            0);

        // Assert
        log.UserId.Should().BeNull();
        log.EntityId.Should().BeNull();
    }

    [Fact]
    public void SetCryptographicHashes_ShouldUpdateHashes()
    {
        // Arrange
        var log = CreateTestLog();
        var contentHash = "content-hash-abc";
        var chainHash = "chain-hash-xyz";

        // Act
        log.SetCryptographicHashes(contentHash, chainHash);

        // Assert
        log.ContentHash.Should().Be(contentHash);
        log.ChainHash.Should().Be(chainHash);
    }

    private static TamperEvidentAuditLog CreateTestLog()
    {
        return TamperEvidentAuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test.Action",
            "TestEntity",
            Guid.NewGuid(),
            null,
            "{}",
            "{}",
            "Low",
            "127.0.0.1",
            "TestAgent",
            null,
            null,
            null,
            "prev-hash",
            1);
    }
}
