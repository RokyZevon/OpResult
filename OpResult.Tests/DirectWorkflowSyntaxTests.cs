namespace OpResult.Tests;

public class DirectWorkflowSyntaxTests
{
    [Fact]
    public void SyncWorkflow_UsesExtensionSyntaxAndTypeInference()
    {
        var observed = string.Empty;

        OpResult<string> result = OpResults.Ok()
            .Then(() => OpResults.Ok(20))
            .Then(value => OpResults.Ok($"value-{value + 2}"))
            .OnOk(value => observed = value);

        Assert.True(result.IsOk);
        Assert.Equal("value-22", result.Value);
        Assert.Equal("value-22", observed);
    }

    [Fact]
    public void SyncWorkflow_ChainsValueToVoidAndMatches()
    {
        OpResult result = OpResults.Ok(42)
            .Then(value => value > 0
                ? OpResults.Ok()
                : OpResults.Err("negative"));

        var text = result.Match(
            onOk: () => "ok",
            onErr: error => error.Message);

        Assert.Equal("ok", text);
    }

    [Fact]
    public async Task AsyncWorkflow_UsesTaskReceiversAndTypeInference()
    {
        var observed = string.Empty;

        OpResult<string> result = await LoadNumberAsync()
            .ThenAsync(value => ValidateNumberAsync(value))
            .ThenAsync(() => LoadTextAsync())
            .OnOkAsync(value =>
            {
                observed = value;
                return Task.CompletedTask;
            });

        Assert.True(result.IsOk);
        Assert.Equal("loaded", result.Value);
        Assert.Equal("loaded", observed);
    }

    [Fact]
    public async Task AsyncWorkflow_ShortCircuitsWithoutRunningLaterContinuations()
    {
        var called = false;

        OpResult<string> result = await Task.FromResult(OpResults.Err("failed"))
            .ThenAsync(() =>
            {
                called = true;
                return LoadTextAsync();
            });

        Assert.False(called);
        Assert.True(result.IsErr);
        Assert.Equal("failed", result.Error!.Message);
    }

    [Fact]
    public async Task MatchAsync_UsesExtensionSyntaxForTaskReceivers()
    {
        var text = await LoadNumberAsync().MatchAsync(
            onOk: value => Task.FromResult($"ok-{value}"),
            onErr: error => Task.FromResult(error.Message));

        Assert.Equal("ok-42", text);
    }

    private static Task<OpResult<int>> LoadNumberAsync() =>
        Task.FromResult(OpResults.Ok(42));

    private static Task<OpResult> ValidateNumberAsync(int value) =>
        Task.FromResult(value > 0 ? OpResults.Ok() : OpResults.Err("invalid"));

    private static Task<OpResult<string>> LoadTextAsync() =>
        Task.FromResult(OpResults.Ok("loaded"));
}
