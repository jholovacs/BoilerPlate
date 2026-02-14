namespace BoilerPlate.Authentication.WebApi.Constants;

/// <summary>
///     Constants for RabbitMQ Management OAuth2 integration.
/// </summary>
public static class RabbitMqOAuthConstants
{
    /// <summary>
    ///     OAuth client ID for RabbitMQ Management UI.
    ///     Only Service Administrators in the system tenant can authorize this client.
    /// </summary>
    public const string ClientId = "rabbitmq-management";

    /// <summary>
    ///     RabbitMQ resource server ID - must appear in token audience for RabbitMQ to accept the token.
    /// </summary>
    public const string ResourceServerId = "rabbitmq";

    /// <summary>
    ///     RabbitMQ scope for administrator access.
    /// </summary>
    public const string AdministratorScope = "rabbitmq.tag:administrator";
}
