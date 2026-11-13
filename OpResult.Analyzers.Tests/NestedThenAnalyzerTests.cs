namespace OpResult.Analyzers.Tests;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

public sealed class NestedThenAnalyzerTests
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> s_analyzers = [new NestedThenAnalyzer()];

    [Fact]
    public void Descriptor_HasExpectedDefaults()
    {
        var descriptor = Assert.Single(s_analyzers[0].SupportedDiagnostics);
        Assert.Equal("OPRESULT006", descriptor.Id);
        Assert.Equal("Prefer flat Then chains", descriptor.Title.ToString());
        Assert.Equal("OpResult.Usage", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault);
        Assert.Equal("https://github.com/RokyZevon/OpResult/blob/main/docs/then-chaining.md", descriptor.HelpLinkUri);
    }

    [Theory]
    [InlineData("Ok().[|Then|](() => Ok().Then(() => Ok()))")]
    [InlineData("Ok().[|Then|](() => Ok().Then(() => Ok(1)))")]
    [InlineData("Ok(1).[|Then|](value => Ok(value).Then(inner => Ok(inner.ToString())))")]
    [InlineData("Ok(1).[|Then|](value => Ok(value).Then(inner => Ok()))")]
    [InlineData("Ok(1).[|Then|]<int, string>(value => Ok(value).Then<int, string>(inner => Ok(inner.ToString())))")]
    [InlineData("OpResultExtensions.[|Then|](onOk: () => OpResultExtensions.Then(onOk: () => Ok(), result: Ok()), result: Ok())")]
    [InlineData("Ok().[|Then|](delegate { return Ok().Then(() => Ok()); })")]
    public Task DirectReturn_ReportsAtThenName(string expression) => AssertProbeAsync($"_ = {expression};");

    [Fact]
    public Task UsingStaticExtensionLookup_ReportsAtThenName() => AssertProbeAsync(
        "_ = Ok().[|Then|](onOk: () => Ok().Then(onOk: () => Ok()));", useStaticExtensionImport: true);

    [Fact]
    public Task ReturnedChain_ReportsOnceForDeepAndBranchedNesting() => AssertProbeAsync(
        """
        _ = Ok(1).[|Then|]<int, int>(x => Ok(x).Then<int, int>(y =>
            Ok(y).Then<int, int>(z => Ok(z).Then<int, int>(a => Ok(a)))));

        _ = Ok().[|Then|](() =>
        {
            if (choose)
            {
                return ((Result)Ok().Then(() => Ok()).OnOk(() => { }));
            }

            return number switch
            {
                1 => OpResultExtensions.OnErr(onErr: _ => { }, result: Ok().Then(() => Ok().Then(() => Ok()))),
                2 => choose ? Ok() : Ok().Then(() => Ok()),
                _ => Ok()
            };
        });
        """);

    [Fact]
    public Task ReturnedReceiverChain_TreatsEveryThenAtThatLevelAsAChild() => AssertProbeAsync(
        """
        _ = Ok().[|Then|](() => (choose ? Ok().Then(() => Ok().Then(() => Ok())) : Ok())
            .OnOk(() => { })
            .Then(() => Ok().Then(() => Ok())));
        """);

    [Theory]
    [InlineData("choose ? Ok().Then(() => Ok()) : new AlternativeResult()", true)]
    [InlineData("number switch { 0 => Ok().Then(() => Ok()), _ => new AlternativeResult() }", true)]
    [InlineData("choose ? (AlternativeResult)Ok().Then(() => Ok()) : Ok()", false)]
    [InlineData("number switch { 0 => (AlternativeResult)Ok().Then(() => Ok()), _ => Ok() }", false)]
    public Task TargetTypedBranches_RespectUserDefinedConversions(string expression, bool shouldReport) => AssertProbeAsync(
        $"_ = OpResultExtensions.{(shouldReport ? "[|Then|]" : "Then")}(Ok(), (Func<Result>)(() => {expression}));");

    [Fact]
    public Task IndependentChains_EachReportAtTheirOwnRoot() => AssertProbeAsync(
        """
        _ = Ok().[|Then|](() =>
        {
            _ = Ok().[|Then|](() => Ok().Then(() => Ok()));
            _ = Ok().Then(() => Ok());
            return Ok().Then(() => Ok());
        });

        _ = Ok().[|Then|](() => Ok().Then(() => Ok())).[|Then|](() => Ok().Then(() => Ok()));
        """);

    [Fact]
    public Task FunctionBoundaries_DoNotCrossButAnalyzeNestedChainsIndependently() => AssertProbeAsync(
        """
        _ = Ok().Then(() =>
        {
            Result SingleStep() => Ok().Then(() => Ok());
            Result NestedSteps() => Ok().[|Then|](() => Ok().Then(() => Ok()));
            Func<Result> singleDelegate = () => Ok().Then(() => Ok());
            Func<Result> nestedDelegate = () => Ok().[|Then|](() => Ok().Then(() => Ok()));
            _ = singleDelegate();
            _ = nestedDelegate();
            _ = Ok().Then(NestedSteps);
            _ = Ok().Then(singleDelegate);
            return SingleStep();
        });
        """);

    [Fact]
    public Task FlatOrIndirectThenChains_DoNotReport() => AssertProbeAsync(
        """
        _ = Ok().Then(() => Ok()).Then(() => Ok()).Then(() => Ok()).Then(() => Ok());
        _ = Ok().Then(() =>
        {
            var audit = Ok().Then(() => Ok());
            _ = audit.IsOk;
            return audit;
        });
        _ = Ok().Then(() => Wrap(Ok().Then(() => Ok())));
        _ = Ok().Then(() => Ok().Then(() => Ok()).WrapExtension());
        _ = Ok().Then(() => Ok().Then(() => Ok()).Match<Result>(() => Ok(), _ => Ok()));
        _ = Ok().Then(() => Ok().Match<Result>(() => Ok().Then(() => Ok()), _ => Ok()));
        _ = Ok().Then<Result>(() => Ok().Then(() => Ok()));
        _ = Other.Then(() => Ok().Then(() => Ok()));
        _ = new OtherResult().Then(() => new OtherResult().Then(() => new OtherResult()));
        _ = Ok().ThenAsync(() => Task.FromResult(Ok().Then(() => Ok())));
        _ = Ok().Then(() => Ok().ThenAsync(() => Task.FromResult(Ok())).GetAwaiter().GetResult());
        _ = Ok().Then(() => Ok().Then(() => Ok()).IsOk ? Ok() : Ok());
        _ = Ok().Then(() => Ok().Then(() => Ok()).IsOk switch { true => Ok(), false => Ok() });
        _ = Ok().Then(() => Ok().OnOk(() => { _ = Ok().Then(() => Ok()); }));
        _ = Ok().Then(() => Other.OnErr(Ok().Then(() => Ok())));
        """);

    [Fact]
    public async Task MalformedOrAmbiguousInvocations_DoNotReportOrCrash()
    {
        var source = Probe(
            """
            _ = Ok().Then(() => Ok().Then(() => ));
            _ = Ok().Then(() => Ok().Then(() => Missing()));
            _ = Ok().Then(() => Ok(1).Then(value => Ambiguous(null)));
            """);

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            source, s_analyzers, allowCompilationErrors: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task UnresolvedExplicitTypeArguments_DoNotReportOrCrash()
    {
        var source = Probe("_ = Ok().Then<Missing>(() => Ok().Then<Missing>(() => default));");
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            source, s_analyzers, allowCompilationErrors: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MissingOpResultReference_DoesNotReportOrCrash()
    {
        var source = Probe("_ = Ok().Then(() => Ok().Then(() => Ok()));");
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            source, s_analyzers, allowCompilationErrors: true, includeOpResultReference: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("OpResultAnalyzerProbe", true)]
    [InlineData("OpResult", false)]
    public async Task SameMetadataNameInConsumer_DoesNotImpersonateRuntimeExtensions(string assemblyName, bool includeOpResultReference)
    {
        const string source = """
            using System;

            namespace OpResult;

            public static class OpResultExtensions
            {
                public static int Then(this int result, Func<int> onOk) => onOk();
                public static int Run() => 1.Then(() => 2.Then(() => 3));
            }
            """;

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            source, s_analyzers, includeOpResultReference: includeOpResultReference, assemblyName: assemblyName,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("", "Probe.g.cs")]
    [InlineData("// <auto-generated/>\n", "Probe.cs")]
    public async Task GeneratedCode_DoesNotReport(string header, string path)
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            header + Probe("_ = Ok().Then(() => Ok().Then(() => Ok()));"), s_analyzers, sourcePath: path,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task SpecificDiagnosticSuppression_DoesNotSuppressExistingAnalyzer()
    {
        var source = Probe(
            """
            _ = Ok().Then(() => Ok().Then(() => Ok()));
            var unproven = Ok(1);
            _ = unproven.Value;
            """);
        var diagnosticOptions = ImmutableDictionary<string, ReportDiagnostic>.Empty.Add("OPRESULT006", ReportDiagnostic.Suppress);
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            source, s_analyzers.Add(new OpResultUsageAnalyzer()), diagnosticOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("OPRESULT001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task AlreadyCancelledAnalysis_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            Probe("_ = Ok().Then(() => Ok().Then(() => Ok()));"), s_analyzers, cancellationToken: cancellation.Token));
    }

    private static async Task AssertProbeAsync(string markedBody, bool useStaticExtensionImport = false)
    {
        var source = Probe(markedBody, useStaticExtensionImport);
        var expectedSpans = new List<TextSpan>();
        for (var start = source.IndexOf("[|Then|]", StringComparison.Ordinal); start >= 0;
            start = source.IndexOf("[|Then|]", StringComparison.Ordinal))
        {
            expectedSpans.Add(new TextSpan(start, "Then".Length));
            source = source.Remove(start, "[|Then|]".Length).Insert(start, "Then");
        }

        var diagnostics = await AnalyzerTestHost.GetDiagnosticsForSourceAsync(
            source, s_analyzers, cancellationToken: TestContext.Current.CancellationToken);
        var orderedDiagnostics = diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToArray();
        Assert.Equal(expectedSpans, orderedDiagnostics.Select(diagnostic => diagnostic.Location.SourceSpan));
        Assert.All(orderedDiagnostics, diagnostic =>
        {
            Assert.Equal("OPRESULT006", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal("Prefer a flat Then chain over returning a nested Then chain.", diagnostic.GetMessage());
        });
    }

    private static string Probe(string body, bool useStaticExtensionImport = false) => $$"""
        using System;
        using System.Threading.Tasks;
        {{(useStaticExtensionImport ? "using static OpResult.OpResultExtensions;" : "using OpResult;")}}
        using static OpResult.OpResults;
        using Result = OpResult.OpResult;

        public static class Probe
        {
            private static Result Wrap(Result result) => result;
            private static Result WrapExtension(this Result result) => result;
            private static Result Ambiguous(string? value) => Ok();
            private static Result Ambiguous(Uri? value) => Ok();

            public static void Run(bool choose, int number)
            {
        {{body}}
            }
        }

        public static class Other
        {
            public static Result Then(Func<Result> onOk) => onOk();
            public static Result OnErr(Result result) => result;
        }

        public struct OtherResult
        {
            public OtherResult Then(Func<OtherResult> onOk) => onOk();
        }

        public struct AlternativeResult
        {
            public static implicit operator AlternativeResult(Result result) => default;
            public static implicit operator Result(AlternativeResult result) => Ok();
        }
        """;
}
