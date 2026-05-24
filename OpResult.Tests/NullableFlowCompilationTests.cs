namespace OpResult.Tests;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public class NullableFlowCompilationTests
{
    private static readonly string[] NotNullConstraintDiagnosticIds = ["CS8714", "CS8631", "CS8634", "CS8625"];

    [Fact]
    public void GuardedOkPath_ValueDereference_DoesNotReportCs8602()
    {
        var diagnostics = CompileSnippet(
            """
            var result = OpResults.Ok(new User(1));
            if (!result.IsOk) return;
            var user = result.Value;
            var id = user.Id;
            _ = id;
            """);

        AssertNoDiagnostic(diagnostics, "CS8602");
    }

    [Fact]
    public void NegatedErrGuardedOkPath_ValueDereference_DoesNotReportCs8602()
    {
        var diagnostics = CompileSnippet(
            """
            var result = OpResults.Ok(new User(1));
            if (result.IsErr) return;
            var user = result.Value;
            var id = user.Id;
            _ = id;
            """);

        AssertNoDiagnostic(diagnostics, "CS8602");
    }

    [Fact]
    public void GuardedErrPath_ErrorDereference_DoesNotReportCs8602()
    {
        var diagnostics = CompileSnippet(
            """
            var result = OpResults.Err<User>("failed");
            if (!result.IsErr) return;
            var message = result.Error.Message;
            _ = message;
            """);

        AssertNoDiagnostic(diagnostics, "CS8602");
    }

    [Fact]
    public void NegatedOkGuardedErrPath_ErrorDereference_DoesNotReportCs8602()
    {
        var diagnostics = CompileSnippet(
            """
            var result = OpResults.Err<User>("failed");
            if (result.IsOk) return;
            var message = result.Error.Message;
            _ = message;
            """);

        AssertNoDiagnostic(diagnostics, "CS8602");
    }

    [Fact]
    public void UnguardedValueDereference_ReportsCs8602()
    {
        var diagnostics = CompileSnippet(
            """
            var result = OpResults.Err<User>("failed");
            var id = result.Value.Id;
            _ = id;
            """);

        AssertHasDiagnostic(diagnostics, "CS8602");
    }

    [Fact]
    public void UnguardedErrorDereference_ReportsCs8602()
    {
        var diagnostics = CompileSnippet(
            """
            var result = OpResults.Ok(new User(1));
            var message = result.Error.Message;
            _ = message;
            """);

        AssertHasDiagnostic(diagnostics, "CS8602");
    }

    [Fact]
    public void NullableTypeArgument_OnOpResultOfT_ReportsNotNullOrNullabilityDiagnostic()
    {
        var diagnostics = CompileSnippet(
            """
            OpResult<User?> result = default;
            _ = result;
            """);

        AssertHasAnyDiagnostic(diagnostics, NotNullConstraintDiagnosticIds);
    }

    [Fact]
    public void NullableTypeArgument_OnOkFactory_ReportsNotNullOrNullabilityDiagnostic()
    {
        var diagnostics = CompileSnippet(
            """
            User? user = new User(1);
            var result = OpResults.Ok<User?>(user);
            _ = result;
            """);

        AssertHasAnyDiagnostic(diagnostics, ["CS8714", "CS8631", "CS8634"]);
    }

    private static ImmutableArray<Diagnostic> CompileSnippet(string body)
    {
        var source = $$"""
            #nullable enable
            using OpResult;

            public sealed class User
            {
                public User(int id) => Id = id;
                public int Id { get; }
            }

            public static class NullableFlowProbe
            {
                public static void Run()
                {
            {{body}}
                }
            }
            """;

        return CompileSource(source);
    }

    private static ImmutableArray<Diagnostic> CompileSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName: "NullableFlowProbeAssembly",
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

    private static void AssertNoDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
    {
        AssertNoUnexpectedErrors(diagnostics);

        var matches = diagnostics.Where(d => d.Id == diagnosticId).ToArray();
        Assert.True(
            matches.Length == 0,
            $"Expected no {diagnosticId} diagnostics, but found:{Environment.NewLine}{FormatDiagnostics(diagnostics)}");
    }

    private static void AssertHasDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
    {
        AssertNoUnexpectedErrors(diagnostics);

        var matches = diagnostics.Where(d => d.Id == diagnosticId).ToArray();
        Assert.True(
            matches.Length > 0,
            $"Expected {diagnosticId} diagnostics, but found:{Environment.NewLine}{FormatDiagnostics(diagnostics)}");
    }

    private static void AssertHasAnyDiagnostic(ImmutableArray<Diagnostic> diagnostics, IReadOnlyCollection<string> diagnosticIds)
    {
        AssertNoUnexpectedErrors(diagnostics);

        var matches = diagnostics.Where(d => diagnosticIds.Contains(d.Id)).ToArray();
        Assert.True(
            matches.Length > 0,
            $"Expected one of [{string.Join(", ", diagnosticIds)}], but found:{Environment.NewLine}{FormatDiagnostics(diagnostics)}");
    }

    private static void AssertNoUnexpectedErrors(ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(
            errors.Length == 0,
            $"Expected snippets to compile without errors, but found:{Environment.NewLine}{FormatDiagnostics(diagnostics)}");
    }

    private static string FormatDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsDefaultOrEmpty)
        {
            return "(none)";
        }

        return string.Join(
            Environment.NewLine,
            diagnostics.Select(d => $"{d.Id} {d.Severity}: {d.GetMessage()}"));
    }
}
