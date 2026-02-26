namespace BoilerPlate.Diagnostics.WebApi.Configuration;

/// <summary>
///     Options for RabbitMQ Management HTTP API (used for queue/exchange monitoring and maintenance).
///     Derived from RABBITMQ_CONNECTION_STRING when not explicitly set.
/// </summary>
public class RabbitMqManagementOptions
{
    public const string SectionName = "RabbitMqManagement";

    /// <summary>Base URL of RabbitMQ Management API (e.g. http://rabbitmq:15672).</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Username for HTTP Basic Auth (from connection string).</summary>
    public string Username { get; set; } = "";

    /// <summary>Password for HTTP Basic Auth (from connection string).</summary>
    public string Password { get; set; } = "";
}
