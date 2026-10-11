using FluentAssertions;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GameGuild.Commerce.Payments.IntegrationTests;

/// <summary>
///     Migration contract tests for the payment-method registry (issue #414): the
/// AddPaymentMethods migration creates the tenant-scoped table with its partial unique
/// indexes (single default + duplicate-token guard) and the subscription reference column.
/// </summary>
public class PaymentMethodMigrationTests
{
    [Fact]
    public void AddPaymentMethods_ShouldCreateTheRegistryTable()
    {
        var operations = UpOperations();

        var createTable = operations
            .OfType<CreateTableOperation>()
            .Should().ContainSingle(t => t.Name == "payment_methods")
            .Which;

        createTable.PrimaryKey.Should().NotBeNull();

        createTable.Columns.Should().Contain(c => c.Name == "Id");
        createTable.Columns.Should().Contain(c => c.Name == "TenantId" && !c.IsNullable);
        createTable.Columns.Should().Contain(c => c.Name == "UserId" && !c.IsNullable);
        createTable.Columns.Should().Contain(c => c.Name == "GatewayProvider");
        createTable.Columns.Should().Contain(c => c.Name == "GatewayPaymentMethodId");
        createTable.Columns.Should().Contain(c => c.Name == "IsDefault");
        createTable.Columns.Should().Contain(c => c.Name == "Status");
        createTable.Columns.Should().Contain(c => c.Name == "CardBrand");
        createTable.Columns.Should().Contain(c => c.Name == "CardLast4");
        createTable.Columns.Should().Contain(c => c.Name == "CardExpMonth");
        createTable.Columns.Should().Contain(c => c.Name == "CardExpYear");
        createTable.Columns.Should().Contain(c => c.Name == "CardFingerprint");
        createTable.Columns.Should().Contain(c => c.Name == "Nickname");
        createTable.Columns.Should().Contain(c => c.Name == "BillingAddress");
        createTable.Columns.Should().Contain(c => c.Name == "ExpiresAt");
        createTable.Columns.Should().Contain(c => c.Name == "LastUsedAt");
        createTable.Columns.Should().Contain(c => c.Name == "DeletedAt", "soft delete marker");
        createTable.Columns.Should().Contain(c => c.Name == "ExpiryNotice30SentAt");
        createTable.Columns.Should().Contain(c => c.Name == "ExpiryNotice14SentAt");
        createTable.Columns.Should().Contain(c => c.Name == "ExpiryNotice7SentAt");
    }

    [Fact]
    public void AddPaymentMethods_ShouldEnforceSingleDefaultWithAPartialUniqueIndex()
    {
        var operations = UpOperations();

        var singleDefault = operations
            .OfType<CreateIndexOperation>()
            .Should().ContainSingle(i => i.Name == "ix_payment_methods_single_default")
            .Which;

        singleDefault.IsUnique.Should().BeTrue();
        singleDefault.Columns.Should().BeEquivalentTo(["TenantId", "UserId"]);
        singleDefault.Filter.Should().Be("\"IsDefault\" AND \"DeletedAt\" IS NULL");
    }

    [Fact]
    public void AddPaymentMethods_ShouldGuardDuplicateGatewayTokensPerTenant()
    {
        var operations = UpOperations();

        var duplicateGuard = operations
            .OfType<CreateIndexOperation>()
            .Should().ContainSingle(i => i.Name == "ix_payment_methods_tenant_gateway_unique")
            .Which;

        duplicateGuard.IsUnique.Should().BeTrue();
        duplicateGuard.Columns.Should().BeEquivalentTo(["TenantId", "GatewayProvider", "GatewayPaymentMethodId"]);
        duplicateGuard.Filter.Should().Be("\"DeletedAt\" IS NULL");
    }

    [Fact]
    public void AddPaymentMethods_ShouldAddTheSubscriptionPaymentMethodReference()
    {
        var operations = UpOperations();

        var addColumn = operations
            .OfType<AddColumnOperation>()
            .Should().ContainSingle(c => c.Table == "Subscriptions" && c.Name == "PaymentMethodId")
            .Which;

        addColumn.IsNullable.Should().BeTrue("existing subscriptions have no registered payment method");

        var subscriptionIndex = operations
            .OfType<CreateIndexOperation>()
            .Should().ContainSingle(i => i.Name == "ix_subscriptions_tenant_payment_method")
            .Which;

        subscriptionIndex.Table.Should().Be("Subscriptions");
        subscriptionIndex.Columns.Should().BeEquivalentTo(["TenantId", "PaymentMethodId"]);
    }

    [Fact]
    public void AddPaymentMethods_Down_ShouldDropTheRegistryAndRevertSubscriptions()
    {
        var builder = new MigrationBuilder("Npgsql");
        new ExposedAddPaymentMethods().BuildDown(builder);

        builder.Operations
            .OfType<DropTableOperation>()
            .Should().Contain(t => t.Name == "payment_methods");
        builder.Operations
            .OfType<DropColumnOperation>()
            .Should().Contain(c => c.Table == "Subscriptions" && c.Name == "PaymentMethodId");
    }

    private static IReadOnlyList<MigrationOperation> UpOperations()
    {
        var builder = new MigrationBuilder("Npgsql");
        new ExposedAddPaymentMethods().BuildUp(builder);
        return builder.Operations;
    }

    /// <summary>
    ///     Migration <c>Up</c>/<c>Down</c> are protected; expose them for operation-level
    ///     assertions (same pattern as <c>EconomyConfirmedFundingWriterMigrationTests</c>).
    /// </summary>
    private sealed class ExposedAddPaymentMethods : AddPaymentMethods
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);

        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }
}
