using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class FixCertificateIdTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReputationActions_Resources_Id",
                table: "ReputationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationLevels_Resources_Id",
                table: "ReputationLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceLocalizations_Resources_ResourceBaseId",
                table: "ResourceLocalizations");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMetadata_Resources_ResourceId",
                table: "ResourceMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourcePermissions_Resources_ResourceBaseId",
                table: "ResourcePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourcePermissions_Resources_ResourceId",
                table: "ResourcePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Resources_Id",
                table: "UserProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_Resources_Id",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_Resources_RelatedResourceId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputations_Resources_Id",
                table: "UserReputations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantReputations_Resources_Id",
                table: "UserTenantReputations");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReputationHistory_UserOrUserTenant",
                table: "UserReputationHistory");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_Name",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_ActionType",
                table: "ReputationActions");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "UserTenantReputations",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "UserTenantReputations",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "UserTenantReputations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "UserTenantReputations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableBalance",
                table: "Users",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Users",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserReputations",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserReputations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserReputations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "UserReputations",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "UserReputations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserReputations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "UserReputations",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserReputations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "UserReputations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "UserReputations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "UserReputationHistory",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "UserReputationHistory",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "UserReputationHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "UserReputationHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserProfiles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserProfiles",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "UserProfiles",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "UserProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "UserProfiles",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserProfiles",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ReputationLevels",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ReputationLevels",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ReputationLevels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "ReputationLevels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ReputationActions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ReputationActions",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ReputationActions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ReputationActions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "ReputationActions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ReputationActions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ReputationActions",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ReputationActions",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ReputationActions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "ReputationActions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContentLicenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLicenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentLicenses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContentLicenses_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShortDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsBundle = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BundleItems = table.Column<string>(type: "jsonb", nullable: true),
                    ReferralCommissionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MaxAffiliateDiscount = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    AffiliateCommissionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Products_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Thumbnail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_programs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_programs_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tag_proficiencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ProficiencyLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_proficiencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tag_proficiencies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tags_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_financial_methods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    LastFour = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ExpiryMonth = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    ExpiryYear = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_financial_methods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_financial_methods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_financial_methods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_kyc_verifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalVerificationId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    VerificationLevel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DocumentTypes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DocumentCountry = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ProviderData = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_kyc_verifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_kyc_verifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_kyc_verifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentContentLicense",
                columns: table => new
                {
                    ContentsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LicensesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentContentLicense", x => new { x.ContentsId, x.LicensesId });
                    table.ForeignKey(
                        name: "FK_ContentContentLicense_ContentLicenses_LicensesId",
                        column: x => x.LicensesId,
                        principalTable: "ContentLicenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_subscription_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    BillingInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TrialPeriodDays = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_subscription_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_subscription_plans_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_subscription_plans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductPricings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    SaleStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SaleEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPricings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPricings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductPricings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductSubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    BillingInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TrialPeriodDays = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSubscriptionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSubscriptionPlans_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductSubscriptionPlans_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductSubscriptionPlans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    MinimumOrderAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    MaxUses = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxUsesPerUser = table.Column<int>(type: "INTEGER", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoCodes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromoCodes_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodes_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CompletionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MinimumGrade = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    RequiresFeedback = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresRating = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinimumRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    ValidityDays = table.Column<int>(type: "INTEGER", nullable: true),
                    VerificationMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    CertificateTemplate = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificates_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_certificates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_certificates_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_certificates_programs_ProgramId1",
                        column: x => x.ProgramId1,
                        principalTable: "programs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgramId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPrograms_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductPrograms_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductPrograms_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductPrograms_programs_ProgramId1",
                        column: x => x.ProgramId1,
                        principalTable: "programs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "program_contents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "jsonb", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    GradingMethod = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxPoints = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_contents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_contents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_contents_program_contents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "program_contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_contents_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    FinalGrade = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_users_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_users_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tag_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tag_relationships", x => x.Id);
                    table.CheckConstraint("CK_TagRelationships_NoSelfReference", "\"SourceId\" != \"TargetId\"");
                    table.ForeignKey(
                        name: "FK_tag_relationships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tag_relationships_tags_SourceId",
                        column: x => x.SourceId,
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tag_relationships_tags_TargetId",
                        column: x => x.TargetId,
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalSubscriptionId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CurrentPeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CanceledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastPaymentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextBillingAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProductSubscriptionPlanId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductSubscriptionPlanId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_ProductSubscriptionPlans_ProductSubscriptionPlanId",
                        column: x => x.ProductSubscriptionPlanId,
                        principalTable: "ProductSubscriptionPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_subscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_product_subscription_plans_ProductSubscriptionPlanId1",
                        column: x => x.ProductSubscriptionPlanId1,
                        principalTable: "product_subscription_plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_subscriptions_product_subscription_plans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "product_subscription_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FromUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    PaymentMethodId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PromoCodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlatformFee = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ProcessorFee = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    NetAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PromoCodeId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_transactions_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_financial_transactions_PromoCodes_PromoCodeId1",
                        column: x => x.PromoCodeId1,
                        principalTable: "PromoCodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_financial_transactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_financial_transactions_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_financial_transactions_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_financial_transactions_user_financial_methods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "user_financial_methods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "certificate_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelationshipType = table.Column<int>(type: "INTEGER", nullable: false),
                    CertificateId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    TagProficiencyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificate_tags_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_certificate_tags_certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificate_tags_certificates_CertificateId1",
                        column: x => x.CertificateId1,
                        principalTable: "certificates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_certificate_tags_tag_proficiencies_TagId",
                        column: x => x.TagId,
                        principalTable: "tag_proficiencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_certificate_tags_tag_proficiencies_TagProficiencyId",
                        column: x => x.TagProficiencyId,
                        principalTable: "tag_proficiencies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "content_interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmissionData = table.Column<string>(type: "jsonb", nullable: true),
                    CompletionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TimeSpentMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    FirstAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProgramContentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_interactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_content_interactions_program_contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "program_contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_content_interactions_program_contents_ProgramContentId",
                        column: x => x.ProgramContentId,
                        principalTable: "program_contents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_content_interactions_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_feedback_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeedbackData = table.Column<string>(type: "jsonb", nullable: false),
                    OverallRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    Comments = table.Column<string>(type: "TEXT", nullable: true),
                    WouldRecommend = table.Column<bool>(type: "INTEGER", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProgramId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramUserId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_feedback_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_feedback_submissions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_program_feedback_submissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_feedback_submissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_feedback_submissions_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_feedback_submissions_program_users_ProgramUserId1",
                        column: x => x.ProgramUserId1,
                        principalTable: "program_users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_feedback_submissions_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_feedback_submissions_programs_ProgramId1",
                        column: x => x.ProgramId1,
                        principalTable: "programs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "program_user_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActiveUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_user_roles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_user_roles_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_user_roles_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    Review = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ContentQualityRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    InstructorRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    DifficultyRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    ValueRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    WouldRecommend = table.Column<bool>(type: "INTEGER", nullable: true),
                    ModerationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ModeratedBy = table.Column<int>(type: "INTEGER", nullable: true),
                    ModeratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModeratorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramRatings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgramRatings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgramRatings_Users_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgramRatings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramRatings_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramRatings_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramRatings_programs_ProgramId1",
                        column: x => x.ProgramId1,
                        principalTable: "programs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "user_certificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VerificationCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FinalGrade = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevocationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CertificateId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_certificates_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_certificates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_certificates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_certificates_certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_certificates_certificates_CertificateId1",
                        column: x => x.CertificateId1,
                        principalTable: "certificates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_certificates_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_certificates_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AcquisitionType = table.Column<int>(type: "INTEGER", nullable: false),
                    AccessStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    PricePaid = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    AccessStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccessEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GiftedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserSubscriptionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProducts_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserProducts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserProducts_Users_GiftedByUserId",
                        column: x => x.GiftedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserProducts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProducts_user_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "user_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserProducts_user_subscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "user_subscriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PromoCodeUses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PromoCodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiscountApplied = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FinancialTransactionId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodeUses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_financial_transactions_FinancialTransactionId",
                        column: x => x.FinancialTransactionId,
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PromoCodeUses_financial_transactions_FinancialTransactionId1",
                        column: x => x.FinancialTransactionId1,
                        principalTable: "financial_transactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "activity_grades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContentInteractionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GraderProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Grade = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    GradingDetails = table.Column<string>(type: "jsonb", nullable: true),
                    GradedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramUserId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_grades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_grades_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_activity_grades_content_interactions_ContentInteractionId",
                        column: x => x.ContentInteractionId,
                        principalTable: "content_interactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_grades_program_users_GraderProgramUserId",
                        column: x => x.GraderProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_grades_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_activity_grades_program_users_ProgramUserId1",
                        column: x => x.ProgramUserId1,
                        principalTable: "program_users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "certificate_blockchain_anchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlockchainNetwork = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TransactionHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BlockHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BlockNumber = table.Column<long>(type: "INTEGER", nullable: true),
                    ContractAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TokenId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AnchoredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificate_blockchain_anchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_certificate_blockchain_anchors_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_certificate_blockchain_anchors_user_certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "user_certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_CreatedAt",
                table: "UserTenantReputations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_DeletedAt",
                table: "UserTenantReputations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_OwnerId",
                table: "UserTenantReputations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_TenantId",
                table: "UserTenantReputations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_Visibility",
                table: "UserTenantReputations",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_CreatedAt",
                table: "UserReputations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_DeletedAt",
                table: "UserReputations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_OwnerId",
                table: "UserReputations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_TenantId",
                table: "UserReputations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_Visibility",
                table: "UserReputations",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_CreatedAt",
                table: "UserReputationHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_DeletedAt",
                table: "UserReputationHistory",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_OwnerId",
                table: "UserReputationHistory",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_TenantId",
                table: "UserReputationHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_Visibility",
                table: "UserReputationHistory",
                column: "Visibility");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReputationHistory_UserOrUserTenant",
                table: "UserReputationHistory",
                sql: "(\"UserId\" IS NOT NULL AND \"UserTenantId\" IS NULL) OR (\"UserId\" IS NULL AND \"UserTenantId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_OwnerId",
                table: "UserProfiles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_TenantId",
                table: "UserProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_Visibility",
                table: "UserProfiles",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_CreatedAt",
                table: "ReputationLevels",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_DeletedAt",
                table: "ReputationLevels",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_Name_TenantId",
                table: "ReputationLevels",
                columns: new[] { "Name", "TenantId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_OwnerId",
                table: "ReputationLevels",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_TenantId",
                table: "ReputationLevels",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_Visibility",
                table: "ReputationLevels",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_ActionType",
                table: "ReputationActions",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_ActionType_TenantId",
                table: "ReputationActions",
                columns: new[] { "ActionType", "TenantId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_CreatedAt",
                table: "ReputationActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_DeletedAt",
                table: "ReputationActions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_OwnerId",
                table: "ReputationActions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_TenantId",
                table: "ReputationActions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_Visibility",
                table: "ReputationActions",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_ContentInteractionId",
                table: "activity_grades",
                column: "ContentInteractionId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_CreatedAt",
                table: "activity_grades",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_DeletedAt",
                table: "activity_grades",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_Grade",
                table: "activity_grades",
                column: "Grade");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_GradedAt",
                table: "activity_grades",
                column: "GradedAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_GraderProgramUserId",
                table: "activity_grades",
                column: "GraderProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_ProgramUserId",
                table: "activity_grades",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_ProgramUserId1",
                table: "activity_grades",
                column: "ProgramUserId1");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_TenantId",
                table: "activity_grades",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_AnchoredAt",
                table: "certificate_blockchain_anchors",
                column: "AnchoredAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_BlockchainNetwork",
                table: "certificate_blockchain_anchors",
                column: "BlockchainNetwork");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_CertificateId",
                table: "certificate_blockchain_anchors",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_CreatedAt",
                table: "certificate_blockchain_anchors",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_DeletedAt",
                table: "certificate_blockchain_anchors",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_Status",
                table: "certificate_blockchain_anchors",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_TenantId",
                table: "certificate_blockchain_anchors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_blockchain_anchors_TransactionHash",
                table: "certificate_blockchain_anchors",
                column: "TransactionHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_CertificateId_TagId",
                table: "certificate_tags",
                columns: new[] { "CertificateId", "TagId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_CertificateId1",
                table: "certificate_tags",
                column: "CertificateId1");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_CreatedAt",
                table: "certificate_tags",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_DeletedAt",
                table: "certificate_tags",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_RelationshipType",
                table: "certificate_tags",
                column: "RelationshipType");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_TagId",
                table: "certificate_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_TagProficiencyId",
                table: "certificate_tags",
                column: "TagProficiencyId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_TenantId",
                table: "certificate_tags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_CreatedAt",
                table: "certificates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_DeletedAt",
                table: "certificates",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_IsActive",
                table: "certificates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_Name",
                table: "certificates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_ProductId",
                table: "certificates",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_ProgramId",
                table: "certificates",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_ProgramId1",
                table: "certificates",
                column: "ProgramId1");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_TenantId",
                table: "certificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_Type",
                table: "certificates",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_CompletedAt",
                table: "content_interactions",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_ContentId",
                table: "content_interactions",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_CreatedAt",
                table: "content_interactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_DeletedAt",
                table: "content_interactions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_LastAccessedAt",
                table: "content_interactions",
                column: "LastAccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_ProgramContentId",
                table: "content_interactions",
                column: "ProgramContentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_ProgramUserId",
                table: "content_interactions",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_Status",
                table: "content_interactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_TenantId",
                table: "content_interactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentContentLicense_LicensesId",
                table: "ContentContentLicense",
                column: "LicensesId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_CreatedAt",
                table: "ContentLicenses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_DeletedAt",
                table: "ContentLicenses",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_OwnerId",
                table: "ContentLicenses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_TenantId",
                table: "ContentLicenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_Visibility",
                table: "ContentLicenses",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_CreatedAt",
                table: "financial_transactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_DeletedAt",
                table: "financial_transactions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_ExternalTransactionId",
                table: "financial_transactions",
                column: "ExternalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_FromUserId",
                table: "financial_transactions",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_PaymentMethodId",
                table: "financial_transactions",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_PromoCodeId",
                table: "financial_transactions",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_PromoCodeId1",
                table: "financial_transactions",
                column: "PromoCodeId1");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_Status",
                table: "financial_transactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_TenantId",
                table: "financial_transactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_ToUserId",
                table: "financial_transactions",
                column: "ToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_Type",
                table: "financial_transactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_product_subscription_plans_CreatedAt",
                table: "product_subscription_plans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_subscription_plans_DeletedAt",
                table: "product_subscription_plans",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_subscription_plans_ProductId",
                table: "product_subscription_plans",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_subscription_plans_TenantId",
                table: "product_subscription_plans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricings_CreatedAt",
                table: "ProductPricings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricings_Currency",
                table: "ProductPricings",
                column: "Currency");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricings_DeletedAt",
                table: "ProductPricings",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricings_ProductId",
                table: "ProductPricings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricings_SaleEndDate",
                table: "ProductPricings",
                column: "SaleEndDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricings_SaleStartDate",
                table: "ProductPricings",
                column: "SaleStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPricings_TenantId",
                table: "ProductPricings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrograms_CreatedAt",
                table: "ProductPrograms",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrograms_DeletedAt",
                table: "ProductPrograms",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrograms_ProductId_ProgramId",
                table: "ProductPrograms",
                columns: new[] { "ProductId", "ProgramId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrograms_ProgramId",
                table: "ProductPrograms",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrograms_ProgramId1",
                table: "ProductPrograms",
                column: "ProgramId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrograms_TenantId",
                table: "ProductPrograms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedAt",
                table: "Products",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatorId",
                table: "Products",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_DeletedAt",
                table: "Products",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_OwnerId",
                table: "Products",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status",
                table: "Products",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId",
                table: "Products",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Visibility",
                table: "Products",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_CreatedAt",
                table: "ProductSubscriptionPlans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_DeletedAt",
                table: "ProductSubscriptionPlans",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_Name",
                table: "ProductSubscriptionPlans",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_ProductId",
                table: "ProductSubscriptionPlans",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_ProductId1",
                table: "ProductSubscriptionPlans",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_TenantId",
                table: "ProductSubscriptionPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_CreatedAt",
                table: "program_contents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_DeletedAt",
                table: "program_contents",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_ParentId",
                table: "program_contents",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_ProgramId",
                table: "program_contents",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_SortOrder",
                table: "program_contents",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_TenantId",
                table: "program_contents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_Type",
                table: "program_contents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_Visibility",
                table: "program_contents",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_CreatedAt",
                table: "program_feedback_submissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_DeletedAt",
                table: "program_feedback_submissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_ProductId",
                table: "program_feedback_submissions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_ProgramId",
                table: "program_feedback_submissions",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_ProgramId1",
                table: "program_feedback_submissions",
                column: "ProgramId1");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_ProgramUserId",
                table: "program_feedback_submissions",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_ProgramUserId1",
                table: "program_feedback_submissions",
                column: "ProgramUserId1");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_SubmittedAt",
                table: "program_feedback_submissions",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_TenantId",
                table: "program_feedback_submissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_feedback_submissions_UserId",
                table: "program_feedback_submissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_ActiveFrom",
                table: "program_user_roles",
                column: "ActiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_ActiveUntil",
                table: "program_user_roles",
                column: "ActiveUntil");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_CreatedAt",
                table: "program_user_roles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_DeletedAt",
                table: "program_user_roles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_ProgramId_ProgramUserId_Role",
                table: "program_user_roles",
                columns: new[] { "ProgramId", "ProgramUserId", "Role" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_ProgramUserId",
                table: "program_user_roles",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_Role",
                table: "program_user_roles",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_TenantId",
                table: "program_user_roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_CompletedAt",
                table: "program_users",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_CreatedAt",
                table: "program_users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_DeletedAt",
                table: "program_users",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_IsActive",
                table: "program_users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_JoinedAt",
                table: "program_users",
                column: "JoinedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_ProgramId_UserId",
                table: "program_users",
                columns: new[] { "ProgramId", "UserId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_TenantId",
                table: "program_users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_UserId",
                table: "program_users",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_CreatedAt",
                table: "ProgramRatings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_DeletedAt",
                table: "ProgramRatings",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_ModeratorId",
                table: "ProgramRatings",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_ProductId",
                table: "ProgramRatings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_ProgramId_UserId",
                table: "ProgramRatings",
                columns: new[] { "ProgramId", "UserId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_ProgramId1",
                table: "ProgramRatings",
                column: "ProgramId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_ProgramUserId",
                table: "ProgramRatings",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_Rating",
                table: "ProgramRatings",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_TenantId",
                table: "ProgramRatings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRatings_UserId",
                table: "ProgramRatings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_CreatedAt",
                table: "programs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_programs_DeletedAt",
                table: "programs",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_programs_OwnerId",
                table: "programs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_Slug",
                table: "programs",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_programs_Status",
                table: "programs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_programs_TenantId",
                table: "programs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_Title",
                table: "programs",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_programs_Visibility",
                table: "programs",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_CreatedAt",
                table: "PromoCodes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_CreatedBy",
                table: "PromoCodes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_DeletedAt",
                table: "PromoCodes",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_IsActive",
                table: "PromoCodes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_ProductId",
                table: "PromoCodes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_ProductId1",
                table: "PromoCodes",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_TenantId",
                table: "PromoCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_ValidFrom",
                table: "PromoCodes",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_ValidUntil",
                table: "PromoCodes",
                column: "ValidUntil");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_CreatedAt",
                table: "PromoCodeUses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_DeletedAt",
                table: "PromoCodeUses",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_FinancialTransactionId",
                table: "PromoCodeUses",
                column: "FinancialTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_FinancialTransactionId1",
                table: "PromoCodeUses",
                column: "FinancialTransactionId1");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_PromoCodeId",
                table: "PromoCodeUses",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_TenantId",
                table: "PromoCodeUses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodeUses_UserId",
                table: "PromoCodeUses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_proficiencies_CreatedAt",
                table: "tag_proficiencies",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tag_proficiencies_DeletedAt",
                table: "tag_proficiencies",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tag_proficiencies_IsActive",
                table: "tag_proficiencies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_tag_proficiencies_Name",
                table: "tag_proficiencies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_tag_proficiencies_ProficiencyLevel",
                table: "tag_proficiencies",
                column: "ProficiencyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_tag_proficiencies_TenantId",
                table: "tag_proficiencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_proficiencies_Type",
                table: "tag_proficiencies",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_tag_relationships_CreatedAt",
                table: "tag_relationships",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tag_relationships_DeletedAt",
                table: "tag_relationships",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tag_relationships_SourceId_TargetId_Type",
                table: "tag_relationships",
                columns: new[] { "SourceId", "TargetId", "Type" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tag_relationships_TargetId",
                table: "tag_relationships",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_relationships_TenantId",
                table: "tag_relationships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_relationships_Type",
                table: "tag_relationships",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_tags_CreatedAt",
                table: "tags",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tags_DeletedAt",
                table: "tags",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tags_IsActive",
                table: "tags",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_tags_Name",
                table: "tags",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_tags_TenantId",
                table: "tags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tags_Type",
                table: "tags",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_CertificateId",
                table: "user_certificates",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_CertificateId1",
                table: "user_certificates",
                column: "CertificateId1");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_CreatedAt",
                table: "user_certificates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_DeletedAt",
                table: "user_certificates",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_ExpiresAt",
                table: "user_certificates",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_IssuedAt",
                table: "user_certificates",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_ProductId",
                table: "user_certificates",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_ProgramId",
                table: "user_certificates",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_ProgramUserId",
                table: "user_certificates",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_Status",
                table: "user_certificates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_TenantId",
                table: "user_certificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_UserId",
                table: "user_certificates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_certificates_VerificationCode",
                table: "user_certificates",
                column: "VerificationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_financial_methods_CreatedAt",
                table: "user_financial_methods",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_financial_methods_DeletedAt",
                table: "user_financial_methods",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_financial_methods_IsDefault",
                table: "user_financial_methods",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_user_financial_methods_Status",
                table: "user_financial_methods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_user_financial_methods_TenantId",
                table: "user_financial_methods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_financial_methods_Type",
                table: "user_financial_methods",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_user_financial_methods_UserId",
                table: "user_financial_methods",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_CreatedAt",
                table: "user_kyc_verifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_DeletedAt",
                table: "user_kyc_verifications",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_ExternalVerificationId",
                table: "user_kyc_verifications",
                column: "ExternalVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_Provider",
                table: "user_kyc_verifications",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_Status",
                table: "user_kyc_verifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_SubmittedAt",
                table: "user_kyc_verifications",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_TenantId",
                table: "user_kyc_verifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_kyc_verifications_UserId",
                table: "user_kyc_verifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_CreatedAt",
                table: "user_subscriptions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_CurrentPeriodEnd",
                table: "user_subscriptions",
                column: "CurrentPeriodEnd");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_CurrentPeriodStart",
                table: "user_subscriptions",
                column: "CurrentPeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_DeletedAt",
                table: "user_subscriptions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_NextBillingAt",
                table: "user_subscriptions",
                column: "NextBillingAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_ProductSubscriptionPlanId",
                table: "user_subscriptions",
                column: "ProductSubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_ProductSubscriptionPlanId1",
                table: "user_subscriptions",
                column: "ProductSubscriptionPlanId1");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_Status",
                table: "user_subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_SubscriptionPlanId",
                table: "user_subscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_TenantId",
                table: "user_subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId",
                table: "user_subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_AccessEndDate",
                table: "UserProducts",
                column: "AccessEndDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_AccessStartDate",
                table: "UserProducts",
                column: "AccessStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_AccessStatus",
                table: "UserProducts",
                column: "AccessStatus");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_AcquisitionType",
                table: "UserProducts",
                column: "AcquisitionType");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_CreatedAt",
                table: "UserProducts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_DeletedAt",
                table: "UserProducts",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_GiftedByUserId",
                table: "UserProducts",
                column: "GiftedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_ProductId",
                table: "UserProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_ProductId1",
                table: "UserProducts",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_SubscriptionId",
                table: "UserProducts",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_TenantId",
                table: "UserProducts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_UserId_ProductId",
                table: "UserProducts",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProducts_UserSubscriptionId",
                table: "UserProducts",
                column: "UserSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationActions_Tenants_TenantId",
                table: "ReputationActions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationActions_Users_OwnerId",
                table: "ReputationActions",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationLevels_Tenants_TenantId",
                table: "ReputationLevels",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationLevels_Users_OwnerId",
                table: "ReputationLevels",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Tenants_TenantId",
                table: "UserProfiles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Users_OwnerId",
                table: "UserProfiles",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Tenants_TenantId",
                table: "UserReputationHistory",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Users_OwnerId",
                table: "UserReputationHistory",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputations_Tenants_TenantId",
                table: "UserReputations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputations_Users_OwnerId",
                table: "UserReputations",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantReputations_Tenants_TenantId",
                table: "UserTenantReputations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantReputations_Users_OwnerId",
                table: "UserTenantReputations",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReputationActions_Tenants_TenantId",
                table: "ReputationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationActions_Users_OwnerId",
                table: "ReputationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationLevels_Tenants_TenantId",
                table: "ReputationLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationLevels_Users_OwnerId",
                table: "ReputationLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Tenants_TenantId",
                table: "UserProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Users_OwnerId",
                table: "UserProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_Tenants_TenantId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_Users_OwnerId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputations_Tenants_TenantId",
                table: "UserReputations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputations_Users_OwnerId",
                table: "UserReputations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantReputations_Tenants_TenantId",
                table: "UserTenantReputations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantReputations_Users_OwnerId",
                table: "UserTenantReputations");

            migrationBuilder.DropTable(
                name: "activity_grades");

            migrationBuilder.DropTable(
                name: "certificate_blockchain_anchors");

            migrationBuilder.DropTable(
                name: "certificate_tags");

            migrationBuilder.DropTable(
                name: "ContentContentLicense");

            migrationBuilder.DropTable(
                name: "ProductPricings");

            migrationBuilder.DropTable(
                name: "ProductPrograms");

            migrationBuilder.DropTable(
                name: "program_feedback_submissions");

            migrationBuilder.DropTable(
                name: "program_user_roles");

            migrationBuilder.DropTable(
                name: "ProgramRatings");

            migrationBuilder.DropTable(
                name: "PromoCodeUses");

            migrationBuilder.DropTable(
                name: "tag_relationships");

            migrationBuilder.DropTable(
                name: "user_kyc_verifications");

            migrationBuilder.DropTable(
                name: "UserProducts");

            migrationBuilder.DropTable(
                name: "content_interactions");

            migrationBuilder.DropTable(
                name: "user_certificates");

            migrationBuilder.DropTable(
                name: "tag_proficiencies");

            migrationBuilder.DropTable(
                name: "ContentLicenses");

            migrationBuilder.DropTable(
                name: "financial_transactions");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "program_contents");

            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropTable(
                name: "program_users");

            migrationBuilder.DropTable(
                name: "PromoCodes");

            migrationBuilder.DropTable(
                name: "user_financial_methods");

            migrationBuilder.DropTable(
                name: "ProductSubscriptionPlans");

            migrationBuilder.DropTable(
                name: "product_subscription_plans");

            migrationBuilder.DropTable(
                name: "programs");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantReputations_CreatedAt",
                table: "UserTenantReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantReputations_DeletedAt",
                table: "UserTenantReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantReputations_OwnerId",
                table: "UserTenantReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantReputations_TenantId",
                table: "UserTenantReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantReputations_Visibility",
                table: "UserTenantReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputations_CreatedAt",
                table: "UserReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputations_DeletedAt",
                table: "UserReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputations_OwnerId",
                table: "UserReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputations_TenantId",
                table: "UserReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputations_Visibility",
                table: "UserReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputationHistory_CreatedAt",
                table: "UserReputationHistory");

            migrationBuilder.DropIndex(
                name: "IX_UserReputationHistory_DeletedAt",
                table: "UserReputationHistory");

            migrationBuilder.DropIndex(
                name: "IX_UserReputationHistory_OwnerId",
                table: "UserReputationHistory");

            migrationBuilder.DropIndex(
                name: "IX_UserReputationHistory_TenantId",
                table: "UserReputationHistory");

            migrationBuilder.DropIndex(
                name: "IX_UserReputationHistory_Visibility",
                table: "UserReputationHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserReputationHistory_UserOrUserTenant",
                table: "UserReputationHistory");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_OwnerId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_TenantId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_Visibility",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_CreatedAt",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_DeletedAt",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_Name_TenantId",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_OwnerId",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_TenantId",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_Visibility",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_ActionType",
                table: "ReputationActions");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_ActionType_TenantId",
                table: "ReputationActions");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_CreatedAt",
                table: "ReputationActions");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_DeletedAt",
                table: "ReputationActions");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_OwnerId",
                table: "ReputationActions");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_TenantId",
                table: "ReputationActions");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_Visibility",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "AvailableBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "ReputationActions");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserReputations",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserProfiles",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ReputationActions",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Resources_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserReputationHistory_UserOrUserTenant",
                table: "UserReputationHistory",
                sql: "(\"UserId\" IS NOT NULL AND \"UserTenantId\" IS NULL) OR (\"UserId\" IS NULL AND \"UserTenantId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_Name",
                table: "ReputationLevels",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_ActionType",
                table: "ReputationActions",
                column: "ActionType",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_CreatedAt",
                table: "Resources",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_DeletedAt",
                table: "Resources",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_OwnerId",
                table: "Resources",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_TenantId",
                table: "Resources",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Visibility",
                table: "Resources",
                column: "Visibility");

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationActions_Resources_Id",
                table: "ReputationActions",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationLevels_Resources_Id",
                table: "ReputationLevels",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceLocalizations_Resources_ResourceBaseId",
                table: "ResourceLocalizations",
                column: "ResourceBaseId",
                principalTable: "Resources",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMetadata_Resources_ResourceId",
                table: "ResourceMetadata",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourcePermissions_Resources_ResourceBaseId",
                table: "ResourcePermissions",
                column: "ResourceBaseId",
                principalTable: "Resources",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourcePermissions_Resources_ResourceId",
                table: "ResourcePermissions",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Resources_Id",
                table: "UserProfiles",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Resources_Id",
                table: "UserReputationHistory",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Resources_RelatedResourceId",
                table: "UserReputationHistory",
                column: "RelatedResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputations_Resources_Id",
                table: "UserReputations",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantReputations_Resources_Id",
                table: "UserTenantReputations",
                column: "Id",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
