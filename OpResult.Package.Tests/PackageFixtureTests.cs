namespace OpResult.Package.Tests;

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

public sealed class PackageFixtureTests
{
    private const string PackageId = "RokyZevon.OpResult";
    private const string AnalyzerPath = "analyzers/dotnet/cs/RokyZevon.OpResult.Analyzers.dll";
    private const string RequiredConfiguration = "Release";

    [Fact]
    public void PackageFixture_RejectsNonReleaseConfiguration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AssertReleaseConfiguration("Debug"));

        Assert.Contains("dotnet test OpResult.Package.Tests/OpResult.Package.Tests.csproj -c Release", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PackedPackage_ContainsRuntimeAndAnalyzerAssets()
    {
        var tempRoot = CreateTempRoot();
        var passed = false;

        try
        {
            var packagesDirectory = Path.Combine(tempRoot, "packages");
            Directory.CreateDirectory(packagesDirectory);

            await PackAsync(packagesDirectory);
            var packagePath = GetPackagePath(packagesDirectory);

            using var package = ZipFile.OpenRead(packagePath);
            var entryNames = package.Entries
                .Select(entry => entry.FullName)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Contains("lib/net6.0/OpResult.dll", entryNames);
            Assert.Contains("lib/net8.0/OpResult.dll", entryNames);
            Assert.Contains("lib/net10.0/OpResult.dll", entryNames);
            Assert.Contains(AnalyzerPath, entryNames);
            Assert.Contains("README.md", entryNames);

            var misplacedAnalyzerEntries = entryNames
                .Where(IsMisplacedRuntimeAsset)
                .ToArray();

            Assert.Empty(misplacedAnalyzerEntries);
            AssertDoesNotDeclareDependencies(package);
            passed = true;
        }
        finally
        {
            CleanupIfPassed(tempRoot, passed);
        }
    }

    [Fact]
    public async Task PackedPackage_ReportsAnalyzerDiagnosticInConsumerBuild()
    {
        var tempRoot = CreateTempRoot();
        var passed = false;

        try
        {
            var packagesDirectory = Path.Combine(tempRoot, "packages");
            Directory.CreateDirectory(packagesDirectory);

            await PackAsync(packagesDirectory);

            var consumerDirectory = Path.Combine(tempRoot, "consumer");
            Directory.CreateDirectory(consumerDirectory);
            WriteConsumerProject(consumerDirectory, packagesDirectory);
            WriteConsumerSource(consumerDirectory);

            var result = await RunDotnetAsync(
                "build",
                Path.Combine(consumerDirectory, "Consumer.csproj"),
                "-c",
                "Release");

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Contains("OPRESULT002", result.ToString(), StringComparison.Ordinal);
            passed = true;
        }
        finally
        {
            CleanupIfPassed(tempRoot, passed);
        }
    }

    [Fact]
    public async Task PackedPackage_NestedThenWarningSupportsSeverityConfigurationAndFlatChains()
    {
        var tempRoot = CreateTempRoot();
        var passed = false;

        try
        {
            var packagesDirectory = Path.Combine(tempRoot, "packages");
            Directory.CreateDirectory(packagesDirectory);
            await PackAsync(packagesDirectory);

            var consumerDirectory = Path.Combine(tempRoot, "consumer");
            Directory.CreateDirectory(consumerDirectory);
            WriteConsumerProject(consumerDirectory, packagesDirectory);
            var sourcePath = Path.Combine(consumerDirectory, "Program.cs");
            var configPath = Path.Combine(consumerDirectory, ".editorconfig");

            File.WriteAllText(sourcePath, """
                using OpResult;

                _ = OpResults.Ok(1).Then(first => Step(first).Then(Step));

                static OpResult<int> Step(int value) => OpResults.Ok(value + 1);
                """);

            var warning = await BuildConsumerAsync(warningsAsErrors: false);
            Assert.True(warning.ExitCode == 0, warning.ToString());
            Assert.Contains("warning OPRESULT006", warning.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("AD0001", warning.ToString(), StringComparison.Ordinal);

            var error = await BuildConsumerAsync(warningsAsErrors: true);
            Assert.True(error.ExitCode != 0, error.ToString());
            Assert.Contains("error OPRESULT006", error.ToString(), StringComparison.Ordinal);

            File.WriteAllText(configPath, """
                root = true

                [*.cs]
                dotnet_diagnostic.OPRESULT006.severity = none
                """);

            var suppressed = await BuildConsumerAsync(warningsAsErrors: true);
            Assert.True(suppressed.ExitCode == 0, suppressed.ToString());
            Assert.DoesNotContain("OPRESULT006", suppressed.ToString(), StringComparison.Ordinal);

            // Suppressing the new rule must not disable the existing usage analyzer.
            WriteConsumerSource(consumerDirectory);
            var existingRule = await BuildConsumerAsync(warningsAsErrors: false);
            Assert.True(existingRule.ExitCode == 0, existingRule.ToString());
            Assert.Contains("warning OPRESULT002", existingRule.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("OPRESULT006", existingRule.ToString(), StringComparison.Ordinal);

            File.WriteAllText(configPath, """
                root = true

                [*.cs]
                dotnet_diagnostic.OPRESULT006.severity = warning
                """);
            File.WriteAllText(sourcePath, """
                using OpResult;

                _ = OpResults.Ok(1).Then(Step).Then(Step);

                static OpResult<int> Step(int value) => OpResults.Ok(value + 1);
                """);

            var flat = await BuildConsumerAsync(warningsAsErrors: true);
            Assert.True(flat.ExitCode == 0, flat.ToString());
            Assert.DoesNotContain("OPRESULT006", flat.ToString(), StringComparison.Ordinal);
            passed = true;

            // A relative project path keeps source and EditorConfig paths consistent when
            // the temporary directory has a symlinked ancestor (e.g. /var on macOS).
            Task<CommandResult> BuildConsumerAsync(bool warningsAsErrors) => RunDotnetInDirectoryAsync(
                consumerDirectory, "build", "Consumer.csproj", "-c", "Release", "-t:Rebuild",
                "-p:LangVersion=10.0",
                $"-p:TreatWarningsAsErrors={warningsAsErrors.ToString().ToLowerInvariant()}");
        }
        finally
        {
            CleanupIfPassed(tempRoot, passed);
        }
    }

    private static async Task PackAsync(string packagesDirectory)
    {
        AssertReleaseConfiguration(GetAssemblyConfiguration());

        var result = await RunDotnetAsync(
            "pack",
            "OpResult/OpResult.csproj",
            "-c",
            RequiredConfiguration,
            "--no-build",
            "--no-restore",
            "-o",
            packagesDirectory,
            "-p:ContinuousIntegrationBuild=true");

        Assert.True(result.ExitCode == 0, result.ToString());
    }

    private static void AssertReleaseConfiguration(string configuration)
    {
        if (string.Equals(configuration, RequiredConfiguration, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Package fixture tests validate release package outputs and must be run with `dotnet test OpResult.Package.Tests/OpResult.Package.Tests.csproj -c {RequiredConfiguration}`.");
    }

    private static string GetAssemblyConfiguration() =>
        typeof(PackageFixtureTests)
            .Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration ?? string.Empty;

    private static string GetPackagePath(string packagesDirectory)
    {
        var packagePaths = Directory
            .EnumerateFiles(packagesDirectory, $"{PackageId}.*.nupkg", SearchOption.TopDirectoryOnly)
            .ToArray();

        Assert.Single(packagePaths);
        return packagePaths[0];
    }

    private static void WriteConsumerProject(string consumerDirectory, string packagesDirectory)
    {
        var project = new XDocument(
            new XElement(
                "Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement(
                    "PropertyGroup",
                    new XElement("OutputType", "Exe"),
                    new XElement("TargetFramework", "net10.0"),
                    new XElement("ImplicitUsings", "enable"),
                    new XElement("Nullable", "enable"),
                    new XElement("RestoreSources", packagesDirectory),
                    new XElement("RestorePackagesPath", Path.Combine(consumerDirectory, "global-packages"))),
                new XElement(
                    "ItemGroup",
                    new XElement(
                        "PackageReference",
                        new XAttribute("Include", PackageId),
                        new XAttribute("Version", "*")))));

        project.Save(Path.Combine(consumerDirectory, "Consumer.csproj"));
    }

    private static void WriteConsumerSource(string consumerDirectory)
    {
        File.WriteAllText(
            Path.Combine(consumerDirectory, "Program.cs"),
            """
            using OpResult;

            var result = OpResults.Ok(1);
            _ = result.Error;
            """);
    }

    private static bool IsMisplacedRuntimeAsset(string entryName)
    {
        if (!entryName.EndsWith(".dll", StringComparison.Ordinal)
            || (!entryName.Contains("RokyZevon.OpResult.Analyzers", StringComparison.Ordinal)
                && !IsMicrosoftCodeAnalysisAssembly(entryName)))
        {
            return false;
        }

        return entryName.StartsWith("lib/", StringComparison.Ordinal)
            || entryName.StartsWith("ref/", StringComparison.Ordinal)
            || entryName.StartsWith("runtime/", StringComparison.Ordinal)
            || entryName.StartsWith("runtimes/", StringComparison.Ordinal);
    }

    private static bool IsMicrosoftCodeAnalysisAssembly(string entryName)
    {
        var fileName = Path.GetFileName(entryName);
        return fileName.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal);
    }

    private static void AssertDoesNotDeclareDependencies(ZipArchive package)
    {
        var nuspecEntry = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspecEntry.Open();
        var nuspec = XDocument.Load(stream);
        var dependencies = nuspec
            .Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .Select(element => (string?)element.Attribute("id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray();

        Assert.Empty(dependencies);
    }

    private static Task<CommandResult> RunDotnetAsync(params string[] arguments) =>
        RunDotnetInDirectoryAsync(GetRepositoryRoot(), arguments);

    private static async Task<CommandResult> RunDotnetInDirectoryAsync(string directory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "OpResult.Package.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void CleanupIfPassed(string directory, bool passed)
    {
        if (!passed)
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpResult.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public override string ToString() =>
            $"""
            Exit code: {ExitCode}

            Standard output:
            {StandardOutput}

            Standard error:
            {StandardError}
            """;
    }
}
