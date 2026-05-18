namespace OpResult.Tests;

using System.Reflection;

public class OpResultTests
{
    [Fact]
    public void OpResults_FactoryParametersKeepNonNullContract()
    {
        var okValue = typeof(OpResults)
            .GetMethod(nameof(OpResults.Ok))!
            .GetParameters()
            .Single();
        var errMessage = typeof(OpResults)
            .GetMethod(nameof(OpResults.Err))!
            .GetParameters()
            .Single();

        Assert.False(IsNullableAnnotated(okValue));
        Assert.False(IsNullableAnnotated(errMessage));
    }

    [Fact]
    public void Ok_CreatesSuccessfulResultFromFactory()
    {
        OpResult<int> result = OpResults.Ok(42);

        Assert.True(result.IsOk);
        Assert.False(result.IsErr);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Ok_CanBeCreatedFromValue()
    {
        OpResult<int> result = 42;

        Assert.True(result.IsOk);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Err_CreatesErrorAndConvertsToResult()
    {
        OpResult<int> result = OpResults.Err("Calculation failed");

        Assert.True(result.IsErr);
        Assert.False(result.IsOk);
        Assert.Equal("Calculation failed", result.Error!.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Err_NormalizesEmptyMessagesAtRunTime(string? message)
    {
        OpResult<int> result = OpResults.Err(message!);

        Assert.True(result.IsErr);
        Assert.Equal(string.Empty, result.Error!.Message);
    }

    [Fact]
    public void Ok_ThrowsWhenFactoryReceivesNullReferencePayload()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            OpResults.Ok<string>(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Ok_ThrowsWhenImplicitConversionReceivesNullReferencePayload()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            string value = null!;
            OpResult<string> result = value;
            _ = result;
        });

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Default_IsFailedResultWithEmptyMessage()
    {
        OpResult<int> result = default;

        Assert.True(result.IsErr);
        Assert.Equal(string.Empty, result.Error!.Message);
    }

    [Fact]
    public void ValueAndError_ReturnFallbackValuesWhenStateDoesNotMatch()
    {
        OpResult<string> ok = OpResults.Ok("ready");
        OpResult<string> err = OpResults.Err("failed");

        Assert.Equal(string.Empty, ok.Error!.Message);
        Assert.Null(err.Value);
    }

    [Fact]
    public void Then_ContinuesWhenResultIsSuccessful()
    {
        OpResult<string> result = OpResults.Ok(21)
            .Then(value => OpResults.Ok((value * 2).ToString()));

        Assert.True(result.IsOk);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void Then_ShortCircuitsWhenResultIsFailed()
    {
        var called = false;

        OpResult<int> source = OpResults.Err("failed");

        OpResult<string> result = source.Then(value =>
            {
                called = true;
                return OpResults.Ok(value.ToString());
            });

        Assert.False(called);
        Assert.True(result.IsErr);
        Assert.Equal("failed", result.Error!.Message);
    }

    [Fact]
    public async Task ThenAsync_ContinuesWhenResultIsSuccessful()
    {
        OpResult<string> result = await LoadNumberAsync()
            .ThenAsync(value => Task.FromResult<OpResult<string>>(value.ToString()));

        Assert.True(result.IsOk);
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public async Task ThenAsync_ShortCircuitsTaskResultWhenResultIsFailed()
    {
        var called = false;

        OpResult<string> result = await LoadFailedNumberAsync()
            .ThenAsync(value =>
            {
                called = true;
                return Task.FromResult<OpResult<string>>(value.ToString());
            });

        Assert.False(called);
        Assert.True(result.IsErr);
        Assert.Equal("load failed", result.Error!.Message);
    }

    [Fact]
    public void OnOk_InvokesActionOnlyWhenResultIsSuccessful()
    {
        var observed = 0;
        OpResult<int> source = OpResults.Ok(42);

        OpResult<int> result = source.OnOk(value => observed = value);

        Assert.Equal(42, observed);
        Assert.Equal(source, result);
    }

    [Fact]
    public void OnErr_InvokesActionOnlyWhenResultIsFailed()
    {
        var observed = string.Empty;
        OpResult<int> source = OpResults.Err("failed");

        OpResult<int> result = source.OnErr(error => observed = error.Message);

        Assert.Equal("failed", observed);
        Assert.Equal(source, result);
    }

    [Fact]
    public async Task OnOkAsyncAndOnErrAsync_SupportTaskResultReceivers()
    {
        var okObserved = 0;
        var errObserved = string.Empty;

        OpResult<int> ok = await LoadNumberAsync()
            .OnOkAsync(value =>
            {
                okObserved = value;
                return Task.CompletedTask;
            });

        OpResult<int> err = await LoadFailedNumberAsync()
            .OnErrAsync(error =>
            {
                errObserved = error.Message;
                return Task.CompletedTask;
            });

        Assert.True(ok.IsOk);
        Assert.Equal(42, okObserved);
        Assert.True(err.IsErr);
        Assert.Equal("load failed", errObserved);
    }

    [Fact]
    public void Match_ReturnsValueOrInvokesActionForMatchingBranch()
    {
        OpResult<int> ok = OpResults.Ok(42);
        OpResult<int> err = OpResults.Err("failed");
        var sideEffect = string.Empty;

        var okText = ok.Match(value => value.ToString(), error => error.Message);
        err.Match(
            value => sideEffect = value.ToString(),
            error => sideEffect = error.Message);

        Assert.Equal("42", okText);
        Assert.Equal("failed", sideEffect);
    }

    [Fact]
    public async Task MatchAsync_SupportsAsynchronousValueAndActionBranches()
    {
        OpResult<int> ok = OpResults.Ok(42);
        var sideEffect = string.Empty;

        var okText = await ok.MatchAsync(
            async value => value.ToString(),
            async error => error.Message
        );

        await LoadFailedNumberAsync().MatchAsync(
            async value => sideEffect = value.ToString(),
            async error => sideEffect = error.Message);

        Assert.Equal("42", okText);
        Assert.Equal("load failed", sideEffect);
    }

    private static async Task<OpResult<int>> LoadNumberAsync() =>
        42;

    private static async Task<OpResult<int>> LoadFailedNumberAsync() =>
        OpResults.Err("load failed");

    private static bool IsNullableAnnotated(ParameterInfo parameter) =>
        parameter.GetCustomAttributesData().Any(IsNullableAttribute);

    private static bool IsNullableAttribute(CustomAttributeData attribute)
    {
        if (attribute.AttributeType.FullName != "System.Runtime.CompilerServices.NullableAttribute")
        {
            return false;
        }

        return attribute.ConstructorArguments.Any(argument =>
            IsNullableFlag(argument, 2) ||
            argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> values &&
            values.Any(value => IsNullableFlag(value, 2)));
    }

    private static bool IsNullableFlag(CustomAttributeTypedArgument argument, byte expected) =>
        argument.Value is byte value && value == expected;
}
