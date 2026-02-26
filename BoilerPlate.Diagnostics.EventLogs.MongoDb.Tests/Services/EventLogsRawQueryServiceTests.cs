using BoilerPlate.Diagnostics.EventLogs.MongoDb.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace BoilerPlate.Diagnostics.EventLogs.MongoDb.Tests.Services;

/// <summary>
///     Unit tests for <see cref="EventLogsRawQueryService" />.
/// </summary>
public class EventLogsRawQueryServiceTests
{
    /// <summary>
    ///     Scenario: EventLogsRawQueryService is constructed with no MongoDB connection string in configuration.
    ///     Expected: Throws InvalidOperationException with message indicating MongoDB connection is not configured.
    /// </summary>
    [Fact]
    public void Constructor_WhenNoConnectionString_ThrowsInvalidOperationException()
    {
        var configData = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var act = () => new EventLogsRawQueryService(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MongoDB connection not configured*");
    }

    /// <summary>
    ///     Scenario: EventLogsRawQueryService is constructed with connection string from ConnectionStrings:EventLogsMongoConnection.
    ///     Expected: Does not throw; service is created successfully.
    /// </summary>
    [Fact]
    public void Constructor_WithEventLogsMongoConnection_Succeeds()
    {
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:EventLogsMongoConnection"] = "mongodb://localhost:27017"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var act = () => new EventLogsRawQueryService(config);

        act.Should().NotThrow();
    }

    /// <summary>
    ///     Scenario: EventLogsRawQueryService falls back to MongoDbConnection when EventLogsMongoConnection is not set.
    ///     Expected: Does not throw; service is created successfully.
    /// </summary>
    [Fact]
    public void Constructor_WithMongoDbConnectionFallback_Succeeds()
    {
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MongoDbConnection"] = "mongodb://localhost:27017"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var act = () => new EventLogsRawQueryService(config);

        act.Should().NotThrow();
    }
}
