# OpResult Single-Package Analyzer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 `RokyZevon.OpResult` 单包添加默认 Roslyn analyzer diagnostics，并把 analyzer DLL 作为 `analyzers/dotnet/cs` asset 打进现有 runtime NuGet 包。

**Architecture:** 保持 `OpResult/OpResult.csproj` 作为唯一公开 NuGet 包，不引入 Core / Analyzers / meta package。新增 `OpResult.Analyzers` 独立 analyzer assembly，新增 analyzer unit tests 和 package fixture tests；runtime project pack 时把 analyzer DLL 放入 nupkg 的 analyzer asset 路径。Analyzer 第一版只报告明确语义错误，默认 severity 为 warning，不提供 code fix。

**Tech Stack:** C# / .NET SDK 10, runtime TFMs `net10.0;net8.0;net6.0`, analyzer `netstandard2.0`, Roslyn `Microsoft.CodeAnalysis.CSharp`, xUnit v3 + Microsoft.Testing.Platform, local NuGet package fixture tests.

---

## File Structure

- Modify: `OpResult.slnx`
- Modify: `OpResult/OpResult.csproj`
- Modify: `OpResult.Tests/PackageMetadataTests.cs`
- Modify: `README.md`, `README.zh.md`
- Create: `OpResult.Analyzers/OpResult.Analyzers.csproj`
- Create: `OpResult.Analyzers/DiagnosticIds.cs`
- Create: `OpResult.Analyzers/DiagnosticDescriptors.cs`
- Create: `OpResult.Analyzers/OpResultUsageAnalyzer.cs`
- Create: `OpResult.Analyzers/OpResultSemanticFacts.cs`
- Create: `OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj`
- Create: `OpResult.Analyzers.Tests/AnalyzerTestHost.cs`
- Create: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`
- Create: `OpResult.Package.Tests/OpResult.Package.Tests.csproj`
- Create: `OpResult.Package.Tests/PackageFixtureTests.cs`

## Diagnostic ID Map

- `OPRESULT001`: Unguarded `Value` access.
- `OPRESULT002`: Unguarded `Error` access.
- `OPRESULT003`: Pseudo branch test based on `Value`, `Error`, or `Error.Message`.
- `OPRESULT004`: Unused `OpResult` return value in a bare expression statement.
- `OPRESULT005`: Direct error-chain loss by rebuilding an error from `result.Error.Message`.

## Task 1: Add Analyzer Project Skeleton

**Files:**
- Create: `OpResult.Analyzers/OpResult.Analyzers.csproj`
- Create: `OpResult.Analyzers/DiagnosticIds.cs`
- Create: `OpResult.Analyzers/DiagnosticDescriptors.cs`
- Create: `OpResult.Analyzers/OpResultUsageAnalyzer.cs`
- Create: `OpResult.Analyzers/OpResultSemanticFacts.cs`
- Modify: `OpResult.slnx`

- [ ] **Step 1: Add the analyzer project file**

Create `OpResult.Analyzers/OpResult.Analyzers.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <AssemblyName>RokyZevon.OpResult.Analyzers</AssemblyName>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.3.1" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add diagnostic ID constants**

Create `OpResult.Analyzers/DiagnosticIds.cs`. This type is public so analyzer tests and README examples can reference stable rule IDs without duplicating strings:

```csharp
namespace OpResult.Analyzers;

public static class DiagnosticIds
{
    public const string UnguardedValueAccess = "OPRESULT001";
    public const string UnguardedErrorAccess = "OPRESULT002";
    public const string PseudoBranchTest = "OPRESULT003";
    public const string UnusedResultReturnValue = "OPRESULT004";
    public const string DirectErrorChainLoss = "OPRESULT005";
}
```

- [ ] **Step 3: Add diagnostic descriptors**

Create `OpResult.Analyzers/DiagnosticDescriptors.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace OpResult.Analyzers;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnguardedValueAccess = new(
        DiagnosticIds.UnguardedValueAccess,
        "Read OpResult value only after proving success",
        "Read 'Value' only after proving the result is Ok",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnguardedErrorAccess = new(
        DiagnosticIds.UnguardedErrorAccess,
        "Read OpResult error only after proving failure",
        "Read 'Error' only after proving the result is Err",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PseudoBranchTest = new(
        DiagnosticIds.PseudoBranchTest,
        "Use IsOk or IsErr to test OpResult branches",
        "Use 'IsOk' or 'IsErr' instead of testing '{0}'",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnusedResultReturnValue = new(
        DiagnosticIds.UnusedResultReturnValue,
        "Consume OpResult return values",
        "The returned OpResult value is not consumed",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DirectErrorChainLoss = new(
        DiagnosticIds.DirectErrorChainLoss,
        "Preserve OpError chains when wrapping failures",
        "Wrap the original OpError instead of rebuilding an error from Error.Message",
        "OpResult.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
```

- [ ] **Step 4: Add analyzer entry point**

Create `OpResult.Analyzers/OpResultUsageAnalyzer.cs` with a compilable shell:

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpResult.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OpResultUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.UnguardedValueAccess,
            DiagnosticDescriptors.UnguardedErrorAccess,
            DiagnosticDescriptors.PseudoBranchTest,
            DiagnosticDescriptors.UnusedResultReturnValue,
            DiagnosticDescriptors.DirectErrorChainLoss);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
```

- [ ] **Step 5: Add semantic helper shell**

Create `OpResult.Analyzers/OpResultSemanticFacts.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace OpResult.Analyzers;

internal static class OpResultSemanticFacts
{
    public static bool IsOpResultType(ITypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        if (type is INamedTypeSymbol { Name: "OpResult", ContainingNamespace: { } ns })
        {
            return ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::OpResult";
        }

        return false;
    }
}
```

- [ ] **Step 6: Add analyzer project to the solution**

Modify `OpResult.slnx`:

```xml
<Solution>
  <Project Path="OpResult/OpResult.csproj" />
  <Project Path="OpResult.Analyzers/OpResult.Analyzers.csproj" />
  <Project Path="OpResult.Tests/OpResult.Tests.csproj" />
</Solution>
```

- [ ] **Step 7: Verify the skeleton builds**

Run:

```bash
dotnet build OpResult.Analyzers/OpResult.Analyzers.csproj -c Release
```

Expected: build succeeds with 0 errors.

## Task 2: Add Analyzer Unit Test Harness

**Files:**
- Create: `OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj`
- Create: `OpResult.Analyzers.Tests/AnalyzerTestHost.cs`
- Create: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`
- Modify: `OpResult.slnx`

- [ ] **Step 1: Add analyzer test project**

Create `OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
    <PackageReference Include="xunit.v3.mtp-v2" Version="3.2.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../OpResult/OpResult.csproj" />
    <ProjectReference Include="../OpResult.Analyzers/OpResult.Analyzers.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add the test project to the solution**

Modify `OpResult.slnx`:

```xml
<Solution>
  <Project Path="OpResult/OpResult.csproj" />
  <Project Path="OpResult.Analyzers/OpResult.Analyzers.csproj" />
  <Project Path="OpResult.Tests/OpResult.Tests.csproj" />
  <Project Path="OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj" />
</Solution>
```

- [ ] **Step 3: Add analyzer test host**

Create `OpResult.Analyzers.Tests/AnalyzerTestHost.cs`:

```csharp
namespace OpResult.Analyzers.Tests;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using OpResult.Analyzers;

internal static class AnalyzerTestHost
{
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string body, bool nullableEnabled = true)
    {
        var nullableDirective = nullableEnabled ? "#nullable enable" : "#nullable disable";
        var source = $$"""
            {{nullableDirective}}
            using OpResult;

            public sealed class User
            {
                public User(int id) => Id = id;
                public int Id { get; }
            }

            public static class Probe
            {
                public static OpResult SaveUser(User user) => OpResults.Ok();
                public static OpResult<User> LoadUser(bool found) => found ? OpResults.Ok(new User(1)) : OpResults.Err("not found");
                public static OpResult<int> LoadNumber(bool found) => found ? OpResults.Ok(1) : OpResults.Err("not found");

                public static void Run()
                {
            {{body}}
                }
            }
            """;

        var compilation = CreateCompilation(source);
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
```

- [ ] **Step 4: Add initial passing test for empty analyzer shell**

Create `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`:

```csharp
namespace OpResult.Analyzers.Tests;

using OpResult.Analyzers;

public sealed class OpResultUsageAnalyzerTests
{
    [Fact]
    public async Task EmptyMethod_DoesNotReportDiagnostics()
    {
        var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
            """
            var user = new User(1);
            _ = user;
            """);

        Assert.Empty(diagnostics);
    }
}
```

- [ ] **Step 5: Verify analyzer tests run**

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release
```

Expected: test project runs and the empty analyzer test passes.

## Task 3: Implement Unguarded `Value` / `Error` Diagnostics

**Files:**
- Modify: `OpResult.Analyzers/OpResultUsageAnalyzer.cs`
- Modify: `OpResult.Analyzers/OpResultSemanticFacts.cs`
- Modify: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`

- [ ] **Step 1: Add failing tests for unguarded access**

Append these tests to `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`:

```csharp
[Fact]
public async Task UnguardedValueAccess_ReportsDiagnostic()
{
    var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
        """
        var result = LoadUser(found: false);
        _ = result.Value;
        """);

    AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedValueAccess);
}

[Fact]
public async Task UnguardedErrorAccess_ReportsDiagnostic()
{
    var diagnostics = await AnalyzerTestHost.GetDiagnosticsAsync(
        """
        var result = LoadUser(found: true);
        _ = result.Error;
        """);

    AnalyzerTestHost.AssertDiagnostic(diagnostics, DiagnosticIds.UnguardedErrorAccess);
}
```

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release -- --filter-method UnguardedValueAccess_ReportsDiagnostic
```

Expected: test fails because `OPRESULT001` is not implemented.

- [ ] **Step 2: Add semantic helpers for result properties and branch facts**

Update `OpResult.Analyzers/OpResultSemanticFacts.cs` so it can:

- Identify `OpResult` and `OpResult<T>` symbols by namespace `OpResult` and name `OpResult`.
- Identify property references named `Value`, `Error`, `IsOk`, and `IsErr`.
- Compare receiver identity with a structured receiver key, not a bare member symbol. The key must distinguish `first.Cached` from `second.Cached` while still matching repeated reads of the same receiver expression.
- Recognize local branch proof patterns:
  - `if (result.IsOk) { result.Value; }`
  - `if (!result.IsErr) { result.Value; }`
  - `if (result.IsOk && user.Enabled) { result.Value; }`
  - `if (user.Enabled && result.IsOk) { result.Value; }`
  - `if (result.IsOk && result.Value.Id > 0) { }`
  - `if (result.IsOk || result.Error.Message.Length > 0) { }`
  - `if (result is { IsOk: true }) { result.Value; }`
  - `if (result.IsErr is false) { result.Value; }`
  - `if (result.IsErr) { result.Error; }`
  - `if (!result.IsOk) { result.Error; }`
  - `else` inverse branches.
  - early-return guards in the same block, including harmless statements between the guard and access.
  - loop-local `continue` / `break` guards that skip the current unsafe branch.
- Invalidate a proof when a reaching write changes the same receiver after the proof. Writes include assignment, deconstruction, and `ref` / `out` arguments.
- Invalidate early-exit proofs when the continuing branch writes the same receiver before the later access.
- Do not invalidate a proof for sibling member writes such as `holder.Other = 1` after guarding `holder.Cached`.
- Do not invalidate a proof for writes on paths that exit before the access with `return`, `throw`, or a `continue` / `break` that skips the access.
- Preserve short-circuit direction: later right-side guards can prove the post-left-mutation value, while later right-side same-function-boundary writes invalidate earlier left-side proofs.
- Let later operands in a C# short-circuit condition use facts proven by earlier operands, while still invalidating those facts for writes that occur before the guarded access.
- Do not expand collection / indexer receiver identity in this PR; property references with arguments are untrackable so a `results[0]` guard cannot prove `results[1]`.

Implementation constraints:

- Use Roslyn symbols inside the receiver key; do not use syntax text alone for receiver identity.
- Do not use string-only matching for type identity.
- Do not recognize custom guard helper methods.
- Do not treat `result.OnOk(...); result.Value` as proof.

- [ ] **Step 3: Register property-reference analysis**

Update `OpResult.Analyzers/OpResultUsageAnalyzer.cs` to register `OperationKind.PropertyReference` and report:

- `OPRESULT001` for unproven `Value`.
- `OPRESULT002` for unproven `Error`.

Use `IPropertyReferenceOperation` and skip non-OpResult receivers.

- [ ] **Step 4: Add non-diagnostic tests for allowed proof patterns**

Append tests covering:

```csharp
if (result.IsOk)
{
    _ = result.Value;
}
```

```csharp
if (result.IsErr)
{
    _ = result.Error;
}
```

```csharp
if (result.IsErr) return;
_ = result.Value;
```

```csharp
if (result.IsOk) return;
_ = result.Error;
```

```csharp
if (result.IsOk)
{
    _ = result.Value;
}
else
{
    _ = result.Error;
}
```

Each test should assert no `OPRESULT001` / `OPRESULT002`.

Also append PR review regression tests covering:

```csharp
if (first.Cached.IsOk)
{
    _ = second.Cached.Value;
}
```

Expected: `OPRESULT001`.

```csharp
if (holder.Cached.IsOk)
{
    holder.Other = 1;
    _ = holder.Cached.Value;
}
```

Expected: no `OPRESULT001`.

```csharp
if (result.IsOk && Replace(ref result))
{
    _ = result.Value;
}
```

Expected: `OPRESULT001`.

```csharp
if (Replace(ref result) && result.IsOk)
{
    _ = result.Value;
}
```

Expected: no `OPRESULT001`.

```csharp
if (result.IsOk && result.Value.Id > 0)
{
}
```

Expected: no `OPRESULT001`.

```csharp
if (result.IsOk || result.Error.Message.Length > 0)
{
}
```

Expected: no `OPRESULT002`.

```csharp
if (result.IsOk && Replace(ref result) && result.Value.Id > 0)
{
}
```

Expected: `OPRESULT001`.

```csharp
if (result.IsErr)
{
    return;
}
var id = 1;
_ = result.Value;
```

Expected: no `OPRESULT001`.

```csharp
for (var i = 0; i < 1; i++)
{
    if (result.IsErr)
    {
        continue;
    }
    _ = result.Value;
}
```

Expected: no `OPRESULT001`.

```csharp
if (result.IsErr)
{
    return;
}
else
{
    result = LoadUser(found: false);
}
_ = result.Value;
```

Expected: `OPRESULT001`.

```csharp
if (result.IsOk)
{
    for (var i = 0; i < 1; i++)
    {
        result = LoadUser(found: false);
        continue;
    }
    _ = result.Value;
}
```

Expected: `OPRESULT001`.

- [ ] **Step 5: Verify focused analyzer tests**

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release
```

Expected: unguarded access tests report diagnostics; guard, early-return, and else tests do not.

## Task 4: Implement Pseudo Branch Test Diagnostic

**Files:**
- Modify: `OpResult.Analyzers/OpResultUsageAnalyzer.cs`
- Modify: `OpResult.Analyzers/OpResultSemanticFacts.cs`
- Modify: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`

- [ ] **Step 1: Add failing pseudo-branch tests**

Add tests that report `OPRESULT003` for:

```csharp
var result = LoadUser(found: false);
if (result.Value != null) { }
```

```csharp
var result = LoadUser(found: false);
if (result.Value == null) { }
```

```csharp
var result = LoadUser(found: false);
if (result.Error != null) { }
```

```csharp
var result = LoadUser(found: false);
if (result.Error == null) { }
```

```csharp
var result = LoadUser(found: false);
if (result.Error.Message == "") { }
```

```csharp
var result = LoadUser(found: false);
if (result.Error.Message == string.Empty) { }
```

```csharp
var result = LoadUser(found: false);
if (result.Error.Message != "") { }
```

```csharp
var result = LoadUser(found: false);
if (result.Error.Message == System.String.Empty) { }
```

```csharp
var result = LoadUser(found: false);
if (result.Value is not null) { }
```

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release -- --filter-method ValueNullCheck_ReportsPseudoBranchDiagnostic
```

Expected: test fails because `OPRESULT003` is not implemented.

- [ ] **Step 2: Implement binary-expression detection**

Register syntax-node analysis for `EqualsExpression` and `NotEqualsExpression`.

Report `OPRESULT003` only for these exact pattern families:

- `result.Value == null`
- `result.Value != null`
- `result.Error == null`
- `result.Error != null`
- `result.Error.Message == ""`
- `result.Error.Message != ""`
- `result.Error.Message == string.Empty`
- `result.Error.Message != string.Empty`
- `result.Error.Message == String.Empty` / `System.String.Empty`
- `result.Value is null` / `is not null`
- `result.Error is null` / `is not null`

Do not report `string.IsNullOrEmpty(result.Error.Message)` or `result.Error.Message.Length == 0`.
Do not report `result.Error.Message == ""` or `result.Error.Message != ""` when the same `result.Error` access is already proven by an `IsErr` guard.

- [ ] **Step 3: Add non-diagnostic pseudo-branch boundary tests**

Add tests that do not report `OPRESULT003` for:

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    var message = result.Error.Message;
    _ = string.IsNullOrEmpty(message);
}
```

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    _ = result.Error.Message.Length == 0;
}
```

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    if (result.Error.Message == "")
    {
    }
}
```

- [ ] **Step 4: Verify focused analyzer tests**

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release
```

Expected: exact pseudo branch patterns report `OPRESULT003`; equivalent-looking message display logic does not.

## Task 5: Implement Unused Result Return Value Diagnostic

**Files:**
- Modify: `OpResult.Analyzers/OpResultUsageAnalyzer.cs`
- Modify: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`

- [ ] **Step 1: Add failing unused-result tests**

Add tests that report `OPRESULT004` for bare expression statements:

```csharp
SaveUser(new User(1));
```

```csharp
LoadUser(found: true).OnOk(user => _ = user.Id);
```

The second case still returns `OpResult<User>` from `OnOk`; if the final expression result is not consumed, it should report.

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release -- --filter-method BareOpResultCall_ReportsUnusedResultDiagnostic
```

Expected: test fails because `OPRESULT004` is not implemented.

- [ ] **Step 2: Implement expression-statement analysis**

Register operation analysis for `OperationKind.ExpressionStatement`.

Report `OPRESULT004` when the expression statement operation type is `OpResult` or `OpResult<T>`.

Do not report:

- Assignments.
- `return SaveUser(...)`.
- `if (SaveUser(...).IsErr) return;`.
- Calls where the final expression type is `void`.

- [ ] **Step 3: Add non-diagnostic unused-result tests**

Add tests that do not report `OPRESULT004` for:

```csharp
var result = SaveUser(new User(1));
_ = result;
```

```csharp
_ = SaveUser(new User(1));
```

```csharp
if (SaveUser(new User(1)).IsErr)
{
    return;
}
```

```csharp
SaveUser(new User(1)).Match(onOk: () => { }, onErr: error => _ = error.Message);
```

- [ ] **Step 4: Verify focused analyzer tests**

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release
```

Expected: bare unused `OpResult` statements report `OPRESULT004`; explicit discard assignment and consumed results do not.

## Task 6: Implement Direct Error-Chain Loss Diagnostic

**Files:**
- Modify: `OpResult.Analyzers/OpResultUsageAnalyzer.cs`
- Modify: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`

- [ ] **Step 1: Add failing direct-chain-loss tests**

Add tests that report `OPRESULT005`:

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    _ = OpResults.Err(result.Error.Message);
}
```

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    OpResult<User> wrapped = OpResults.Err<User>(result.Error.Message);
    _ = wrapped;
}
```

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release -- --filter-method DirectErrorMessageRebuild_ReportsChainLossDiagnostic
```

Expected: test fails because `OPRESULT005` is not implemented.

- [ ] **Step 2: Implement invocation detection**

Register operation analysis for invocations.

Report `OPRESULT005` when:

- Target method is `OpResult.OpResults.Err` or generic `OpResult.OpResults.Err<T>`.
- The invocation has exactly one argument, or has two arguments where the inner-error argument is a null constant.
- That argument is direct `result.Error.Message`.
- Arguments are matched by target parameter, not source order, so reordered named arguments are covered.
- The `result.Error` access is proven to be in an `Err` branch by the same local proof helper used for `OPRESULT002`.

Do not report:

- `OpResults.Err("outer", result.Error)`.
- `result.Error.ToErr("outer")`.
- `var message = result.Error.Message; OpResults.Err(message);`.
- `OpResults.Err($"Failed: {result.Error.Message}")`.
- `OpResults.Err(result.Error!.Message)` in this PR; null-forgiving suppression unwrapping is intentionally left for a future diagnostic-surface decision.

- [ ] **Step 3: Add non-diagnostic chain-preservation tests**

Add tests that do not report `OPRESULT005`:

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    _ = result.Error.ToErr("Could not load profile.");
}
```

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    _ = OpResults.Err("Could not load profile.", result.Error);
}
```

```csharp
var result = LoadUser(found: false);
if (result.IsErr)
{
    var message = result.Error.Message;
    _ = OpResults.Err(message);
}
```

- [ ] **Step 4: Verify focused analyzer tests**

Run:

```bash
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release
```

Expected: direct message rebuild reports `OPRESULT005`; chain-preserving APIs and non-direct message use do not.

## Task 7: Pack Analyzer Into The Runtime Package

**Files:**
- Modify: `OpResult/OpResult.csproj`
- Modify: `OpResult.Tests/PackageMetadataTests.cs`

- [ ] **Step 1: Add analyzer DLL pack item**

Modify `OpResult/OpResult.csproj` to reference the analyzer project for build ordering and pack the analyzer DLL:

```xml
  <ItemGroup>
    <ProjectReference Include="../OpResult.Analyzers/OpResult.Analyzers.csproj"
                      ReferenceOutputAssembly="false"
                      PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <None Include="../OpResult.Analyzers/bin/$(Configuration)/netstandard2.0/RokyZevon.OpResult.Analyzers.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
  </ItemGroup>
```

Do not set `OutputItemType="Analyzer"` on this `ProjectReference`; the runtime package project needs the reference for build ordering only. The analyzer DLL is still included in the package by the explicit `<None Pack="true" PackagePath="analyzers/dotnet/cs">` item above.

Keep the existing README pack item unchanged:

```xml
  <ItemGroup>
    <None Include="../README.md" Pack="true" PackagePath="" />
  </ItemGroup>
```

- [ ] **Step 2: Add package metadata test for analyzer pack item**

Add a test to `OpResult.Tests/PackageMetadataTests.cs`:

```csharp
[Fact]
public void OpResultProject_PacksAnalyzerAtNuGetAnalyzerPath()
{
    var analyzerItem = LoadProject()
        .Descendants("None")
        .Single(element => ((string?)element.Attribute("Include"))?.Contains("RokyZevon.OpResult.Analyzers.dll", StringComparison.Ordinal) == true);

    Assert.Equal("true", (string?)analyzerItem.Attribute("Pack"));
    Assert.Equal("analyzers/dotnet/cs", (string?)analyzerItem.Attribute("PackagePath"));
    Assert.Equal("false", (string?)analyzerItem.Attribute("Visible"));
}
```

- [ ] **Step 3: Add package metadata test for analyzer project reference**

Add a test to `OpResult.Tests/PackageMetadataTests.cs`:

```csharp
[Fact]
public void OpResultProject_ReferencesAnalyzerProjectOnlyForBuildOrdering()
{
    var projectReference = LoadProject()
        .Descendants("ProjectReference")
        .Single(element => (string?)element.Attribute("Include") == "../OpResult.Analyzers/OpResult.Analyzers.csproj");

    Assert.Equal("false", (string?)projectReference.Attribute("ReferenceOutputAssembly"));
    Assert.Equal("all", (string?)projectReference.Attribute("PrivateAssets"));
    Assert.Null(projectReference.Attribute("OutputItemType"));
}
```

- [ ] **Step 4: Verify runtime build still succeeds**

Run:

```bash
dotnet build OpResult/OpResult.csproj -c Release
```

Expected: runtime project builds, analyzer project builds first, and no Roslyn reference is added to runtime compile assets.

## Task 8: Add Package Fixture Tests

**Files:**
- Create: `OpResult.Package.Tests/OpResult.Package.Tests.csproj`
- Create: `OpResult.Package.Tests/PackageFixtureTests.cs`
- Modify: `OpResult.slnx`

- [ ] **Step 1: Add package fixture test project**

Create `OpResult.Package.Tests/OpResult.Package.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3.mtp-v2" Version="3.2.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../OpResult/OpResult.csproj" ReferenceOutputAssembly="false" SetTargetFramework="TargetFramework=net6.0" />
    <ProjectReference Include="../OpResult/OpResult.csproj" ReferenceOutputAssembly="false" SetTargetFramework="TargetFramework=net8.0" />
    <ProjectReference Include="../OpResult/OpResult.csproj" ReferenceOutputAssembly="false" SetTargetFramework="TargetFramework=net10.0" />
    <ProjectReference Include="../OpResult.Analyzers/OpResult.Analyzers.csproj" ReferenceOutputAssembly="false" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add test project to solution**

Modify `OpResult.slnx`:

```xml
<Solution>
  <Project Path="OpResult/OpResult.csproj" />
  <Project Path="OpResult.Analyzers/OpResult.Analyzers.csproj" />
  <Project Path="OpResult.Tests/OpResult.Tests.csproj" />
  <Project Path="OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj" />
  <Project Path="OpResult.Package.Tests/OpResult.Package.Tests.csproj" />
</Solution>
```

- [ ] **Step 3: Add package fixture tests**

Create `OpResult.Package.Tests/PackageFixtureTests.cs` with tests that:

- Run `dotnet pack OpResult/OpResult.csproj -c Release --no-build --no-restore -o <temp-packages> -p:ContinuousIntegrationBuild=true`.
- Assert the fixture itself is running under `Release`; if it is launched as Debug or from an IDE default configuration, fail clearly with `dotnet test OpResult.Package.Tests/OpResult.Package.Tests.csproj -c Release`.
- Locate the single generated `RokyZevon.OpResult.*.nupkg` file in `<temp-packages>` without reading or asserting its version.
- Inspect the `.nupkg` with `System.IO.Compression.ZipArchive`.
- Assert these entries exist:
  - `lib/net6.0/OpResult.dll`
  - `lib/net8.0/OpResult.dll`
  - `lib/net10.0/OpResult.dll`
  - `analyzers/dotnet/cs/RokyZevon.OpResult.Analyzers.dll`
  - `README.md`
- Assert no analyzer DLL exists under `lib/`, `ref/`, or `runtime/`.
- Create a temporary consumer project with a local `RestoreSources` pointing only at the packed package directory and `PackageReference Include="RokyZevon.OpResult" Version="*"`.
- Build a consumer source file containing:

```csharp
using OpResult;

var result = OpResults.Ok(1);
_ = result.Error;
```

- Assert `dotnet build` output contains `OPRESULT002`.

Implementation notes:

- Use `ProcessStartInfo` with `RedirectStandardOutput = true` and `RedirectStandardError = true`.
- Start `StandardOutput.ReadToEndAsync()` and `StandardError.ReadToEndAsync()` before awaiting process exit so noisy failing commands cannot block on a full pipe.
- Set `WorkingDirectory` to the repository root.
- The fixture project references every runtime target framework plus the analyzer project with `ReferenceOutputAssembly="false"` so `--no-build` pack has all expected build outputs when package tests run directly.
- Give each fixture test its own directory under `Path.GetTempPath()`.
- Do not write fixture projects into the repository.

- [ ] **Step 4: Verify package fixture tests**

Run:

```bash
dotnet test OpResult.Package.Tests/OpResult.Package.Tests.csproj -c Release
```

Expected: fixture tests pack the local package, verify nupkg layout, and prove analyzer diagnostics appear in `dotnet build`.

## Task 9: Update README Documentation

**Files:**
- Modify: `README.md`
- Modify: `README.zh.md`

- [ ] **Step 1: Update English installation section**

Change `README.md` installation text to state the package includes runtime and analyzer diagnostics:

````markdown
## Installation

```bash
dotnet add package RokyZevon.OpResult
```

The package includes the runtime library and the default Roslyn analyzers. Analyzer diagnostics are part of the default OpResult semantics and also run in command-line builds.
````

- [ ] **Step 2: Add English diagnostics section**

Add a concise `## Analyzer Diagnostics` section documenting:

- `OPRESULT001`: read `Value` only after proving `IsOk`.
- `OPRESULT002`: read `Error` only after proving `IsErr`.
- `OPRESULT003`: use `IsOk` / `IsErr`, not null or empty-message pseudo branch tests.
- `OPRESULT004`: consume returned `OpResult` values.
- `OPRESULT005`: preserve `OpError` chains with `ToErr` or `OpResults.Err(message, innerError)`.

State that first-version diagnostics have default severity `warning`.

Include a concrete bad / good guard example, for example:

````markdown
Bad example:

```csharp
OpResult<User> result = FindUser(userId);
return result.Value.DisplayName; // OPRESULT001
```

Good example:

```csharp
OpResult<User> result = FindUser(userId);

if (result.IsErr)
{
    return $"Could not load user: {result.Error.Message}";
}

return result.Value.DisplayName;
```
````

Include `.editorconfig` example:

```ini
[*.cs]
dotnet_diagnostic.OPRESULT001.severity = error
dotnet_diagnostic.OPRESULT004.severity = none
```

- [ ] **Step 3: Mirror the documentation in Chinese**

Update `README.zh.md` with equivalent Chinese text and keep the existing language-switch link labels unchanged.

- [ ] **Step 4: Verify docs do not mention removed package shapes**

Run:

```bash
rg -n "RokyZevon\\.OpResult\\.Core|RokyZevon\\.OpResult\\.Analyzers|runtime-only|analyzer-only|meta package" README.md README.zh.md
```

Expected: no matches.

## Task 10: Final Verification

**Files:**
- All files touched by Tasks 1-9.

- [ ] **Step 1: Restore**

Run:

```bash
dotnet restore OpResult.slnx
```

Expected: restore succeeds.

- [ ] **Step 2: Build**

Run:

```bash
dotnet build OpResult.slnx -c Release --no-restore -m:1 -nr:false
```

Expected: build succeeds.

- [ ] **Step 3: Run all tests**

Run:

```bash
dotnet test OpResult.slnx -c Release --no-build --no-restore
```

Expected: all runtime, analyzer, and package fixture tests pass.

- [ ] **Step 4: Pack**

Run:

```bash
dotnet pack OpResult/OpResult.csproj -c Release --no-build --no-restore --output /tmp/opresult-analyzer-pack-check -p:ContinuousIntegrationBuild=true -m:1 -nr:false
```

Expected: `.nupkg` and `.snupkg` are created; `.nupkg` contains `analyzers/dotnet/cs/RokyZevon.OpResult.Analyzers.dll`.

- [ ] **Step 5: Check git diff**

Run:

```bash
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 6: Commit only if the user explicitly permits git writes**

Repository instructions normally forbid git write operations. If the user explicitly permits a commit for this implementation run, use:

```bash
git add OpResult.slnx OpResult OpResult.Analyzers OpResult.Tests OpResult.Analyzers.Tests OpResult.Package.Tests README.md README.zh.md
git commit -m "feat: add OpResult analyzer diagnostics"
```

Expected: one conventional commit containing analyzer implementation, tests, packaging, and docs.

## Assumptions

- `RokyZevon.OpResult` remains the only public NuGet package.
- Analyzer project is build-only and is not published as `RokyZevon.OpResult.Analyzers`.
- Analyzer project compiles against Roslyn package version `4.3.1` so the generic `analyzers/dotnet/cs` asset remains loadable by the .NET 6 SDK compiler host.
- Test projects may use a newer Roslyn package because those dependencies are test-only and are not packed into the runtime NuGet package.
- First version uses IDs `OPRESULT001` through `OPRESULT005`; no separate value-type-only diagnostic is created.
- No code fix is included.
- Default severity is warning for every first-version diagnostic.
- Git write commands require explicit user permission at execution time.
