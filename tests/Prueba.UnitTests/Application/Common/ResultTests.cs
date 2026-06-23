using FluentAssertions;
using Prueba.Application.Common;

namespace Prueba.UnitTests.Application.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResultWithValue()
    {
        // Arrange & Act
        var result = Result<string>.Success("hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Fail_ShouldCreateFailedResultWithError()
    {
        // Arrange & Act
        var result = Result<string>.Fail("something went wrong");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void Success_WithNullValue_ShouldStillBeSuccessful()
    {
        // Arrange & Act
        var result = Result<string?>.Success(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Fail_Generic_ShouldHaveDefaultForValueType()
    {
        // Arrange & Act
        var result = Result<int>.Fail("not found");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default(int));
        result.Error.Should().Be("not found");
    }

    [Fact]
    public void NonGeneric_Success_ShouldCreateSuccessfulResult()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NonGeneric_Fail_ShouldCreateFailedResultWithError()
    {
        // Arrange & Act
        var result = Result.Fail("validation failed");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("validation failed");
    }
}
