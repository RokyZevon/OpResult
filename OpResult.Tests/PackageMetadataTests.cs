namespace OpResult.Tests;

using System.Xml.Linq;

public sealed class PackageMetadataTests
{
    [Fact]
    public void OpResultProject_DeclaresNuGetPackageMetadata()
    {
        var project = LoadProject();
        var properties = ReadProperties(project);

        Assert.Equal("RokyZevon.OpResult", properties["PackageId"]);
        Assert.Equal("0.1.1", properties["Version"]);
        Assert.Equal("OpResult", properties["Title"]);
        Assert.Equal("RokyZevon", properties["Authors"]);
        Assert.Equal("RokyZevon", properties["Company"]);
        Assert.Equal("OpResult", properties["Product"]);
        Assert.Equal("Copyright (c) 2026 RokyZevon", properties["Copyright"]);
        Assert.Equal(
            "A small .NET Result Pattern library for explicit Ok and Err business flows.",
            properties["Description"]);
        Assert.Equal("result;result-pattern;error-handling;dotnet;csharp", properties["PackageTags"]);
        Assert.Equal("https://github.com/RokyZevon/OpResult", properties["PackageProjectUrl"]);
        Assert.Equal("https://github.com/RokyZevon/OpResult", properties["RepositoryUrl"]);
        Assert.Equal("git", properties["RepositoryType"]);
        Assert.Equal("true", properties["PublishRepositoryUrl"]);
        Assert.Equal("README.md", properties["PackageReadmeFile"]);
        Assert.Equal("MIT", properties["PackageLicenseExpression"]);
        Assert.Equal("true", properties["IncludeSymbols"]);
        Assert.Equal("snupkg", properties["SymbolPackageFormat"]);
        Assert.False(properties.ContainsKey("LicenseUrl"));
        Assert.False(properties.ContainsKey("IconUrl"));
        Assert.False(properties.ContainsKey("PackageLicenseFile"));
        Assert.False(properties.ContainsKey("PackageLicenseUrl"));
        Assert.False(properties.ContainsKey("PackageReleaseNotes"));
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

    private static XDocument LoadProject()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "OpResult",
            "OpResult.csproj"));

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
}
