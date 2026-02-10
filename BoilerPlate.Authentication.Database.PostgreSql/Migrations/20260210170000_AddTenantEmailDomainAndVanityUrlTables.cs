using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoilerPlate.Authentication.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantEmailDomainAndVanityUrlTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_email_domain",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_email_domain", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_email_domain_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_vanity_url",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostname = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_vanity_url", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_vanity_url_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_email_domain_domain",
                table: "tenant_email_domain",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_email_domain_tenant_id",
                table: "tenant_email_domain",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_email_domain_tenant_id_is_active",
                table: "tenant_email_domain",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_vanity_url_hostname",
                table: "tenant_vanity_url",
                column: "hostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_vanity_url_tenant_id",
                table: "tenant_vanity_url",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_vanity_url_tenant_id_is_active",
                table: "tenant_vanity_url",
                columns: new[] { "tenant_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_email_domain");

            migrationBuilder.DropTable(
                name: "tenant_vanity_url");
        }
    }
}
