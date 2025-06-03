using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <summary>
    /// Migration to support ResourceBase implementing ITenantable
    /// </summary>
    public partial class MakeResourceBaseTenantable : Migration
    {
        /// <summary>
        /// Upgrades the database to support ResourceBase implementing ITenantable
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add TenantId column to all tables that represent ResourceBase entities
            // This is a list of known ResourceBase-derived tables
            var resourceBaseTables = new[]
            {
                "ResourceMetadata",
                "ResourceRoles"
                // Add any other tables that represent ResourceBase-derived entities
            };

            foreach (var tableName in resourceBaseTables)
            {
                // Add TenantId column - nullable since a resource can be global
                migrationBuilder.AddColumn<Guid>(
                    name: "TenantId",
                    table: tableName,
                    nullable: true);

                // Add foreign key constraint
                migrationBuilder.AddForeignKey(
                    name: $"FK_{tableName}_Tenants_TenantId",
                    table: tableName,
                    column: "TenantId",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);

                // Add index for better query performance
                migrationBuilder.CreateIndex(
                    name: $"IX_{tableName}_TenantId",
                    table: tableName,
                    column: "TenantId");
            }
        }

        /// <summary>
        /// Reverts the database changes made for ResourceBase implementing ITenantable
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove TenantId column from all tables that represent ResourceBase entities
            var resourceBaseTables = new[]
            {
                "ResourceMetadata",
                "ResourceRoles"
                // Add any other tables that represent ResourceBase-derived entities
            };

            foreach (var tableName in resourceBaseTables)
            {
                // Drop foreign key constraint
                migrationBuilder.DropForeignKey(
                    name: $"FK_{tableName}_Tenants_TenantId",
                    table: tableName);

                // Drop index
                migrationBuilder.DropIndex(
                    name: $"IX_{tableName}_TenantId",
                    table: tableName);

                // Drop TenantId column
                migrationBuilder.DropColumn(
                    name: "TenantId",
                    table: tableName);
            }
        }
    }
}
