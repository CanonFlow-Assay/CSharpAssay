using System.Collections.Immutable;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsAssay.Workspaces.Tests;

public sealed class ProfileNegotiatorTests
{
    [Theory]
    [InlineData("net8.0")]
    [InlineData("net10.0")]
    [InlineData("netstandard2.0")]
    [InlineData("net12.0")]
    [InlineData("net11.01")]
    public void Unsupported_frameworks_never_fall_back_to_compatibility(string framework)
    {
        var result = ProfileNegotiator.Negotiate(AssayProfile.Native, Unit(framework));

        Assert.Equal("native-unavailable", result.EvidenceName);
        Assert.IsType<Presence<MissingEvidence>.Present>(result.Missing);
    }

    [Theory]
    [InlineData("net11.0")]
    [InlineData("net11.0-windows")]
    public void Native_is_available_without_an_opt_in(string framework)
    {
        var result = ProfileNegotiator.Negotiate(AssayProfile.Native, Unit(framework));

        Assert.Equal(EffectiveProfile.Native, result.Profile);
        Assert.Equal("native", result.EvidenceName);
        Assert.IsType<Presence<MissingEvidence>.Absent>(result.Missing);
    }

    [Fact]
    public void Earlier_language_versions_are_incomplete()
    {
        var result = ProfileNegotiator.Negotiate(
            AssayProfile.Native, Unit("net11.0", LanguageVersion.CSharp14));

        Assert.IsType<Presence<MissingEvidence>.Present>(result.Missing);
    }

    [Theory]
    [InlineData("compat")]
    [InlineData("auto")]
    public void Removed_profiles_are_rejected_by_policy(string profile) =>
        Assert.Throws<InvalidDataException>(() =>
            PolicyLoader.Parse("{\"profile\":\"" + profile + "\"}"));

    [Theory]
    [InlineData("net10.0")]
    [InlineData("net8.0")]
    public void Policy_cannot_require_earlier_frameworks(string framework) =>
        Assert.Throws<InvalidDataException>(() => PolicyLoader.Parse(
            "{\"release\":{\"requiredTargetFrameworks\":[\"" + framework + "\"]}}"));

    private static WorkspaceCompilation Unit(
        string framework,
        LanguageVersion language = LanguageVersion.Preview)
    {
        var paths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ??
            throw new InvalidOperationException("Runtime references are unavailable.");
        var compilation = CSharpCompilation.Create(
            "NativeProfileProbe",
            [CSharpSyntaxTree.ParseText("public sealed class Sample { }", new CSharpParseOptions(language))],
            paths.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return new WorkspaceCompilation(
            "Probe", "Probe.csproj", framework, compilation,
            ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
    }
}
