namespace OpResult.Tests;

using System.Xml.Linq;

public sealed class PackageMetadataTests
{
    [Fact]
    public void OpResultProject_DeclaresNuGetPackageMetadata()
    {
        var project = LoadProject();
        var properties = ReadProperties(project);

        Assert.Equal("net10.0;net8.0;net6.0", properties["TargetFrameworks"]);
        Assert.Equal("RokyZevon.OpResult", properties["PackageId"]);
        Assert.Equal("OpResult", properties["Title"]);
        Assert.Equal("RokyZevon", properties["Authors"]);
        Assert.Equal("RokyZevon", properties["Company"]);
        Assert.Equal("OpResult", properties["Product"]);
        Assert.Equal("Copyright (c) 2026 RokyZevon", properties["Copyright"]);
        Assert.Equal(
            "A small Native AOT-compatible .NET Result Pattern library for explicit Ok and Err business flows.",
            properties["Description"]);
        Assert.Equal("result;result-pattern;error-handling;dotnet;csharp;native-aot;aot;trimming", properties["PackageTags"]);
        Assert.Equal("https://github.com/RokyZevon/OpResult", properties["PackageProjectUrl"]);
        Assert.Equal("https://github.com/RokyZevon/OpResult", properties["RepositoryUrl"]);
        Assert.Equal("git", properties["RepositoryType"]);
        Assert.Equal("true", properties["PublishRepositoryUrl"]);
        Assert.Equal("README.md", properties["PackageReadmeFile"]);
        Assert.Equal("MIT", properties["PackageLicenseExpression"]);
        Assert.Equal("true", properties["IncludeSymbols"]);
        Assert.Equal("snupkg", properties["SymbolPackageFormat"]);
        Assert.Equal("true", properties["IsAotCompatible"]);
        Assert.Equal("true", properties["VerifyReferenceAotCompatibility"]);
        Assert.Equal(
            "$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))",
            ReadPropertyConditions(project)["IsAotCompatible"]);
        Assert.Equal(
            "$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))",
            ReadPropertyConditions(project)["VerifyReferenceAotCompatibility"]);
        Assert.False(properties.ContainsKey("LicenseUrl"));
        Assert.False(properties.ContainsKey("IconUrl"));
        Assert.False(properties.ContainsKey("PackageLicenseFile"));
        Assert.False(properties.ContainsKey("PackageLicenseUrl"));
        Assert.False(properties.ContainsKey("PackageReleaseNotes"));
        Assert.False(properties.ContainsKey("Version"));
        Assert.False(properties.ContainsKey("PackageVersion"));
    }

    [Fact]
    public void OpResultProject_PacksRootReadmeAtPackageRoot()
    {
        var readmeItem = LoadProject()
            .Descendants("None")
            .Single(element => (string?)element.Attribute("Include") == "../README.md");

        Assert.Equal("true", (string?)readmeItem.Attribute("Pack"));
        Assert.Equal(string.Empty, (string?)readmeItem.Attribute("PackagePath"));
    }

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

    [Fact]
    public void OpResultProject_PacksAnalyzerAtNuGetAnalyzerPath()
    {
        var analyzerItem = LoadProject()
            .Descendants("None")
            .Single(element => ((string?)element.Attribute("Include"))?.Contains(
                "RokyZevon.OpResult.Analyzers.dll",
                StringComparison.Ordinal) == true);

        Assert.Equal("true", (string?)analyzerItem.Attribute("Pack"));
        Assert.Equal("analyzers/dotnet/cs", (string?)analyzerItem.Attribute("PackagePath"));
        Assert.Equal("false", (string?)analyzerItem.Attribute("Visible"));
    }

    [Fact]
    public void AnalyzerProject_UsesNet6CompatibleRoslynDependency()
    {
        var packageReference = LoadProject("OpResult.Analyzers", "OpResult.Analyzers.csproj")
            .Descendants("PackageReference")
            .Single(element => (string?)element.Attribute("Include") == "Microsoft.CodeAnalysis.CSharp");

        Assert.Equal("4.3.1", (string?)packageReference.Attribute("Version"));
        Assert.Equal("all", (string?)packageReference.Attribute("PrivateAssets"));
    }

    private static XDocument LoadProject()
    {
        return LoadProject("OpResult", "OpResult.csproj");
    }

    private static XDocument LoadProject(string projectDirectory, string projectFileName)
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            projectDirectory,
            projectFileName));

        return XDocument.Load(projectPath);
    }

    private static Dictionary<string, string> ReadProperties(XDocument project) =>
        project
            .Descendants("PropertyGroup")
            .Elements()
            .Where(element => !element.HasElements)
            .ToDictionary(
                element => element.Name.LocalName,
                element => element.Value,
                StringComparer.Ordinal);

    private static Dictionary<string, string> ReadPropertyConditions(XDocument project) =>
        project
            .Descendants("PropertyGroup")
            .Elements()
            .Where(element => !element.HasElements)
            .ToDictionary(
                element => element.Name.LocalName,
                element => (string?)element.Attribute("Condition") ?? string.Empty,
                StringComparer.Ordinal);
}
