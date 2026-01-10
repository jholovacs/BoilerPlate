using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Resolvers;

/// <summary>
/// Unit tests for DefaultTopicNameResolver
/// </summary>
public class DefaultTopicNameResolverTests
{
    private readonly DefaultTopicNameResolver _resolver;

    public DefaultTopicNameResolverTests()
    {
        _resolver = new DefaultTopicNameResolver();
    }

    #region Default Naming Strategy Tests

    [Fact]
    public void ResolveTopicName_WithPascalCase_ShouldConvertToKebabCase()
    {
        // Arrange
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = _resolver.ResolveTopicName(messageType);

        // Assert
        result.Should().Be("user-created-event");
    }

    [Fact]
    public void ResolveTopicName_WithGenericType_ShouldRemoveGenericParameters()
    {
        // Arrange
        var messageType = typeof(GenericMessage<int>);

        // Act
        var result = _resolver.ResolveTopicName(messageType);

        // Assert
        result.Should().Be("generic-message");
    }

    [Fact]
    public void ResolveTopicName_WithSingleWord_ShouldConvertToLowercase()
    {
        // Arrange
        var messageType = typeof(Event);

        // Act
        var result = _resolver.ResolveTopicName(messageType);

        // Assert
        result.Should().Be("event");
    }

    [Fact]
    public void ResolveTopicName_WithMultipleCapitals_ShouldInsertHyphensCorrectly()
    {
        // Arrange
        var messageType = typeof(HTTPRequestEvent);

        // Act
        var result = _resolver.ResolveTopicName(messageType);

        // Assert
        result.Should().Be("h-t-t-p-request-event");
    }

    [Fact]
    public void ResolveTopicName_GenericMethod_ShouldWorkWithTypeParameter()
    {
        // Act
        var result = _resolver.ResolveTopicName<UserCreatedEvent>();

        // Assert
        result.Should().Be("user-created-event");
    }

    #endregion

    #region Custom Naming Strategy Tests

    [Fact]
    public void ResolveTopicName_WithCustomStrategy_ShouldUseProvidedStrategy()
    {
        // Arrange
        Func<Type, string> customStrategy = type => $"custom-{type.Name.ToLowerInvariant()}";
        var resolver = new DefaultTopicNameResolver(customStrategy);
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = resolver.ResolveTopicName(messageType);

        // Assert
        result.Should().Be("custom-usercreatedevent");
    }

    [Fact]
    public void DefaultTopicNameResolver_WithNullStrategy_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultTopicNameResolver(null!));
    }

    #endregion

    #region Static Naming Strategy Tests

    [Fact]
    public void DefaultNamingStrategy_WithPascalCase_ShouldConvertToKebabCase()
    {
        // Arrange
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = DefaultTopicNameResolver.DefaultNamingStrategy(messageType);

        // Assert
        result.Should().Be("user-created-event");
    }

    [Fact]
    public void FullTypeNameStrategy_WithNamespace_ShouldIncludeNamespaceInKebabCase()
    {
        // Arrange
        var messageType = typeof(UserCreatedEvent);

        // Act
        var result = DefaultTopicNameResolver.FullTypeNameStrategy(messageType);

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
        var result = DefaultTopicNameResolver.TypeNameAsIsStrategy(messageType);

        // Assert
        result.Should().Be("UserCreatedEvent");
    }

    [Fact]
    public void TypeNameAsIsStrategy_WithGeneric_ShouldRemoveGenericParameters()
    {
        // Arrange
        var messageType = typeof(GenericMessage<string>);

        // Act
        var result = DefaultTopicNameResolver.TypeNameAsIsStrategy(messageType);

        // Assert
        result.Should().Be("GenericMessage");
    }

    #endregion

    #region Sanitization Tests

    [Fact]
    public void ResolveTopicName_WithInvalidCharacters_ShouldSanitize()
    {
        // Arrange
        Func<Type, string> strategy = _ => "test@#$%message";
        var resolver = new DefaultTopicNameResolver(strategy);

        // Act
        var result = resolver.ResolveTopicName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test-message");
        result.Should().NotContain("@");
        result.Should().NotContain("#");
        result.Should().NotContain("$");
        result.Should().NotContain("%");
    }

    [Fact]
    public void ResolveTopicName_WithConsecutiveHyphens_ShouldRemoveDuplicates()
    {
        // Arrange
        Func<Type, string> strategy = _ => "test---message";
        var resolver = new DefaultTopicNameResolver(strategy);

        // Act
        var result = resolver.ResolveTopicName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test-message");
        result.Should().NotContain("---");
    }

    [Fact]
    public void ResolveTopicName_WithLeadingTrailingSpecialChars_ShouldTrim()
    {
        // Arrange
        Func<Type, string> strategy = _ => "---test-message---";
        var resolver = new DefaultTopicNameResolver(strategy);

        // Act
        var result = resolver.ResolveTopicName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test-message");
        result.Should().NotStartWith("-");
        result.Should().NotEndWith("-");
    }

    [Fact]
    public void ResolveTopicName_WithLongName_ShouldTruncateTo255Chars()
    {
        // Arrange
        var longName = new string('a', 300);
        Func<Type, string> strategy = _ => longName;
        var resolver = new DefaultTopicNameResolver(strategy);

        // Act
        var result = resolver.ResolveTopicName(typeof(UserCreatedEvent));

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(255);
    }

    [Fact]
    public void ResolveTopicName_WithEmptyString_ShouldReturnDefaultTopic()
    {
        // Arrange
        Func<Type, string> strategy = _ => string.Empty;
        var resolver = new DefaultTopicNameResolver(strategy);

        // Act
        var result = resolver.ResolveTopicName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("default-topic");
    }

    [Fact]
    public void ResolveTopicName_WithWhitespace_ShouldReturnDefaultTopic()
    {
        // Arrange
        Func<Type, string> strategy = _ => "   ";
        var resolver = new DefaultTopicNameResolver(strategy);

        // Act
        var result = resolver.ResolveTopicName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("default-topic");
    }

    [Fact]
    public void ResolveTopicName_WithNullMessageType_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _resolver.ResolveTopicName((Type)null!));
    }

    [Fact]
    public void ResolveTopicName_WithValidCharacters_ShouldPreserveThem()
    {
        // Arrange
        Func<Type, string> strategy = _ => "test.message_123:valid";
        var resolver = new DefaultTopicNameResolver(strategy);

        // Act
        var result = resolver.ResolveTopicName(typeof(UserCreatedEvent));

        // Assert
        result.Should().Be("test.message_123:valid");
    }

    #endregion
}
