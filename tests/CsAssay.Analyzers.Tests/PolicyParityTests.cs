using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CsAssay.Analyzers.Tests;

public sealed class PolicyParityTests
{
    private const string Source = "namespace App.Domain; public record Order { public int Count { get; set; } }";
    private const string PathRoot = "/policy-parity";

    [Theory]
    [InlineData("coreProjects", DiagnosticSeverity.Error, "core")]
    [InlineData("shellProjects", DiagnosticSeverity.Warning, "shell")]
    public async Task Additional_file_and_verifier_host_inputs_use_the_same_policy(string boundary, DiagnosticSeverity severity, string scope)
    {
        var json = "{\"boundaries\":{\"" + boundary + "\":[\"Domain.csproj\"]}}";
        var fromFile = await Analyze(json, Source);
        var policy = PolicyParser.Parse(json, CsAssay.Catalogue.RuleCatalogue.All.Select(rule => rule.Id).ToImmutableArray());
        var supplied = new AnalysisPolicyContext(PolicyEvaluation.Normalize(policy, CsAssay.Catalogue.RuleCatalogue.All.Select(rule => rule.Id).ToImmutableArray(), false),
            PathRoot, PathRoot + "/Domain.csproj", "Domain", "net11.0", DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime), true, "test", Fingerprints.Sha256(json));
        var verifierHost = await AnalyzerTestHost.AnalyzeAsync([new(PathRoot + "/Order.cs", Source)], new Dictionary<string, string>(),
            analyzer: new FunctionalPolicyAnalyzer(supplied));
        var finding = Assert.Single(fromFile.Where(diagnostic => diagnostic.Id == "CSAI0001"));
        var same = Assert.Single(verifierHost.Where(diagnostic => diagnostic.Id == "CSAI0001"));
        Assert.Equal(severity, finding.Severity);
        Assert.Equal(scope, finding.Properties["csassay_scope"]);
        Assert.Equal(finding.GetMessage(System.Globalization.CultureInfo.InvariantCulture), same.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(finding.Properties["csassay_fingerprint"], same.Properties["csassay_fingerprint"]);
        Assert.Equal(finding.Severity, same.Severity);
    }

    [Fact]
    public async Task Only_a_real_matching_unexpired_grant_authorizes_source_suppression()
    {
        var source = "#pragma warning disable CSAI0001\n" + Source + "\n#pragma warning restore CSAI0001";
        var baseline = await Analyze("{}", source);
        Assert.Contains(baseline, finding => finding.Id == "CSAP0001" && finding.Severity == DiagnosticSeverity.Error);
        var fingerprint = Assert.Single(baseline.Where(finding => finding.Id == "CSAI0001")).Properties["csassay_fingerprint"]!;
        string Grant(string hash, string expires) => JsonSerializer.Serialize(new { suppressions = new[] { new { ruleId = "CSAI0001", owner = "fixture-owner", reason = "Reviewed transport conversion", expires, fingerprint = hash } } });
        var accepted = await Analyze(Grant(fingerprint, "2099-01-01"), source);
        Assert.DoesNotContain(accepted, finding => finding.Id == "CSAP0001");
        Assert.Equal("true", Assert.Single(accepted.Where(finding => finding.Id == "CSAI0001")).Properties["csassay_accepted"]);
        foreach (var invalid in new[] { Grant(fingerprint, "2020-01-01"), Grant(Fingerprints.Sha256("mismatched finding"), "2099-01-01") })
        {
            Assert.Contains(await Analyze(invalid, source), finding => finding.Id == "CSAP0001" && finding.Severity == DiagnosticSeverity.Error);
        }
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"boundaries\":{\"coreProjects\":[\"Domain.csproj\"],\"shellProjects\":[\"Domain.csproj\"]}}")]
    public async Task Invalid_declared_policy_cannot_select_a_permissive_fallback(string json)
    {
        Assert.Contains(await Analyze(json, Source), finding => finding.Id == "CSAP0001" && finding.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("silent")]
    [InlineData("warning")]
    public async Task Editorconfig_demotion_is_an_integrity_error(string severity)
    {
        var options = Options();
        options["dotnet_diagnostic.CSAI0001.severity"] = severity;
        Assert.Contains(await Analyze("{}", Source, options), finding => finding.Id == "CSAP0001" && finding.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Shell_boundary_cannot_restore_missing_nullable_flow_evidence()
    {
        var diagnostics = await Analyze("{\"boundaries\":{\"shellProjects\":[\"Domain.csproj\"]}}", "#nullable disable\n" + Source);
        Assert.Contains(diagnostics, finding => finding.Id == "CSAP0001" && finding.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("App.Domain", DiagnosticSeverity.Error, "core")]
    [InlineData("App.Domain.Persistence", DiagnosticSeverity.Warning, "shell")]
    public async Task Callback_namespace_identity_preserves_nested_shell_exceptions(string name, DiagnosticSeverity severity, string scope)
    {
        var policy = "{\"boundaries\":{\"coreProjects\":[\"Domain.csproj\"],\"coreNamespaces\":[\"App.Domain\"],\"shellNamespaces\":[\"App.Domain.Persistence\"]}}";
        var source = "namespace " + name + "; public record Order { public int Count { get; set; } }";
        var finding = Assert.Single((await Analyze(policy, source)).Where(item => item.Id == "CSAI0001"));
        Assert.Equal(severity, finding.Severity);
        Assert.Equal(scope, finding.Properties["csassay_scope"]);
    }

    [Fact]
    public async Task MSBuild_boolean_casing_cannot_disable_core_enforcement()
    {
        var options = Options();
        options["build_property.CsAssayEnforceOnBuild"] = "True";
        Assert.Contains(await Analyze("{}", Source, options), finding => finding.Id == "CSAI0001" && finding.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Malformed_MSBuild_boolean_is_not_an_implicit_rollback()
    {
        var options = Options();
        options["build_property.CsAssayEnforceOnBuild"] = "yes";
        Assert.Contains(await Analyze("{}", Source, options), finding => finding.Id == "CSAP0001" && finding.Severity == DiagnosticSeverity.Error);
    }

    private static Task<ImmutableArray<Diagnostic>> Analyze(string json, string source, Dictionary<string, string>? options = null) =>
        AnalyzerTestHost.AnalyzeAsync([new(PathRoot + "/Order.cs", source)], options ?? Options(),
            additionalFiles: [new PolicyText(PathRoot + "/.csassay.json", json)]);

    private static Dictionary<string, string> Options() => new(StringComparer.Ordinal)
    {
        ["build_property.CsAssayPolicyPath"] = PathRoot + "/.csassay.json",
        ["build_property.MSBuildProjectFullPath"] = PathRoot + "/Domain.csproj",
        ["build_property.MSBuildProjectName"] = "Domain",
        ["build_property.TargetFramework"] = "net11.0",
        ["build_property.CsAssayEnforceOnBuild"] = "true"
    };

    private sealed class PolicyText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text, Encoding.UTF8);
    }
}
