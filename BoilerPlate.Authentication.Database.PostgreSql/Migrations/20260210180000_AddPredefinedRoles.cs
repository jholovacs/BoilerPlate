using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoilerPlate.Authentication.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPredefinedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Allow same role names across tenants: unique on (tenant_id, normalized_name) instead of globally on normalized_name
            migrationBuilder.DropIndex(
                name: "ix_role_name_index",
                table: "asp_net_roles");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_roles_tenant_id_normalized_name",
                table: "asp_net_roles",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);

            // Insert Tenant Administrator, User Administrator, Role Administrator for every active tenant
            migrationBuilder.Sql(@"
                INSERT INTO asp_net_roles (id, tenant_id, name, normalized_name, description, created_at, updated_at, concurrency_stamp)
                SELECT
                    gen_random_uuid(),
                    t.id,
                    r.role_name,
                    UPPER(r.role_name),
                    r.description,
                    NOW() AT TIME ZONE 'UTC',
                    NULL,
                    gen_random_uuid()::text
                FROM tenant t
                CROSS JOIN (
                    VALUES
                        ('Tenant Administrator'::text, 'Full access to resources within the tenant'::text),
                        ('User Administrator', 'User management within the tenant'),
                        ('Role Administrator', 'Create and manage custom roles within the tenant')
                ) AS r(role_name, description)
                WHERE t.is_active = true
                AND NOT EXISTS (
                    SELECT 1 FROM asp_net_roles ar
                    WHERE ar.tenant_id = t.id AND ar.name = r.role_name
                );
            ");

            // Insert Service Administrator only for the system tenant (name is 'System' or 'System Tenant')
            migrationBuilder.Sql(@"
                INSERT INTO asp_net_roles (id, tenant_id, name, normalized_name, description, created_at, updated_at, concurrency_stamp)
                SELECT
                    gen_random_uuid(),
                    t.id,
                    'Service Administrator',
                    'SERVICE ADMINISTRATOR',
                    'Full access to all resources across all tenants',
                    NOW() AT TIME ZONE 'UTC',
                    NULL,
                    gen_random_uuid()::text
                FROM tenant t
                WHERE t.is_active = true
                AND (t.name = 'System' OR t.name = 'System Tenant')
                AND NOT EXISTS (
                    SELECT 1 FROM asp_net_roles ar
                    WHERE ar.tenant_id = t.id AND ar.name = 'Service Administrator'
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove predefined roles by name (all tenants). Index is left as (tenant_id, normalized_name) unique.
            migrationBuilder.Sql(@"
                DELETE FROM asp_net_roles
                WHERE name IN (
                    'Service Administrator',
                    'Tenant Administrator',
                    'User Administrator',
                    'Role Administrator'
                );
            ");
        }
    }
}
