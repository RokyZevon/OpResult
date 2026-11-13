namespace OpResult.Analyzers.Tests;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using global::OpResult.Analyzers;
using Xunit;

internal static class AnalyzerTestHost
{
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string body, bool nullableEnabled = true)
    {
        var nullableDirective = nullableEnabled ? "#nullable enable" : "#nullable disable";
        var source = $$"""
            {{nullableDirective}}

            using global::OpResult;

            public sealed class User
            {
                public User(int id) => Id = id;
                public int Id { get; }
            }

            public static class Probe
            {
                public static global::OpResult.OpResult SaveUser(User user) => global::OpResult.OpResults.Ok();
                public static global::OpResult.OpResult<User> LoadUser(bool found) => found ? global::OpResult.OpResults.Ok(new User(1)) : global::OpResult.OpResults.Err("not found");
                public static global::OpResult.OpResult<int> LoadNumber(bool found) => found ? global::OpResult.OpResults.Ok(1) : global::OpResult.OpResults.Err("not found");

                public static void Run()
                {
            {{body}}
                }
            }
            """;

        return await GetDiagnosticsForSourceAsync(source);
    }

    public static Task<ImmutableArray<Diagnostic>> GetDiagnosticsForSourceAsync(string source) =>
        GetDiagnosticsForSourceAsync(source, ImmutableArray.Create<DiagnosticAnalyzer>(new OpResultUsageAnalyzer()));

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsForSourceAsync(
        string source,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        ImmutableDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        string sourcePath = "",
        bool allowCompilationErrors = false,
        bool includeOpResultReference = true,
        string assemblyName = "OpResultAnalyzerProbe",
        CancellationToken cancellationToken = default)
    {
        var compilation = CreateCompilation(source, diagnosticOptions, sourcePath, includeOpResultReference, assemblyName);
        var errors = compilation.GetDiagnostics(cancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (allowCompilationErrors)
        {
            Assert.NotEmpty(errors);
        }
        else
        {
            Assert.True(
                errors.Length == 0,
                $"Expected probe source to compile, but found errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString()))}");
        }

        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    public static void AssertDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
    {
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    public static void AssertNoDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
    {
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        ImmutableDictionary<string, ReportDiagnostic>? diagnosticOptions,
        string sourcePath,
        bool includeOpResultReference,
        string assemblyName)
    {
        return CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path: sourcePath)],
            references: GetMetadataReferences(includeOpResultReference),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                warningLevel: 9999,
                specificDiagnosticOptions: diagnosticOptions));
    }

    private static MetadataReference[] GetMetadataReferences(bool includeOpResultReference)
    {
        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? [];

        var references = new List<MetadataReference>(trustedAssemblies.Length + 1);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var opResultAssemblyPath = typeof(OpResults).Assembly.Location;
        if (!includeOpResultReference)
        {
            seenPaths.Add(opResultAssemblyPath);
        }

        foreach (var assemblyPath in trustedAssemblies)
        {
            if (seenPaths.Add(assemblyPath))
            {
                references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        if (seenPaths.Add(opResultAssemblyPath))
        {
            references.Add(MetadataReference.CreateFromFile(opResultAssemblyPath));
        }

        return references.ToArray();
    }
}
