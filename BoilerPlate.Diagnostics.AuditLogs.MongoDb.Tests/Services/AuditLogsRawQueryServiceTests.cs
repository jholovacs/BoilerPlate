using BoilerPlate.Diagnostics.AuditLogs.MongoDb.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace BoilerPlate.Diagnostics.AuditLogs.MongoDb.Tests.Services;

/// <summary>
///     Unit tests for <see cref="AuditLogsRawQueryService" />.
/// </summary>
public class AuditLogsRawQueryServiceTests
{
    /// <summary>
    ///     Scenario: AuditLogsRawQueryService is constructed with no MongoDB connection string in configuration.
    ///     Expected: Throws InvalidOperationException with message indicating MongoDB connection is not configured.
    /// </summary>
    [Fact]
    public void Constructor_WhenNoConnectionString_ThrowsInvalidOperationException()
    {
        var configData = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var act = () => new AuditLogsRawQueryService(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MongoDB connection not configured*");
    }

    /// <summary>
    ///     Scenario: AuditLogsRawQueryService is constructed with connection string from ConnectionStrings:AuditLogsMongoConnection.
    ///     Expected: Does not throw; service is created successfully.
    /// </summary>
    [Fact]
    public void Constructor_WithAuditLogsMongoConnection_Succeeds()
    {
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:AuditLogsMongoConnection"] = "mongodb://localhost:27017"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var act = () => new AuditLogsRawQueryService(config);

        act.Should().NotThrow();
    }

    /// <summary>
    ///     Scenario: AuditLogsRawQueryService is constructed with connection string from MONGODB_CONNECTION_STRING env fallback.
    ///     Expected: Does not throw when env var is set (service reads Environment.GetEnvironmentVariable directly).
    /// </summary>
    [Fact]
    public void Constructor_WithMongoDbConnectionStringEnv_Succeeds()
    {
        Environment.SetEnvironmentVariable("MONGODB_CONNECTION_STRING", "mongodb://localhost:27017");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var act = () => new AuditLogsRawQueryService(config);

            act.Should().NotThrow();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MONGODB_CONNECTION_STRING", null);
        }
    }
}
