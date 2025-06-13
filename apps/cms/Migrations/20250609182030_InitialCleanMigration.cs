using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.Migrations
{
    /// <inheritdoc />
    public partial class InitialCleanMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Languages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRevoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    RevokedByIp = table.Column<string>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByIp = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResourceMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AdditionalData = table.Column<string>(type: "jsonb", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SeoMetadata = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceMetadata_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
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
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResourceLocalizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LanguageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceBaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceLocalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceLocalizations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceLocalizations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
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
                name: "ContentLicenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                        name: "FK_ContentLicenses_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
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
                name: "Credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Credentials_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Credentials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                        name: "FK_Products_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
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
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                        name: "FK_programs_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
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
                name: "ReputationLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MinimumScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReputationLevels_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReputationLevels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReputationLevels_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourcePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    GrantedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourcePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_ResourceMetadata_ResourceMetadataId",
                        column: x => x.ResourceMetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "UserTenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenants_Users_UserId",
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
                name: "product_pricing",
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
                    table.PrimaryKey("PK_product_pricing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_pricing_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_pricing_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
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
                name: "promo_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
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
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promo_codes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_promo_codes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_promo_codes_Users_CreatedBy",
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
                    Description = table.Column<string>(type: "TEXT", nullable: false),
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
                name: "product_programs",
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
                    table.PrimaryKey("PK_product_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_programs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_programs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_product_programs_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_programs_programs_ProgramId1",
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
                    Description = table.Column<string>(type: "TEXT", nullable: false),
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
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletionPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
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
                name: "ReputationActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiredLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReputationActions_ReputationLevels_RequiredLevelId",
                        column: x => x.RequiredLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReputationActions_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReputationActions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReputationActions_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLevelCalculation = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PositiveChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    NegativeChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    ReputationTierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReputations_ReputationLevels_CurrentLevelId",
                        column: x => x.CurrentLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputations_ReputationLevels_ReputationTierId",
                        column: x => x.ReputationTierId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputations_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserReputations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTypePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentTypeName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    AssignedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTypePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTypePermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ContentTypePermissions_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContentTypePermissions_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentTypePermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTenantReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLevelCalculation = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PositiveChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    NegativeChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenantReputations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_ReputationLevels_CurrentLevelId",
                        column: x => x.CurrentLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                        name: "FK_user_subscriptions_product_subscription_plans_ProductSubscriptionPlanId",
                        column: x => x.ProductSubscriptionPlanId,
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
                        name: "FK_financial_transactions_promo_codes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalTable: "promo_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_financial_transactions_promo_codes_PromoCodeId1",
                        column: x => x.PromoCodeId1,
                        principalTable: "promo_codes",
                        principalColumn: "Id");
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
                name: "program_ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(2,1)", nullable: false),
                    Review = table.Column<string>(type: "TEXT", nullable: true),
                    ContentQualityRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    InstructorRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    DifficultyRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    ValueRating = table.Column<decimal>(type: "decimal(2,1)", nullable: true),
                    WouldRecommend = table.Column<bool>(type: "INTEGER", nullable: true),
                    ModerationStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ModeratedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModeratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProgramId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_ratings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_ratings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_ratings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_ratings_Users_ModeratedBy",
                        column: x => x.ModeratedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_ratings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_ratings_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_ratings_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_ratings_programs_ProgramId1",
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
                name: "UserReputationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReputationActionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PointsChange = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousScore = table.Column<int>(type: "INTEGER", nullable: false),
                    NewScore = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NewLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RelatedResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserReputationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserTenantReputationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputationHistory", x => x.Id);
                    table.CheckConstraint("CK_UserReputationHistory_UserOrUserTenant", "(\"UserId\" IS NOT NULL AND \"UserTenantId\" IS NULL) OR (\"UserId\" IS NULL AND \"UserTenantId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_ReputationActions_ReputationActionId",
                        column: x => x.ReputationActionId,
                        principalTable: "ReputationActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_ReputationLevels_NewLevelId",
                        column: x => x.NewLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_ReputationLevels_PreviousLevelId",
                        column: x => x.PreviousLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_UserReputations_UserReputationId",
                        column: x => x.UserReputationId,
                        principalTable: "UserReputations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_UserTenantReputations_UserTenantReputationId",
                        column: x => x.UserTenantReputationId,
                        principalTable: "UserTenantReputations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_products",
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
                    table.PrimaryKey("PK_user_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_products_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_products_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_products_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_products_Users_GiftedByUserId",
                        column: x => x.GiftedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_products_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_products_user_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "user_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_products_user_subscriptions_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "user_subscriptions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "promo_code_uses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PromoCodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FinancialTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiscountApplied = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_code_uses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_promo_code_uses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_promo_code_uses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_promo_code_uses_financial_transactions_FinancialTransactionId",
                        column: x => x.FinancialTransactionId,
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_promo_code_uses_promo_codes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalTable: "promo_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_grades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ContentInteractionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GraderProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Grade = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", nullable: true),
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
                name: "IX_activity_grades_ContentInteractionId",
                table: "activity_grades",
                column: "ContentInteractionId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_CreatedAt",
                table: "activity_grades",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_activity_grades_DeletedAt",
                table: "activity_grades",
                column: "DeletedAt");

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
                name: "IX_certificate_tags_CertificateId",
                table: "certificate_tags",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_certificate_tags_CertificateId_TagId",
                table: "certificate_tags",
                columns: new[] { "CertificateId", "TagId" },
                unique: true);

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
                name: "IX_certificates_CompletionPercentage",
                table: "certificates",
                column: "CompletionPercentage");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_CreatedAt",
                table: "certificates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_certificates_DeletedAt",
                table: "certificates",
                column: "DeletedAt");

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
                name: "IX_content_interactions_ProgramContentId",
                table: "content_interactions",
                column: "ProgramContentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_interactions_ProgramUserId",
                table: "content_interactions",
                column: "ProgramUserId");

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
                name: "IX_ContentLicenses_MetadataId",
                table: "ContentLicenses",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_OwnerId",
                table: "ContentLicenses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_TenantId",
                table: "ContentLicenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_AssignedByUser",
                table: "ContentTypePermissions",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_CreatedAt",
                table: "ContentTypePermissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_DeletedAt",
                table: "ContentTypePermissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_TenantId",
                table: "ContentTypePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_User_Tenant_ContentType",
                table: "ContentTypePermissions",
                columns: new[] { "UserId", "TenantId", "ContentTypeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_UserTenantId",
                table: "ContentTypePermissions",
                column: "UserTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_CreatedAt",
                table: "Credentials",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_DeletedAt",
                table: "Credentials",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_TenantId",
                table: "Credentials",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_UserId_Type",
                table: "Credentials",
                columns: new[] { "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_Amount",
                table: "financial_transactions",
                column: "Amount");

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
                name: "IX_financial_transactions_ProcessedAt",
                table: "financial_transactions",
                column: "ProcessedAt");

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
                name: "IX_Languages_Code",
                table: "Languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Languages_CreatedAt",
                table: "Languages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_DeletedAt",
                table: "Languages",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Name",
                table: "Languages",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_TenantId",
                table: "Languages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_CreatedAt",
                table: "product_pricing",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_Currency",
                table: "product_pricing",
                column: "Currency");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_DeletedAt",
                table: "product_pricing",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_IsDefault",
                table: "product_pricing",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_ProductId",
                table: "product_pricing",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_SaleEndDate",
                table: "product_pricing",
                column: "SaleEndDate");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_SaleStartDate",
                table: "product_pricing",
                column: "SaleStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_product_pricing_TenantId",
                table: "product_pricing",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_programs_CreatedAt",
                table: "product_programs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_programs_DeletedAt",
                table: "product_programs",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_programs_ProductId_ProgramId",
                table: "product_programs",
                columns: new[] { "ProductId", "ProgramId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_product_programs_ProductId_SortOrder",
                table: "product_programs",
                columns: new[] { "ProductId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_product_programs_ProgramId",
                table: "product_programs",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_product_programs_ProgramId1",
                table: "product_programs",
                column: "ProgramId1");

            migrationBuilder.CreateIndex(
                name: "IX_product_programs_TenantId",
                table: "product_programs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_product_subscription_plans_CreatedAt",
                table: "product_subscription_plans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_subscription_plans_DeletedAt",
                table: "product_subscription_plans",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_product_subscription_plans_TenantId",
                table: "product_subscription_plans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_BillingInterval",
                table: "product_subscription_plans",
                column: "BillingInterval");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_IsActive",
                table: "product_subscription_plans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_IsDefault",
                table: "product_subscription_plans",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_Name",
                table: "product_subscription_plans",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_Price",
                table: "product_subscription_plans",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubscriptionPlans_ProductId",
                table: "product_subscription_plans",
                column: "ProductId");

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
                name: "IX_Products_MetadataId",
                table: "Products",
                column: "MetadataId");

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
                name: "IX_program_contents_CreatedAt",
                table: "program_contents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_DeletedAt",
                table: "program_contents",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_IsRequired",
                table: "program_contents",
                column: "IsRequired");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_ParentId",
                table: "program_contents",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_ParentId_SortOrder",
                table: "program_contents",
                columns: new[] { "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_ProgramId",
                table: "program_contents",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_program_contents_ProgramId_SortOrder",
                table: "program_contents",
                columns: new[] { "ProgramId", "SortOrder" });

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
                name: "IX_program_feedback_submissions_OverallRating",
                table: "program_feedback_submissions",
                column: "OverallRating");

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
                name: "IX_program_feedback_submissions_UserId_ProgramId",
                table: "program_feedback_submissions",
                columns: new[] { "UserId", "ProgramId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_CreatedAt",
                table: "program_ratings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_DeletedAt",
                table: "program_ratings",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_ModeratedBy",
                table: "program_ratings",
                column: "ModeratedBy");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_ModerationStatus",
                table: "program_ratings",
                column: "ModerationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_ProductId",
                table: "program_ratings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_ProgramId",
                table: "program_ratings",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_ProgramId1",
                table: "program_ratings",
                column: "ProgramId1");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_ProgramUserId",
                table: "program_ratings",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_Rating",
                table: "program_ratings",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_SubmittedAt",
                table: "program_ratings",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_TenantId",
                table: "program_ratings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_UserId",
                table: "program_ratings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_program_ratings_UserId_ProgramId",
                table: "program_ratings",
                columns: new[] { "UserId", "ProgramId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_users_CompletionPercentage",
                table: "program_users",
                column: "CompletionPercentage");

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
                name: "IX_program_users_ProgramId",
                table: "program_users",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_TenantId",
                table: "program_users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_UserId",
                table: "program_users",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_program_users_UserId_ProgramId",
                table: "program_users",
                columns: new[] { "UserId", "ProgramId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_programs_CreatedAt",
                table: "programs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_programs_DeletedAt",
                table: "programs",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_programs_MetadataId",
                table: "programs",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_OwnerId",
                table: "programs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_Slug",
                table: "programs",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_programs_Status",
                table: "programs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_programs_TenantId",
                table: "programs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_Visibility",
                table: "programs",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_CreatedAt",
                table: "promo_code_uses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_DeletedAt",
                table: "promo_code_uses",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_FinancialTransactionId",
                table: "promo_code_uses",
                column: "FinancialTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_PromoCodeId",
                table: "promo_code_uses",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_TenantId",
                table: "promo_code_uses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_UserId",
                table: "promo_code_uses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_Code",
                table: "promo_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_CreatedAt",
                table: "promo_codes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_CreatedBy",
                table: "promo_codes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_DeletedAt",
                table: "promo_codes",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_IsActive",
                table: "promo_codes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_ProductId",
                table: "promo_codes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_TenantId",
                table: "promo_codes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_Type",
                table: "promo_codes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_ValidFrom",
                table: "promo_codes",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_ValidUntil",
                table: "promo_codes",
                column: "ValidUntil");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_CreatedAt",
                table: "RefreshTokens",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_DeletedAt",
                table: "RefreshTokens",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_ActionType",
                table: "ReputationActions",
                column: "ActionType",
                unique: true);

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
                name: "IX_ReputationActions_IsActive",
                table: "ReputationActions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_MetadataId",
                table: "ReputationActions",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_OwnerId",
                table: "ReputationActions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_Points",
                table: "ReputationActions",
                column: "Points");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_RequiredLevelId",
                table: "ReputationActions",
                column: "RequiredLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_TenantId",
                table: "ReputationActions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_CreatedAt",
                table: "ReputationLevels",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_DeletedAt",
                table: "ReputationLevels",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_MetadataId",
                table: "ReputationLevels",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_MinimumScore",
                table: "ReputationLevels",
                column: "MinimumScore");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_OwnerId",
                table: "ReputationLevels",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_SortOrder",
                table: "ReputationLevels",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_TenantId",
                table: "ReputationLevels",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_CreatedAt",
                table: "ResourceLocalizations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_DeletedAt",
                table: "ResourceLocalizations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_LanguageId",
                table: "ResourceLocalizations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_ResourceBaseId",
                table: "ResourceLocalizations",
                column: "ResourceBaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_TenantId",
                table: "ResourceLocalizations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_CreatedAt",
                table: "ResourceMetadata",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_DeletedAt",
                table: "ResourceMetadata",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_ResourceType",
                table: "ResourceMetadata",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_TenantId",
                table: "ResourceMetadata",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_CreatedAt",
                table: "ResourcePermissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_DeletedAt",
                table: "ResourcePermissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_GrantedByUser",
                table: "ResourcePermissions",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_Resource",
                table: "ResourcePermissions",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_ResourceMetadataId",
                table: "ResourcePermissions",
                column: "ResourceMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_TenantId",
                table: "ResourcePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_User_Resource",
                table: "ResourcePermissions",
                columns: new[] { "UserId", "ResourceId" },
                unique: true);

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
                name: "IX_tag_relationships_SourceId",
                table: "tag_relationships",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_tag_relationships_SourceId_TargetId",
                table: "tag_relationships",
                columns: new[] { "SourceId", "TargetId" },
                unique: true);

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
                name: "IX_tags_Name_TenantId",
                table: "tags",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_TenantId",
                table: "tags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tags_Type",
                table: "tags",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAt",
                table: "Tenants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_DeletedAt",
                table: "Tenants",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                table: "Tenants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantId",
                table: "Tenants",
                column: "TenantId");

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
                name: "IX_user_financial_methods_ExternalId",
                table: "user_financial_methods",
                column: "ExternalId");

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
                name: "IX_user_products_AccessEndDate",
                table: "user_products",
                column: "AccessEndDate");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_AccessStatus",
                table: "user_products",
                column: "AccessStatus");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_AcquisitionType",
                table: "user_products",
                column: "AcquisitionType");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_CreatedAt",
                table: "user_products",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_DeletedAt",
                table: "user_products",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_GiftedByUserId",
                table: "user_products",
                column: "GiftedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_ProductId",
                table: "user_products",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_ProductId1",
                table: "user_products",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_SubscriptionId",
                table: "user_products",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_TenantId",
                table: "user_products",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_UserId",
                table: "user_products",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_products_UserId_ProductId",
                table: "user_products",
                columns: new[] { "UserId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_products_UserSubscriptionId",
                table: "user_products",
                column: "UserSubscriptionId");

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
                name: "IX_user_subscriptions_ExternalSubscriptionId",
                table: "user_subscriptions",
                column: "ExternalSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_NextBillingAt",
                table: "user_subscriptions",
                column: "NextBillingAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_ProductSubscriptionPlanId",
                table: "user_subscriptions",
                column: "ProductSubscriptionPlanId");

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
                name: "IX_UserReputationHistory_CreatedAt",
                table: "UserReputationHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_DeletedAt",
                table: "UserReputationHistory",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_MetadataId",
                table: "UserReputationHistory",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_NewLevelId",
                table: "UserReputationHistory",
                column: "NewLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_OccurredAt",
                table: "UserReputationHistory",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_OwnerId",
                table: "UserReputationHistory",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_PointsChange",
                table: "UserReputationHistory",
                column: "PointsChange");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_PreviousLevelId",
                table: "UserReputationHistory",
                column: "PreviousLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_RelatedResourceId",
                table: "UserReputationHistory",
                column: "RelatedResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_ReputationActionId",
                table: "UserReputationHistory",
                column: "ReputationActionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_TenantId",
                table: "UserReputationHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_TriggeredByUserId",
                table: "UserReputationHistory",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserId_OccurredAt",
                table: "UserReputationHistory",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserReputationId",
                table: "UserReputationHistory",
                column: "UserReputationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserTenantId_OccurredAt",
                table: "UserReputationHistory",
                columns: new[] { "UserTenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserTenantReputationId",
                table: "UserReputationHistory",
                column: "UserTenantReputationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_CreatedAt",
                table: "UserReputations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_CurrentLevelId",
                table: "UserReputations",
                column: "CurrentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_DeletedAt",
                table: "UserReputations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_MetadataId",
                table: "UserReputations",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_OwnerId",
                table: "UserReputations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_ReputationTierId",
                table: "UserReputations",
                column: "ReputationTierId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_Score",
                table: "UserReputations",
                column: "Score");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_TenantId",
                table: "UserReputations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_UserId",
                table: "UserReputations",
                column: "UserId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletedAt",
                table: "Users",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_CreatedAt",
                table: "UserTenantReputations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_CurrentLevelId",
                table: "UserTenantReputations",
                column: "CurrentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_DeletedAt",
                table: "UserTenantReputations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_MetadataId",
                table: "UserTenantReputations",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_OwnerId",
                table: "UserTenantReputations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_Score",
                table: "UserTenantReputations",
                column: "Score");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_TenantId",
                table: "UserTenantReputations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_UserTenantId",
                table: "UserTenantReputations",
                column: "UserTenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_CreatedAt",
                table: "UserTenants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_DeletedAt",
                table: "UserTenants",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_TenantId",
                table: "UserTenants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_UserId_TenantId",
                table: "UserTenants",
                columns: new[] { "UserId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_grades");

            migrationBuilder.DropTable(
                name: "certificate_blockchain_anchors");

            migrationBuilder.DropTable(
                name: "certificate_tags");

            migrationBuilder.DropTable(
                name: "ContentContentLicense");

            migrationBuilder.DropTable(
                name: "ContentTypePermissions");

            migrationBuilder.DropTable(
                name: "Credentials");

            migrationBuilder.DropTable(
                name: "product_pricing");

            migrationBuilder.DropTable(
                name: "product_programs");

            migrationBuilder.DropTable(
                name: "program_feedback_submissions");

            migrationBuilder.DropTable(
                name: "program_ratings");

            migrationBuilder.DropTable(
                name: "promo_code_uses");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ResourceLocalizations");

            migrationBuilder.DropTable(
                name: "ResourcePermissions");

            migrationBuilder.DropTable(
                name: "tag_relationships");

            migrationBuilder.DropTable(
                name: "user_kyc_verifications");

            migrationBuilder.DropTable(
                name: "user_products");

            migrationBuilder.DropTable(
                name: "UserReputationHistory");

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
                name: "Languages");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "ReputationActions");

            migrationBuilder.DropTable(
                name: "UserReputations");

            migrationBuilder.DropTable(
                name: "UserTenantReputations");

            migrationBuilder.DropTable(
                name: "program_contents");

            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropTable(
                name: "program_users");

            migrationBuilder.DropTable(
                name: "promo_codes");

            migrationBuilder.DropTable(
                name: "user_financial_methods");

            migrationBuilder.DropTable(
                name: "product_subscription_plans");

            migrationBuilder.DropTable(
                name: "ReputationLevels");

            migrationBuilder.DropTable(
                name: "UserTenants");

            migrationBuilder.DropTable(
                name: "programs");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "ResourceMetadata");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
