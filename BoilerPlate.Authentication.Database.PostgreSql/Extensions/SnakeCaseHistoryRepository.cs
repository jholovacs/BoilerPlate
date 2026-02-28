using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace BoilerPlate.Authentication.Database.PostgreSql.Extensions;

/// <summary>
///     EF Core history repository that stores migration history in snake_case column names.
///     Used by PostgreSQL migrations to match snake_case naming convention.
/// </summary>
public class SnakeCaseHistoryRepository : NpgsqlHistoryRepository
{
#pragma warning disable EF1001
    /// <summary>
    ///     Initializes the history repository with the given dependencies.
    /// </summary>
    /// <param name="dependencies">EF Core history repository dependencies</param>
    public SnakeCaseHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }
#pragma warning restore EF1001

    /// <summary>
    ///     Table name for the migrations history (snake_case).
    /// </summary>
    protected override string TableName => "__ef_migrations_history";

    /// <summary>
    ///     Schema for the migrations history table (null for default).
    /// </summary>
    protected override string TableSchema => null!;

    /// <summary>
    ///     Configures the history table with snake_case column names.
    /// </summary>
    /// <param name="history">Entity type builder for the history row</param>
    protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
    {
        base.ConfigureTable(history);

        // Override table name to ensure snake_case
        history.ToTable(TableName, TableSchema);

        // Manually map the shadow properties to snake_case
        history.Property(h => h.MigrationId).HasColumnName("migration_id");
        history.Property(h => h.ProductVersion).HasColumnName("product_version");
    }
}