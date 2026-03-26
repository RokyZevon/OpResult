using OpResult;
using Xunit;

namespace OpResult.Tests;

/// <summary>
/// Simple tests for OpResult pattern demonstrating basic usage.
/// </summary>
public class OpResultTests
{
    [Fact]
    public void Ok_Should_CreateSuccessfulResult()
    {
        // Arrange & Act
        var result = OpResult<int, string>.Ok(42);

        // Assert
        Assert.True(result.IsOk);
        Assert.False(result.IsErr);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Err_Should_CreateErrorResult()
    {
        // Arrange & Act
        var result = OpResult<int, string>.Err("Something went wrong");

        // Assert
        Assert.False(result.IsOk);
        Assert.True(result.IsErr);
        Assert.True(result.TryGetError(out var error));
        Assert.Equal("Something went wrong", error);
    }

    [Fact]
    public void Match_Should_HandleOkCase()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(42);

        // Act
        var output = result.Match(
            onOk: value => $"Success: {value}",
            onErr: error => $"Error: {error}"
        );

        // Assert
        Assert.Equal("Success: 42", output);
    }

    [Fact]
    public void Match_Should_HandleErrCase()
    {
        // Arrange
        var result = OpResult<int, string>.Err("Failed");

        // Act
        var output = result.Match(
            onOk: value => $"Success: {value}",
            onErr: error => $"Error: {error}"
        );

        // Assert
        Assert.Equal("Error: Failed", output);
    }

    [Fact]
    public void Map_Should_TransformOkValue()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(42);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        Assert.True(mapped.IsOk);
        Assert.True(mapped.TryGetValue(out var value));
        Assert.Equal(84, value);
    }

    [Fact]
    public void Map_Should_PassThroughError()
    {
        // Arrange
        var result = OpResult<int, string>.Err("Error");

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        Assert.True(mapped.IsErr);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Equal("Error", error);
    }

    [Fact]
    public void AndThen_Should_ChainSuccessfulOperations()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(10);

        // Act
        var chained = result
            .AndThen(x => OpResult<int, string>.Ok(x + 5))
            .AndThen(x => OpResult<int, string>.Ok(x * 2));

        // Assert
        Assert.True(chained.IsOk);
        Assert.True(chained.TryGetValue(out var value));
        Assert.Equal(30, value); // (10 + 5) * 2
    }

    [Fact]
    public void AndThen_Should_StopOnFirstError()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(10);

        // Act
        var chained = result
            .AndThen(x => OpResult<int, string>.Err("First error"))
            .AndThen(x => OpResult<int, string>.Ok(x * 2)); // This should not execute

        // Assert
        Assert.True(chained.IsErr);
        Assert.True(chained.TryGetError(out var error));
        Assert.Equal("First error", error);
    }

    [Fact]
    public void ImplicitConversion_Should_ConvertValueToOk()
    {
        // Arrange & Act
        OpResult<int, string> result = 42;

        // Assert
        Assert.True(result.IsOk);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void ImplicitConversion_Should_ConvertErrorToErr()
    {
        // Arrange & Act
        OpResult<int, string> result = "Error message";

        // Assert
        Assert.True(result.IsErr);
        Assert.True(result.TryGetError(out var error));
        Assert.Equal("Error message", error);
    }

    [Fact]
    public void DefaultInstance_Should_BeErr()
    {
        // Arrange & Act
        var result = default(OpResult<int, string>);

        // Assert
        Assert.False(result.IsOk);
        Assert.True(result.IsErr);
    }

    [Fact]
    public void Match_WithNullOkDelegate_Should_ReturnDefault()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(42);

        // Act
        var output = result.Match(null!, onErr: error => $"Error: {error}");

        // Assert
        Assert.Null(output);
    }

    [Fact]
    public void Match_WithNullErrDelegate_Should_ReturnDefault()
    {
        // Arrange
        var result = OpResult<int, string>.Err("Error");

        // Act
        var output = result.Match(onOk: value => $"Success: {value}", null!);

        // Assert
        Assert.Null(output);
    }

    [Fact]
    public void VoidMatch_WithNullOkDelegate_Should_NotExecuteAnyAction()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(42);
        var errorExecuted = false;

        // Act
        result.Match(null!, onErr: _ => errorExecuted = true);

        // Assert - neither action should execute when any delegate is null
        Assert.False(errorExecuted);
    }

    [Fact]
    public void VoidMatch_WithNullErrDelegate_Should_NotExecuteAnyAction()
    {
        // Arrange
        var result = OpResult<int, string>.Err("Error");
        var okExecuted = false;

        // Act
        result.Match(onOk: _ => okExecuted = true, null!);

        // Assert - neither action should execute when any delegate is null
        Assert.False(okExecuted);
    }

    [Fact]
    public void Map_WithNullDelegate_OnOk_Should_ReturnErrWithDefaultE()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(42);

        // Act
        var mapped = result.Map<int>(null!);

        // Assert
        Assert.True(mapped.IsErr);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Null(error); // default(string) is null
    }

    [Fact]
    public void Map_WithNullDelegate_OnErr_Should_PreserveError()
    {
        // Arrange
        var result = OpResult<int, string>.Err("Original error");

        // Act
        var mapped = result.Map<int>(null!);

        // Assert
        Assert.True(mapped.IsErr);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Equal("Original error", error);
    }

    [Fact]
    public void MapErr_WithNullDelegate_OnOk_Should_PreserveValue()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(42);

        // Act
        var mapped = result.MapErr<int>(null!);

        // Assert
        Assert.True(mapped.IsOk);
        Assert.True(mapped.TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void MapErr_WithNullDelegate_OnErr_Should_ReturnErrWithDefaultF()
    {
        // Arrange
        var result = OpResult<int, string>.Err("Original error");

        // Act
        var mapped = result.MapErr<int>(null!);

        // Assert
        Assert.True(mapped.IsErr);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Equal(0, error); // default(int) is 0
    }

    [Fact]
    public void AndThen_WithNullDelegate_OnOk_Should_ReturnErrWithDefaultE()
    {
        // Arrange
        var result = OpResult<int, string>.Ok(42);

        // Act
        var chained = result.AndThen<int>(null!);

        // Assert
        Assert.True(chained.IsErr);
        Assert.True(chained.TryGetError(out var error));
        Assert.Null(error); // default(string) is null
    }

    [Fact]
    public void AndThen_WithNullDelegate_OnErr_Should_PreserveError()
    {
        // Arrange
        var result = OpResult<int, string>.Err("Original error");

        // Act
        var chained = result.AndThen<int>(null!);

        // Assert
        Assert.True(chained.IsErr);
        Assert.True(chained.TryGetError(out var error));
        Assert.Equal("Original error", error);
    }
}

/// <summary>
/// Tests for the convenience OpResult&lt;T&gt; type with OpError.
/// </summary>
public class OpResultWithOpErrorTests
{
    [Fact]
    public void Ok_Should_CreateSuccessfulResult()
    {
        // Arrange & Act
        var result = OpResult<int>.Ok(42);

        // Assert
        Assert.True(result.IsOk);
        Assert.False(result.IsErr);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Err_WithMessage_Should_CreateErrorResult()
    {
        // Arrange & Act
        var result = OpResult<int>.Err(OpError.Create("Something went wrong"));

        // Assert
        Assert.True(result.IsErr);
        Assert.True(result.TryGetError(out var error));
        Assert.Equal("Something went wrong", error.Message);
        Assert.Equal(string.Empty, error.Code);
    }

    [Fact]
    public void Err_WithCodeAndMessage_Should_CreateErrorResult()
    {
        // Arrange & Act
        var result = OpResult<int>.Err(OpError.Create("ERR001", "Something went wrong"));

        // Assert
        Assert.True(result.IsErr);
        Assert.True(result.TryGetError(out var error));
        Assert.Equal("ERR001", error.Code);
        Assert.Equal("Something went wrong", error.Message);
    }

    [Fact]
    public void Map_Should_TransformOkValue()
    {
        // Arrange
        var result = OpResult<int>.Ok(42);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        Assert.True(mapped.IsOk);
        Assert.True(mapped.TryGetValue(out var value));
        Assert.Equal("42", value);
    }

    [Fact]
    public void StaticFactory_Ok_Should_CreateResult()
    {
        // Arrange & Act
        var result = OpResult.Ok(42);

        // Assert
        Assert.True(result.IsOk);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void StaticFactory_Err_Should_CreateResult()
    {
        // Arrange & Act
        var result = OpResult.Err<int>(OpError.Create("Error message"));

        // Assert
        Assert.True(result.IsErr);
        Assert.True(result.TryGetError(out var error));
        Assert.Equal("Error message", error.Message);
    }

    [Fact]
    public void Map_WithNullDelegate_OnOk_Should_ReturnErrWithDefaultOpError()
    {
        // Arrange
        var result = OpResult<int>.Ok(42);

        // Act
        var mapped = result.Map<int>(null!);

        // Assert
        Assert.True(mapped.IsErr);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Null(error.Code);
        Assert.Null(error.Message);
    }

    [Fact]
    public void Map_WithNullDelegate_OnErr_Should_PreserveError()
    {
        // Arrange
        var result = OpResult<int>.Err(OpError.Create("ERR001", "Original error"));

        // Act
        var mapped = result.Map<int>(null!);

        // Assert
        Assert.True(mapped.IsErr);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Equal("ERR001", error.Code);
        Assert.Equal("Original error", error.Message);
    }

    [Fact]
    public void MapErr_WithNullDelegate_OnErr_Should_ReturnErrWithDefaultOpError()
    {
        // Arrange
        var result = OpResult<int>.Err(OpError.Create("ERR001", "Original error"));

        // Act
        var mapped = result.MapErr(null!);

        // Assert
        Assert.True(mapped.IsErr);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Null(error.Code);
        Assert.Null(error.Message);
    }

    [Fact]
    public void AndThen_WithNullDelegate_OnOk_Should_ReturnErrWithDefaultOpError()
    {
        // Arrange
        var result = OpResult<int>.Ok(42);

        // Act
        var chained = result.AndThen<int>(null!);

        // Assert
        Assert.True(chained.IsErr);
        Assert.True(chained.TryGetError(out var error));
        Assert.Null(error.Code);
        Assert.Null(error.Message);
    }

    [Fact]
    public void AndThen_WithNullDelegate_OnErr_Should_PreserveError()
    {
        // Arrange
        var result = OpResult<int>.Err(OpError.Create("ERR001", "Original error"));

        // Act
        var chained = result.AndThen<int>(null!);

        // Assert
        Assert.True(chained.IsErr);
        Assert.True(chained.TryGetError(out var error));
        Assert.Equal("ERR001", error.Code);
        Assert.Equal("Original error", error.Message);
    }
}

/// <summary>
/// Tests for OpError type.
/// </summary>
public class OpErrorTests
{
    [Fact]
    public void New_WithCodeAndMessage_Should_CreateError()
    {
        // Arrange & Act
        var error = OpError.Create("ERR001", "Something went wrong");

        // Assert
        Assert.Equal("ERR001", error.Code);
        Assert.Equal("Something went wrong", error.Message);
    }

    [Fact]
    public void New_WithMessage_Should_CreateErrorWithEmptyCode()
    {
        // Arrange & Act
        var error = OpError.Create("Something went wrong");

        // Assert
        Assert.Equal(string.Empty, error.Code);
        Assert.Equal("Something went wrong", error.Message);
    }

    [Fact]
    public void OpError_Should_ImplementIOpError()
    {
        // Arrange & Act
        var error = OpError.Create("TEST", "Test message");
        IOpError iError = error;

        // Assert
        Assert.Equal("TEST", iError.Code);
        Assert.Equal("Test message", iError.Message);
    }
}
