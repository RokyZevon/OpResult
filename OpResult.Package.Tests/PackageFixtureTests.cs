namespace OpResult.Package.Tests;

using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

public sealed class PackageFixtureTests
{
    private const string PackageId = "RokyZevon.OpResult";
    private const string PackageVersion = "0.1.1";
    private const string AnalyzerPath = "analyzers/dotnet/cs/RokyZevon.OpResult.Analyzers.dll";

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

    private static async Task PackAsync(string packagesDirectory)
    {
        var result = await RunDotnetAsync(
            "pack",
            "OpResult/OpResult.csproj",
            "-c",
            "Release",
            "-o",
            packagesDirectory,
            $"-p:Version={PackageVersion}",
            $"-p:PackageVersion={PackageVersion}",
            "-p:ContinuousIntegrationBuild=true");

        Assert.True(result.ExitCode == 0, result.ToString());
    }

    private static string GetPackagePath(string packagesDirectory)
    {
        var packagePath = Directory
            .EnumerateFiles(packagesDirectory, $"{PackageId}.{PackageVersion}.nupkg", SearchOption.TopDirectoryOnly)
            .SingleOrDefault();

        Assert.NotNull(packagePath);
        return packagePath;
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
                        new XAttribute("Version", PackageVersion)))));

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

    private static async Task<CommandResult> RunDotnetAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = GetRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

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
