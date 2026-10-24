using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using CsAssay.Domain;
using CsAssay.Reporting;
using NJsonSchema;

namespace CsAssay.Runner.Tests;

public sealed class SarifSchemaTests
{
    private static readonly Lazy<Task<JsonSchema>> Schema = new(() => JsonSchema.FromFileAsync(Path.Combine(AppContext.BaseDirectory, "Schemas/sarif-schema-2.1.0.json")));

    [Fact]
    public async Task Official_errata_schema_validates_all_verdicts_and_rejects_invalid_execution_type()
    {
        var schema = await Schema.Value;
        var empty = EvidenceBundle.Empty("App.slnx", true);
        var missing = new MissingEvidence("CSASSAY-COMPILER-ERRORS", "Cannot complete scope.", "Domain", "net11.0");
        var failure = new EvaluationFailure("CSASSAY-ANALYZER-CRASH", "Host failed.", "host", Presence.Missing<string>());
        var finding = FindingAt("Domain/a b(é).cs");
        AssayVerdict[] verdicts = [new PassVerdict(empty), new PassVerdict(empty with { IsAuthoritative = false }),
            new FailVerdict([finding], empty with { Findings = [finding] }),
            new InconclusiveVerdict([missing], empty with { Missing = [missing] }),
            new ToolFailureVerdict([failure], empty with { Failures = [failure] }),
            new PassVerdict(empty with { Findings = [finding with { Suppressed = true }], Suppressions = [new("CSAI0001", "policy-grant", "Reviewed scoped transport boundary", true, finding.Location)] }),
            new FailVerdict([], empty with { Tests = [new("Tests.csproj", "Release", true, TestRunOutcome.Failed, 1, 3, 2, 1, 0)] })];
        foreach (var verdict in verdicts)
        {
            var json = Encoding.UTF8.GetString(SarifWriter.Write(verdict));
            Assert.Empty(schema.Validate(json));
            Assert.NotEmpty(schema.Validate(json.Replace("\"executionSuccessful\": true", "\"executionSuccessful\": \"true\"", StringComparison.Ordinal)
                .Replace("\"executionSuccessful\": false", "\"executionSuccessful\": \"false\"", StringComparison.Ordinal)));
        }
    }

    [Theory]
    [InlineData("Domain/a b(é).cs", "Domain/a%20b%28%C3%A9%29.cs", true)]
    [InlineData("C:\\work space\\Domain\\Order.cs", "file:///C:/work%20space/Domain/Order.cs", false)]
    public async Task Artifact_uris_escape_paths_and_resolve_source_roots(string path, string uri, bool relative)
    {
        var finding = FindingAt(path);
        var verdict = new FailVerdict([finding], EvidenceBundle.Empty("App.slnx", true) with { Findings = [finding] });
        var bytes = SarifWriter.Write(verdict);
        Assert.Empty((await Schema.Value).Validate(Encoding.UTF8.GetString(bytes)));
        using var document = JsonDocument.Parse(bytes);
        var run = document.RootElement.GetProperty("runs")[0];
        var location = run.GetProperty("results")[0].GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation");
        Assert.Equal(uri, location.GetProperty("uri").GetString());
        Assert.Equal(relative, location.TryGetProperty("uriBaseId", out _));
        Assert.True(run.GetProperty("originalUriBaseIds").TryGetProperty("%SRCROOT%", out _));
        var result = run.GetProperty("results")[0];
        Assert.Equal(result.GetProperty("ruleId").GetString(), run.GetProperty("tool").GetProperty("driver").GetProperty("rules")[result.GetProperty("ruleIndex").GetInt32()].GetProperty("id").GetString());
    }

    [Fact]
    public void Console_and_sarif_share_effective_observations_and_determinism()
    {
        var admitted = FindingAt("Domain/Order.cs");
        var advisory = admitted with { Disposition = RuleDisposition.Advise, Scope = "shell" };
        var prototype = admitted with { RuleId = "CSAF0001", Certainty = RuleCertainty.Heuristic, Disposition = RuleDisposition.Block };
        var evidence = EvidenceBundle.Empty("App.slnx", true) with { Findings = [admitted, advisory, prototype, admitted with { Suppressed = true }], PolicyEvaluationDate = "2026-10-23" };
        var verdict = new FailVerdict([admitted], evidence);
        var presentation = DiagnosticPresentation.Findings(verdict);
        Assert.Equal(1, presentation.Count(item => item.Level == "error"));
        using var document = JsonDocument.Parse(SarifWriter.Write(verdict));
        Assert.Equal(presentation.Select(item => item.Level), document.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().Select(item => item.GetProperty("level").GetString()));
        Assert.Equal(SarifWriter.Write(verdict), SarifWriter.Write(verdict));
        Assert.Equal(JsonEvidenceWriter.Write(verdict), JsonEvidenceWriter.Write(verdict));
    }

    private static Finding FindingAt(string path) => new("CSAI0001", "Mutable setter", FindingSeverity.Warning,
        RuleCertainty.Deterministic, RuleDisposition.Block, false, "Domain", "net11.0", new(path, 18, 12, 18, 16), Fingerprints.Sha256("synthetic-schema-case"));
}
