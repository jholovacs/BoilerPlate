using System.Text;
using System.Text.RegularExpressions;

namespace BoilerPlate.ServiceBus.Abstractions;

/// <summary>
///     Default implementation of ITopicNameResolver
///     Resolves topic names from message type names using a configurable naming convention
/// </summary>
public class DefaultTopicNameResolver : ITopicNameResolver
{
    private readonly Func<Type, string> _namingStrategy;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DefaultTopicNameResolver" /> class
    ///     Uses a default naming strategy that converts type names to kebab-case
    /// </summary>
    public DefaultTopicNameResolver()
        : this(DefaultNamingStrategy)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DefaultTopicNameResolver" /> class
    /// </summary>
    /// <param name="namingStrategy">Custom naming strategy function</param>
    public DefaultTopicNameResolver(Func<Type, string> namingStrategy)
    {
        _namingStrategy = namingStrategy ?? throw new ArgumentNullException(nameof(namingStrategy));
    }

    /// <inheritdoc />
    public string ResolveTopicName<TMessage>() where TMessage : class, IMessage
    {
        return ResolveTopicName(typeof(TMessage));
    }

    /// <inheritdoc />
    public string ResolveTopicName(Type messageType)
    {
        if (messageType == null) throw new ArgumentNullException(nameof(messageType));

        var name = _namingStrategy(messageType);

        // Sanitize the name to ensure it's valid for RabbitMQ
        // Note: This requires a reference to BoilerPlate.ServiceBus.RabbitMq
        // For abstraction layer, we'll do basic sanitization here
        return SanitizeName(name);
    }

    /// <summary>
    ///     Sanitizes a name to be valid for RabbitMQ
    ///     Basic sanitization that removes invalid characters
    /// </summary>
    private static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "default-topic";

        // Replace invalid characters with hyphens
        var sanitized = Regex.Replace(
            name,
            @"[^a-zA-Z0-9._:-]",
            "-");

        // Remove consecutive hyphens
        sanitized = Regex.Replace(sanitized, @"-+", "-");

        // Remove leading and trailing periods, hyphens, and underscores
        sanitized = sanitized.Trim('.', '-', '_');

        // Truncate to 255 characters (approximation, actual limit is bytes)
        if (sanitized.Length > 255) sanitized = sanitized.Substring(0, 255).TrimEnd('.', '-', '_');

        // Ensure not empty
        if (string.IsNullOrEmpty(sanitized)) sanitized = "default-topic";

        return sanitized;
    }

    /// <summary>
    ///     Default naming strategy that converts type names to kebab-case
    ///     Example: "UserCreatedEvent" -> "user-created-event"
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <returns>The topic name in kebab-case</returns>
    public static string DefaultNamingStrategy(Type messageType)
    {
        var typeName = messageType.Name;

        // Remove generic type parameters if present (e.g., "Message`1" -> "Message")
        if (typeName.Contains('`')) typeName = typeName.Substring(0, typeName.IndexOf('`'));

        // Convert PascalCase to kebab-case
        var result = new StringBuilder();
        for (var i = 0; i < typeName.Length; i++)
        {
            if (i > 0 && char.IsUpper(typeName[i])) result.Append('-');
            result.Append(char.ToLowerInvariant(typeName[i]));
        }

        return result.ToString();
    }

    /// <summary>
    ///     Naming strategy that uses the full type name (namespace + type name)
    ///     Example: "MyApp.Events.UserCreatedEvent" -> "myapp.events.user-created-event"
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <returns>The topic name based on full type name</returns>
    public static string FullTypeNameStrategy(Type messageType)
    {
        var fullName = messageType.FullName ?? messageType.Name;

        // Remove generic type parameters if present
        if (fullName.Contains('`')) fullName = fullName.Substring(0, fullName.IndexOf('`'));

        // Convert to kebab-case, replacing dots with dots
        var parts = fullName.Split('.');
        var result = new StringBuilder();

        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) result.Append('.');

            var part = parts[i];
            for (var j = 0; j < part.Length; j++)
            {
                if (j > 0 && char.IsUpper(part[j])) result.Append('-');
                result.Append(char.ToLowerInvariant(part[j]));
            }
        }

        return result.ToString();
    }

    /// <summary>
    ///     Naming strategy that uses the type name as-is (no transformation)
    ///     Example: "UserCreatedEvent" -> "UserCreatedEvent"
    /// </summary>
    /// <param name="messageType">The message type</param>
    /// <returns>The topic name as the type name</returns>
    public static string TypeNameAsIsStrategy(Type messageType)
    {
        var typeName = messageType.Name;

        // Remove generic type parameters if present
        if (typeName.Contains('`')) typeName = typeName.Substring(0, typeName.IndexOf('`'));

        return typeName;
    }
}