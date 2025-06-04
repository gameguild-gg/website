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
            // Note: This migration will be handled by subsequent migrations that create ResourceBase tables
            // SQLite doesn't support adding foreign keys to existing tables, so we need to create tables
            // with the TenantId column and foreign key constraints from the start
            
            // No operations needed here - the ResourceBase tables will be created in later migrations
            // with the TenantId column already included
        }

        /// <summary>
        /// Reverts the database changes made for ResourceBase implementing ITenantable
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No operations needed here - the ResourceBase tables will be dropped in other migrations
            // that handle the full table lifecycle
        }
    }
}
