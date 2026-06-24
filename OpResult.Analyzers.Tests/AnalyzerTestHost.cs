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

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsForSourceAsync(string source)
    {
        var compilation = CreateCompilation(source);
        var compilationDiagnostics = compilation.GetDiagnostics();
        var errors = compilationDiagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(
            errors.Length == 0,
            $"Expected probe source to compile, but found errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(diagnostic => diagnostic.ToString()))}");

        var analyzer = new OpResultUsageAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    public static void AssertDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
    {
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    public static void AssertNoDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
    {
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
            assemblyName: "OpResultAnalyzerProbe",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                warningLevel: 9999));
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
}
