using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Resolvers;

/// <summary>
/// Unit tests for DefaultQueueNameResolver
/// </summary>
public class DefaultQueueNameResolverTests
{
    private readonly DefaultQueueNameResolver _resolver;

    public DefaultQueueNameResolverTests()
    {
        _resolver = new DefaultQueueNameResolver();
    }

    #region Default Naming Strategy Tests

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

    [Fact]
    public void DefaultQueueNameResolver_WithNullStrategy_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultQueueNameResolver(null!));
    }

    #endregion

    #region Static Naming Strategy Tests

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

    [Fact]
    public void ResolveQueueName_WithNullMessageType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _resolver.ResolveQueueName((Type)null!));
    }

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
