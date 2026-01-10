using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
/// Service to create MongoDB indexes for log collection on application startup
/// </summary>
public class MongoDbLogIndexService : IHostedService
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly ILogger<MongoDbLogIndexService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbLogIndexService"/> class
    /// </summary>
    public MongoDbLogIndexService(ILogger<MongoDbLogIndexService> logger)
    {
        _connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") 
            ?? throw new InvalidOperationException("MONGODB_CONNECTION_STRING environment variable is required");
        _logger = logger;

        // Parse database name from connection string or use default
        var uri = new Uri(_connectionString);
        _databaseName = uri.AbsolutePath.TrimStart('/') == string.Empty ? "logs" : uri.AbsolutePath.TrimStart('/');
    }

    /// <summary>
    /// Creates the timestamp index on the logs collection
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = new MongoClient(_connectionString);
            var database = client.GetDatabase(_databaseName);
            var collection = database.GetCollection<MongoDB.Bson.BsonDocument>("logs");

            // Create index on timestamp field (descending for most recent first)
            var indexDefinition = Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Descending("Timestamp");
            var indexOptions = new CreateIndexOptions
            {
                Name = "Timestamp_Index",
                Background = true
            };

            var indexModel = new CreateIndexModel<MongoDB.Bson.BsonDocument>(indexDefinition, indexOptions);

            // Check if index already exists
            var indexes = await (await collection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);
            var indexExists = indexes.Any(idx => idx["name"].AsString == "Timestamp_Index");

            if (!indexExists)
            {
                await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
                _logger.LogInformation("Created timestamp index on MongoDB logs collection");
            }
            else
            {
                _logger.LogDebug("Timestamp index already exists on MongoDB logs collection");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create timestamp index on MongoDB logs collection");
            // Don't throw - allow application to start even if index creation fails
        }
    }

    /// <summary>
    /// No cleanup needed
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
