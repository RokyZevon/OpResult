namespace OpResult.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class ThenMigrationTests
{
    #region Docs_NestedAndFlatChains

    [Fact]
    public void FlatAndNestedChains_KeepTheSameTraceAndErrorAtEveryStep()
    {
        foreach (var failedStep in new int?[] { null, 1, 2 })
        {
            var failure = failedStep is null
                ? null
                : OpResults.Err($"step {failedStep} failed");
            var flatTrace = new List<int>();
            var nestedTrace = new List<int>();

            var flat = RunFlat(failedStep, failure, flatTrace);
            var nested = RunNested(failedStep, failure, nestedTrace);

            Assert.Equal(flat.IsOk, nested.IsOk);
            Assert.Equal(flatTrace, nestedTrace);

            if (failure is null)
            {
                Assert.True(flat.IsOk);
                Assert.Equal(3, flat.Value);
                Assert.Equal(flat.Value, nested.Value);
                Assert.Equal(new[] { 1, 2 }, flatTrace);

                var explicitTrace = new List<int>();
                var nestedWithExplicitTypeArguments = RunNestedWithExplicitTypeArguments(explicitTrace);
                Assert.Equal(nested.Value, nestedWithExplicitTypeArguments.Value);
                Assert.Equal(nestedTrace, explicitTrace);
            }
            else
            {
                Assert.True(flat.IsErr);
                Assert.Same(failure, flat.Error);
                Assert.Same(failure, nested.Error);
                Assert.Equal(failedStep == 1 ? new[] { 1 } : new[] { 1, 2 }, flatTrace);
            }
        }

        static OpResult<int> RunFlat(int? failedStep, OpError? failure, List<int> trace) =>
            OpResults.Ok(1)
                .Then(first => Step(first, 1, failedStep, failure, trace))
                .Then(second => Step(second, 2, failedStep, failure, trace));

#pragma warning disable OPRESULT006 // This nested form is the migration example under test.
        static OpResult<int> RunNested(int? failedStep, OpError? failure, List<int> trace) =>
            OpResults.Ok(1)
                .Then(first => Step(first, 1, failedStep, failure, trace)
                    .Then(second => Step(second, 2, failedStep, failure, trace)));

        static OpResult<int> RunNestedWithExplicitTypeArguments(List<int> trace) =>
            OpResults.Ok(1)
                .Then<int, int>(first => Step(first, 1, null, null, trace)
                    .Then<int, int>(second => Step(second, 2, null, null, trace)));
#pragma warning restore OPRESULT006

        static OpResult<int> Step(int value, int step, int? failedStep, OpError? failure, List<int> trace)
        {
            trace.Add(step);
            return failedStep == step
                ? failure!
                : OpResults.Ok(value + 1);
        }
    }

    #endregion

    #region Docs_NamedStepKeepsOuterValue

    [Fact]
    public void NamedStep_KeepsTheOrderAcrossANonGenericPermissionResult()
    {
        var order = new Order(42);
        var nestedTrace = new List<string>();
        var namedTrace = new List<string>();
        var nestedSavedOrders = new List<Order>();
        var namedSavedOrders = new List<Order>();

        var nested = UpdateOrderNested(order, nestedTrace, nestedSavedOrders);
        var named = UpdateOrderWithNamedStep(order, namedTrace, namedSavedOrders);

        Assert.True(nested.IsOk);
        Assert.True(named.IsOk);
        Assert.Equal(new[] { "load", "permission:42", "save:42" }, nestedTrace);
        Assert.Equal(nestedTrace, namedTrace);
        Assert.Collection(nestedSavedOrders, savedOrder => Assert.Same(order, savedOrder));
        Assert.Collection(namedSavedOrders, savedOrder => Assert.Same(order, savedOrder));
    }

    private static OpResult<Order> LoadOrder(Order order, List<string> trace)
    {
        trace.Add("load");
        return OpResults.Ok(order);
    }

    private static OpResult ValidatePermission(Order order, List<string> trace)
    {
        trace.Add($"permission:{order.Id}");
        return OpResults.Ok();
    }

    private static OpResult SaveOrder(Order order, List<string> trace, List<Order> savedOrders)
    {
        trace.Add($"save:{order.Id}");
        savedOrders.Add(order);
        return OpResults.Ok();
    }

#pragma warning disable OPRESULT006 // This nested form is kept for the paired migration assertion.
    private static OpResult UpdateOrderNested(Order order, List<string> trace, List<Order> savedOrders) =>
        LoadOrder(order, trace).Then(currentOrder =>
            ValidatePermission(currentOrder, trace)
                .Then(() => SaveOrder(currentOrder, trace, savedOrders)));
#pragma warning restore OPRESULT006

    private static OpResult UpdateOrderWithNamedStep(Order order, List<string> trace, List<Order> savedOrders)
    {
        OpResult SaveAfterPermission(Order currentOrder) =>
            ValidatePermission(currentOrder, trace)
                .Then(() => SaveOrder(currentOrder, trace, savedOrders));

        return LoadOrder(order, trace).Then(SaveAfterPermission);
    }

    #endregion

    #region Docs_OnErrScope

    [Fact]
    public void OnErrInsideAContinuation_ObservesChildErrorsButNotLoadErrors()
    {
        var loadError = OpResults.Err("load failed");
        var childError = OpResults.Err("permission failed");
        var nestedLogs = new List<OpError>();
        var namedStepLogs = new List<OpError>();

        var nestedLoadFailure = RunWithScopedOnErr(true, loadError, childError, nestedLogs);
        var namedStepLoadFailure = RunWithScopedOnErr(false, loadError, childError, namedStepLogs);

        Assert.Same(loadError, nestedLoadFailure.Error);
        Assert.Same(loadError, namedStepLoadFailure.Error);
        Assert.Empty(nestedLogs);
        Assert.Empty(namedStepLogs);

        var nestedChildFailure = RunWithScopedOnErr(true, null, childError, nestedLogs);
        var namedStepChildFailure = RunWithScopedOnErr(false, null, childError, namedStepLogs);

        Assert.Same(childError, nestedChildFailure.Error);
        Assert.Same(childError, namedStepChildFailure.Error);
        Assert.Equal(new[] { childError }, nestedLogs);
        Assert.Equal(nestedLogs, namedStepLogs);
    }

    private static OpResult RunWithScopedOnErr(
        bool nested,
        OpError? loadError,
        OpError? childError,
        List<OpError> loggedErrors)
    {
        OpResult<Order> Load() =>
            loadError is null ? OpResults.Ok(new Order(42)) : loadError;

        OpResult CheckPermission(Order _) =>
            childError is null ? OpResults.Ok() : childError;

        OpResult Save(Order _) => OpResults.Ok();

        OpResult SaveWithOnErr(Order order) =>
            CheckPermission(order)
                .Then(() => Save(order))
                .OnErr(loggedErrors.Add);

#pragma warning disable OPRESULT006 // This nested form verifies OnErr scope during migration.
        return nested
            ? Load().Then(order => CheckPermission(order)
                .Then(() => Save(order))
                .OnErr(loggedErrors.Add))
            : Load().Then(SaveWithOnErr);
#pragma warning restore OPRESULT006
    }

    #endregion

    #region Docs_ResourceScope

    [Fact]
    public void UsingScope_DisposesAfterBothStepsOnSuccessErrAndException()
    {
        var successTrace = new List<string>();
        var success = RunWithScopedSteps(null, null, null, successTrace);
        Assert.True(success.IsOk);
        Assert.Equal(new[] { "step:1", "step:2", "dispose" }, successTrace);

        var firstStepError = OpResults.Err("first step failed");
        var firstErrorTrace = new List<string>();
        var firstError = RunWithScopedSteps(1, null, firstStepError, firstErrorTrace);
        Assert.Same(firstStepError, firstError.Error);
        Assert.Equal(new[] { "step:1", "dispose" }, firstErrorTrace);

        var secondStepError = OpResults.Err("second step failed");
        var secondErrorTrace = new List<string>();
        var secondError = RunWithScopedSteps(2, null, secondStepError, secondErrorTrace);
        Assert.Same(secondStepError, secondError.Error);
        Assert.Equal(new[] { "step:1", "step:2", "dispose" }, secondErrorTrace);

        var exceptionTrace = new List<string>();
        var exception = Assert.Throws<InvalidOperationException>(
            () => RunWithScopedSteps(null, 2, null, exceptionTrace));
        Assert.Equal("step 2 threw", exception.Message);
        Assert.Equal(new[] { "step:1", "step:2", "dispose" }, exceptionTrace);
    }

    private static OpResult RunWithScopedSteps(
        int? failedStep,
        int? thrownStep,
        OpError? failure,
        List<string> trace)
    {
        using var resource = new TrackedResource(trace);

        return OpResults.Ok()
            .Then(() => RunScopedStep(resource, 1, failedStep, thrownStep, failure))
            .Then(() => RunScopedStep(resource, 2, failedStep, thrownStep, failure));
    }

    private static OpResult RunScopedStep(
        TrackedResource resource,
        int step,
        int? failedStep,
        int? thrownStep,
        OpError? failure)
    {
        resource.Use(step);

        if (thrownStep == step)
        {
            throw new InvalidOperationException($"step {step} threw");
        }

        return failedStep == step
            ? failure!
            : OpResults.Ok();
    }

    private sealed class TrackedResource(List<string> trace) : IDisposable
    {
        private bool _isDisposed;

        public void Use(int step)
        {
            Assert.False(_isDisposed);
            trace.Add($"step:{step}");
        }

        public void Dispose()
        {
            _isDisposed = true;
            trace.Add("dispose");
        }
    }

    #endregion

    private sealed record Order(int Id);

    #region Docs_CSharpSnippetCompilation

    [Theory]
    [InlineData("then-chaining.md")]
    [InlineData("then-chaining.zh.md")]
    public void GuideCSharpCodeBlocks_CompileAgainstPublicApi(string guideFileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var guidePath = Path.Combine(repositoryRoot, "docs", guideFileName);
        var markdown = File.ReadAllText(guidePath);
        var codeBlocks = ExtractCSharpCodeBlocks(markdown);

        Assert.NotEmpty(codeBlocks);

        var errors = new List<string>();
        for (var i = 0; i < codeBlocks.Length; i++)
        {
            var diagnostics = CompileGuideCodeBlock(codeBlocks[i], i);
            errors.AddRange(diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => $"{guideFileName}, C# block {i + 1}: {diagnostic}"));
        }

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "OpResult.Tests", "OpResult.Tests.csproj")) &&
                    File.Exists(Path.Combine(directory.FullName, "docs", "then-chaining.md")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing docs/then-chaining.md.");
    }

    private static string[] ExtractCSharpCodeBlocks(string markdown) =>
        Regex.Matches(markdown, "```csharp\\r?\\n([\\s\\S]*?)\\r?\\n```")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static IReadOnlyList<Diagnostic> CompileGuideCodeBlock(string code, int index)
    {
        var parsedBlock = SyntaxFactory.ParseStatement($"{{{Environment.NewLine}{code}{Environment.NewLine}}}");
        if (parsedBlock is not BlockSyntax body)
        {
            throw new InvalidOperationException($"C# block {index + 1} did not parse as a method body.");
        }

        if (body.Statements.LastOrDefault() is not ReturnStatementSyntax)
        {
            body = body.AddStatements(SyntaxFactory.ParseStatement("return OpResults.Ok();"));
        }

        var method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("OpResult"),
                "Run")
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .WithBody(body);
        var source = $$"""
            #nullable enable
            using System;
            using OpResult;

            namespace OpResult.DocumentationTests;

            public static class ThenGuideSnippet{{index}}
            {
                private sealed record Order(int Id);
                private sealed class Resource : IDisposable
                {
                    public void Dispose() { }
                }

                private static OpResult<Order> LoadOrder() => OpResults.Ok(new Order(42));
                private static OpResult<Order> LoadOrder(int id) => OpResults.Ok(new Order(id));
                private static OpResult CheckPermission(Order order) => OpResults.Ok();
                private static OpResult SaveOrder(Order order) => OpResults.Ok();
                private static void Log(OpError error) { }
                private static Resource AcquireResource() => new();
                private static OpResult WriteFirstPart(Resource resource) => OpResults.Ok();
                private static OpResult WriteSecondPart(Resource resource) => OpResults.Ok();

            {{method.NormalizeWhitespace().ToFullString()}}
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp10));
        var compilation = CSharpCompilation.Create(
            assemblyName: $"ThenGuideSnippet{index}",
            syntaxTrees: [syntaxTree],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                warningLevel: 9999));

        return compilation.GetDiagnostics();
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];

        var references = new List<MetadataReference>(trustedAssemblies.Length + 1);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assemblyPath in trustedAssemblies)
        {
            if (seenPaths.Add(assemblyPath))
            {
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        var opResultAssemblyPath = typeof(OpResults).Assembly.Location;
        if (seenPaths.Add(opResultAssemblyPath))
        {
            references.Add(MetadataReference.CreateFromFile(opResultAssemblyPath));
        }

        return references.ToArray();
    }

    #endregion
}
