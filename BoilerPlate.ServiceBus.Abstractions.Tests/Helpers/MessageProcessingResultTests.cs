using FluentAssertions;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Helpers;

/// <summary>
///     Unit tests for MessageProcessingResult enum
/// </summary>
public class MessageProcessingResultTests
{
    /// <summary>
    ///     Tests that MessageProcessingResult enum has exactly three values: Success, Failed, and PermanentFailure.
    ///     Verifies that:
    ///     - The enum contains exactly 3 distinct values
    ///     - All expected values are present (Success, Failed, PermanentFailure)
    /// </summary>
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

    /// <summary>
    ///     Tests that MessageProcessingResult.Success has an integer value of 0.
    ///     Verifies that:
    ///     - The Success enum value is correctly defined as 0
    ///     - This represents a successful message processing outcome
    /// </summary>
    [Fact]
    public void MessageProcessingResult_Success_ShouldBeZero()
    {
        // Assert
        ((int)MessageProcessingResult.Success).Should().Be(0);
    }

    /// <summary>
    ///     Tests that MessageProcessingResult.Failed has an integer value of 1.
    ///     Verifies that:
    ///     - The Failed enum value is correctly defined as 1
    ///     - This represents a temporary failure in message processing
    /// </summary>
    [Fact]
    public void MessageProcessingResult_Failed_ShouldBeOne()
    {
        // Assert
        ((int)MessageProcessingResult.Failed).Should().Be(1);
    }

    /// <summary>
    ///     Tests that MessageProcessingResult.PermanentFailure has an integer value of 2.
    ///     Verifies that:
    ///     - The PermanentFailure enum value is correctly defined as 2
    ///     - This represents a permanent failure in message processing that should not be retried
    /// </summary>
    [Fact]
    public void MessageProcessingResult_PermanentFailure_ShouldBeTwo()
    {
        // Assert
        ((int)MessageProcessingResult.PermanentFailure).Should().Be(2);
    }
}