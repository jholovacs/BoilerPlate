using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace BoilerPlate.Authentication.Database.PostgreSql.Extensions;

public class SnakeCaseHistoryRepository : NpgsqlHistoryRepository
{
#pragma warning disable EF1001
    public SnakeCaseHistoryRepository(HistoryRepositoryDependencies dependencies)
        : base(dependencies)
    {
    }
#pragma warning restore EF1001

    protected override string TableName => "__ef_migrations_history";
    
    protected override string TableSchema => null!;

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