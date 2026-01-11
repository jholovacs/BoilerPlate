using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Resolvers;

/// <summary>
///     Unit tests for DefaultQueueNameResolver
/// </summary>
public class DefaultQueueNameResolverTests
{
    private readonly DefaultQueueNameResolver _resolver;

    public DefaultQueueNameResolverTests()
    {
        _resolver = new DefaultQueueNameResolver();
    }

    #region Default Naming Strategy Tests

    /// <summary>
    ///     Tests that ResolveQueueName converts PascalCase type names to kebab-case queue names.
    ///     Verifies that:
    ///     - Capital letters are converted to lowercase with hyphens between words
    ///     - The resulting queue name follows kebab-case convention
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithPascalCase_ShouldConvertToKebabCase()
    {
        // Arrange
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = _resolver.ResolveQueueName(messageType);

        // Assert
        result.Should().Be("user-created-event");
    }

    /// <summary>
    ///     Tests that ResolveQueueName removes generic type parameters from type names.
    ///     Verifies that:
    ///     - Generic parameters (e.g., &lt;int&gt;) are stripped from the queue name
    ///     - Only the base type name is used for queue resolution
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithGenericType_ShouldRemoveGenericParameters()
    {
        // Arrange
        var messageType = typeof(GenericMessage<int>);

        // Act
        var result = _resolver.ResolveQueueName(messageType);

        // Assert
        result.Should().Be("generic-message");
    }

    /// <summary>
    ///     Tests that ResolveQueueName converts single-word type names to lowercase.
    ///     Verifies that:
    ///     - Single-word type names are converted to lowercase
    ///     - No hyphens are inserted for single words
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithSingleWord_ShouldConvertToLowercase()
    {
        // Arrange
        var messageType = typeof(Event);

        // Act
        var result = _resolver.ResolveQueueName(messageType);

        // Assert
        result.Should().Be("event");
    }

    /// <summary>
    ///     Tests that ResolveQueueName correctly handles multiple consecutive capital letters.
    ///     Verifies that:
    ///     - Each capital letter is separated with a hyphen
    ///     - Abbreviations like "HTTP" are converted to "h-t-t-p"
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithMultipleCapitals_ShouldInsertHyphensCorrectly()
    {
        // Arrange
        var messageType = typeof(HTTPRequestEvent);

        // Act
        var result = _resolver.ResolveQueueName(messageType);

        // Assert
        result.Should().Be("h-t-t-p-request-event");
    }

    /// <summary>
    ///     Tests that ResolveQueueName generic method overload works correctly with type parameters.
    ///     Verifies that:
    ///     - The generic method overload resolves queue names correctly
    ///     - Type inference works properly for generic method calls
    /// </summary>
    [Fact]
    public void ResolveQueueName_GenericMethod_ShouldWorkWithTypeParameter()
    {
        // Act
        var result = _resolver.ResolveQueueName<UserCreatedEvent>();

        // Assert
        result.Should().Be("user-created-event");
    }

    #endregion

    #region Custom Naming Strategy Tests

    /// <summary>
    ///     Tests that ResolveQueueName uses a custom naming strategy when provided to the constructor.
    ///     Verifies that:
    ///     - Custom strategy functions are correctly applied
    ///     - The resolver uses the provided strategy instead of the default
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithCustomStrategy_ShouldUseProvidedStrategy()
    {
        // Arrange
        Func<Type, string> customStrategy = type => $"custom-{type.Name.ToLowerInvariant()}";
        var resolver = new DefaultQueueNameResolver(customStrategy);
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = resolver.ResolveQueueName(messageType);

        // Assert
        result.Should().Be("custom-usercreatedevent");
    }

    /// <summary>
    ///     Tests that DefaultQueueNameResolver constructor throws ArgumentNullException when provided with a null strategy.
    ///     Verifies that:
    ///     - Null strategies are rejected
    ///     - Appropriate exception is thrown for null input
    /// </summary>
    [Fact]
    public void DefaultQueueNameResolver_WithNullStrategy_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultQueueNameResolver(null!));
    }

    #endregion

    #region Static Naming Strategy Tests

    /// <summary>
    ///     Tests that DefaultNamingStrategy static method converts PascalCase type names to kebab-case.
    ///     Verifies that:
    ///     - The static method produces the same result as the default instance behavior
    ///     - PascalCase is correctly converted to kebab-case
    /// </summary>
    [Fact]
    public void DefaultNamingStrategy_WithPascalCase_ShouldConvertToKebabCase()
    {
        // Arrange
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = DefaultQueueNameResolver.DefaultNamingStrategy(messageType);

        // Assert
        result.Should().Be("user-created-event");
    }

    /// <summary>
    ///     Tests that FullTypeNameStrategy includes the full namespace in the queue name converted to kebab-case.
    ///     Verifies that:
    ///     - The full namespace path is included in the queue name
    ///     - Namespace segments are converted to kebab-case
    ///     - The type name is also included in the result
    /// </summary>
    [Fact]
    public void FullTypeNameStrategy_WithNamespace_ShouldIncludeNamespaceInKebabCase()
    {
        // Arrange
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = DefaultQueueNameResolver.FullTypeNameStrategy(messageType);

        // Assert
        result.Should().Contain("boiler-plate"); // Kebab-case conversion
        result.Should().Contain("service-bus");
        result.Should().Contain("abstractions");
        result.Should().Contain("tests");
        result.Should().Contain("user-created-event");
    }

    /// <summary>
    ///     Tests that TypeNameAsIsStrategy returns the type name unchanged (PascalCase preserved).
    ///     Verifies that:
    ///     - The type name is returned exactly as-is
    ///     - No case conversion or hyphen insertion occurs
    /// </summary>
    [Fact]
    public void TypeNameAsIsStrategy_ShouldReturnTypeNameUnchanged()
    {
        // Arrange
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = DefaultQueueNameResolver.TypeNameAsIsStrategy(messageType);

        // Assert
        result.Should().Be("UserCreatedEvent");
    }

    /// <summary>
    ///     Tests that TypeNameAsIsStrategy removes generic type parameters from generic types.
    ///     Verifies that:
    ///     - Generic parameters are stripped from the type name
    ///     - Only the base type name is returned
    /// </summary>
    [Fact]
    public void TypeNameAsIsStrategy_WithGeneric_ShouldRemoveGenericParameters()
    {
        // Arrange
        var messageType = typeof(GenericMessage<string>);

        // Act
        var result = DefaultQueueNameResolver.TypeNameAsIsStrategy(messageType);

        // Assert
        result.Should().Be("GenericMessage");
    }

    #endregion

    #region Sanitization Tests

    /// <summary>
    ///     Tests that ResolveQueueName sanitizes invalid characters from queue names.
    ///     Verifies that:
    ///     - Special characters like @, #, $, % are removed or replaced
    ///     - Only valid characters remain in the queue name
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithInvalidCharacters_ShouldSanitize()
    {
        // Arrange
        Func<Type, string> strategy = _ => "test@#$%message";
        var resolver = new DefaultQueueNameResolver(strategy);

        // Act
        var result = resolver.ResolveQueueName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test-message");
        result.Should().NotContain("@");
        result.Should().NotContain("#");
        result.Should().NotContain("$");
        result.Should().NotContain("%");
    }

    /// <summary>
    ///     Tests that ResolveQueueName removes consecutive hyphens from queue names.
    ///     Verifies that:
    ///     - Multiple consecutive hyphens are collapsed to a single hyphen
    ///     - Queue names are normalized to avoid redundant separators
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithConsecutiveHyphens_ShouldRemoveDuplicates()
    {
        // Arrange
        Func<Type, string> strategy = _ => "test---message";
        var resolver = new DefaultQueueNameResolver(strategy);

        // Act
        var result = resolver.ResolveQueueName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test-message");
        result.Should().NotContain("---");
    }

    /// <summary>
    ///     Tests that ResolveQueueName trims leading and trailing special characters from queue names.
    ///     Verifies that:
    ///     - Leading and trailing hyphens are removed
    ///     - Queue names start and end with valid characters
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithLeadingTrailingSpecialChars_ShouldTrim()
    {
        // Arrange
        Func<Type, string> strategy = _ => "---test-message---";
        var resolver = new DefaultQueueNameResolver(strategy);

        // Act
        var result = resolver.ResolveQueueName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test-message");
        result.Should().NotStartWith("-");
        result.Should().NotEndWith("-");
    }

    /// <summary>
    ///     Tests that ResolveQueueName truncates queue names that exceed 255 characters.
    ///     Verifies that:
    ///     - Very long queue names are truncated to 255 characters maximum
    ///     - Length limits are enforced for queue names
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithLongName_ShouldTruncateTo255Chars()
    {
        // Arrange
        var longName = new string('a', 300);
        Func<Type, string> strategy = _ => longName;
        var resolver = new DefaultQueueNameResolver(strategy);

        // Act
        var result = resolver.ResolveQueueName(typeof(UserCreatedEvent));

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(255);
    }

    /// <summary>
    ///     Tests that ResolveQueueName returns a default queue name when the strategy returns an empty string.
    ///     Verifies that:
    ///     - Empty strings result in the default queue name "default-queue"
    ///     - Fallback behavior is provided for empty inputs
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithEmptyString_ShouldReturnDefaultQueue()
    {
        // Arrange
        Func<Type, string> strategy = _ => string.Empty;
        var resolver = new DefaultQueueNameResolver(strategy);

        // Act
        var result = resolver.ResolveQueueName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("default-queue");
    }

    /// <summary>
    ///     Tests that ResolveQueueName returns a default queue name when the strategy returns only whitespace.
    ///     Verifies that:
    ///     - Whitespace-only strings result in the default queue name "default-queue"
    ///     - Whitespace is treated the same as empty strings
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithWhitespace_ShouldReturnDefaultQueue()
    {
        // Arrange
        Func<Type, string> strategy = _ => "   ";
        var resolver = new DefaultQueueNameResolver(strategy);

        // Act
        var result = resolver.ResolveQueueName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("default-queue");
    }

    /// <summary>
    ///     Tests that ResolveQueueName throws ArgumentNullException when provided with a null message type.
    ///     Verifies that:
    ///     - Null type parameters are rejected
    ///     - Appropriate exception is thrown for null input
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithNullMessageType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _resolver.ResolveQueueName(null!));
    }

    /// <summary>
    ///     Tests that ResolveQueueName preserves valid characters like dots, underscores, and colons.
    ///     Verifies that:
    ///     - Characters that are valid for queue names (., _, :) are preserved
    ///     - Only truly invalid characters are sanitized
    /// </summary>
    [Fact]
    public void ResolveQueueName_WithValidCharacters_ShouldPreserveThem()
    {
        // Arrange
        Func<Type, string> strategy = _ => "test.message_123:valid";
        var resolver = new DefaultQueueNameResolver(strategy);

        // Act
        var result = resolver.ResolveQueueName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test.message_123:valid");
    }

    #endregion
}