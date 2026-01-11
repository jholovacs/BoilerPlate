using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BoilerPlate.Authentication.Database.PostgreSql.Extensions;

public static class PostgresNamingExtensions
{
    public static DbContextOptionsBuilder UsePostgresSnakeCase(
        this DbContextOptionsBuilder options,
        string? connectionString = null)
    {
        // Apply snake_case naming convention
        options.UseSnakeCaseNamingConvention();
        
        // Replace the history repository to use snake_case column names
        options.ReplaceService<IHistoryRepository, SnakeCaseHistoryRepository>();
        
        return options;
    }
}