using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoilerPlate.Authentication.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rate_limit_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permitted_requests = table.Column<int>(type: "integer", nullable: false),
                    window_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_limit_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rate_limit_configs_endpoint_key",
                table: "rate_limit_configs",
                column: "endpoint_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rate_limit_configs");
        }
    }
}
