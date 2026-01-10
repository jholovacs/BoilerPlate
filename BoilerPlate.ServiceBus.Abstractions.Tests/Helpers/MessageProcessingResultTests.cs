using BoilerPlate.ServiceBus.Abstractions;
using FluentAssertions;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Helpers;

/// <summary>
/// Unit tests for MessageProcessingResult enum
/// </summary>
public class MessageProcessingResultTests
{
    [Fact]
    public void MessageProcessingResult_ShouldHaveThreeValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<MessageProcessingResult>();

        // Assert
        values.Should().HaveCount(3);
        values.Should().Contain(MessageProcessingResult.Success);
        values.Should().Contain(MessageProcessingResult.Failed);
        values.Should().Contain(MessageProcessingResult.PermanentFailure);
    }

    [Fact]
    public void MessageProcessingResult_Success_ShouldBeZero()
    {
        // Assert
        ((int)MessageProcessingResult.Success).Should().Be(0);
    }

    [Fact]
    public void MessageProcessingResult_Failed_ShouldBeOne()
    {
        // Assert
        ((int)MessageProcessingResult.Failed).Should().Be(1);
    }

    [Fact]
    public void MessageProcessingResult_PermanentFailure_ShouldBeTwo()
    {
        // Assert
        ((int)MessageProcessingResult.PermanentFailure).Should().Be(2);
    }
}
