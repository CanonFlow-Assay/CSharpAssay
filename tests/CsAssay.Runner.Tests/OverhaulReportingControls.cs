using System.Collections.Immutable;
using System.Text.Json;
using CsAssay.Domain;
using CsAssay.Reporting;

namespace CsAssay.Runner.Tests;

public sealed class OverhaulReportingControls
{
    [Fact]
    public void Default_console_emits_msbuild_locations_and_all_active_findings()
    {
        var findings = Enumerable.Range(1, 63).Select(index => FindingAt(index)).ToImmutableArray();
        var evidence = EvidenceBundle.Empty("App.slnx", true) with { Findings = findings };
        using var output = new StringWriter();
        ConsoleReporter.Write(output, new FailVerdict(findings, evidence));
        Assert.Contains("Domain/Order63.cs(18,12): error CSAI0001:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("additional findings", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Provisional_pass_never_claims_complete_verification()
    {
        using var output = new StringWriter();
        ConsoleReporter.Write(output, new PassVerdict(EvidenceBundle.Empty("App.slnx", false)));
        Assert.Contains("Analysis only:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Verification passed:", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Lost_release_authority_does_not_turn_requested_verification_into_provisional_check()
    {
        var finding = FindingAt(1);
        var evidence = EvidenceBundle.Empty("App.slnx", true) with { IsAuthoritative = false, Findings = [finding] };
        using var output = new StringWriter();
        ConsoleReporter.Write(output, new FailVerdict([finding], evidence));
        Assert.Contains("Verification failed:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Analysis only:", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Accepted_suppression_is_counted_without_an_active_error()
    {
        var finding = FindingAt(1) with { Suppressed = true };
        var evidence = EvidenceBundle.Empty("App.slnx", true) with
        {
            Findings = [finding],
            Suppressions = [new("CSAI0001", "policy-grant", "Reviewed immutable projection boundary", true, finding.Location)]
        };
        using var output = new StringWriter();
        ConsoleReporter.Write(output, new PassVerdict(evidence));
        Assert.Contains("1 accepted suppression", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(": error CSAI0001:", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Console_messages_cannot_inject_a_second_diagnostic()
    {
        var finding = FindingAt(1) with { Message = "mutable setter\nInjected.cs(1,1): error FAKE0001: forged\u001b[31m" };
        using var output = new StringWriter();
        ConsoleReporter.Write(output, new FailVerdict([finding], EvidenceBundle.Empty("App.slnx", true) with { Findings = [finding] }));
        Assert.DoesNotContain("\nInjected.cs", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', output.ToString());
    }

    [Fact]
    public void Incomplete_sarif_has_unsuccessful_invocation_even_without_findings()
    {
        var missing = new MissingEvidence("CSASSAY-COMPILER-ERRORS", "Required project has compiler errors.", "Domain", "net11.0");
        var verdict = new InconclusiveVerdict([missing], EvidenceBundle.Empty("App.slnx", true) with { Missing = [missing] });
        using var document = JsonDocument.Parse(SarifWriter.Write(verdict));
        var invocation = document.RootElement.GetProperty("runs")[0].GetProperty("invocations")[0];
        Assert.False(invocation.GetProperty("executionSuccessful").GetBoolean());
        Assert.Equal(2, invocation.GetProperty("exitCode").GetInt32());
        Assert.True(invocation.GetProperty("toolConfigurationNotifications").GetArrayLength() > 0);
    }

    [Fact]
    public void Completed_policy_failure_is_successful_sarif_execution_with_exit_one()
    {
        var finding = FindingAt(1);
        var verdict = new FailVerdict([finding], EvidenceBundle.Empty("App.slnx", true) with { Findings = [finding] });
        using var document = JsonDocument.Parse(SarifWriter.Write(verdict));
        var run = document.RootElement.GetProperty("runs")[0];
        Assert.True(run.GetProperty("invocations")[0].GetProperty("executionSuccessful").GetBoolean());
        Assert.Equal(1, run.GetProperty("invocations")[0].GetProperty("exitCode").GetInt32());
        Assert.Equal("error", run.GetProperty("results")[0].GetProperty("level").GetString());
    }

    [Fact]
    public void Policy_suppression_is_external_and_justified_in_sarif()
    {
        var finding = FindingAt(1) with { Suppressed = true };
        var evidence = EvidenceBundle.Empty("App.slnx", true) with
        {
            Findings = [finding],
            Suppressions = [new("CSAI0001", "policy-grant", "Reviewed immutable projection boundary", true, finding.Location)]
        };
        using var document = JsonDocument.Parse(SarifWriter.Write(new PassVerdict(evidence)));
        var suppression = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("suppressions")[0];
        Assert.Equal("external", suppression.GetProperty("kind").GetString());
        Assert.Equal("accepted", suppression.GetProperty("status").GetString());
        Assert.Equal("Reviewed immutable projection boundary", suppression.GetProperty("justification").GetString());
    }

    [Fact]
    public void Explicit_compatibility_format_and_suppression_review_flags_parse()
    {
        var command = CommandLine.Parse(["check", "App.slnx", "--format", "text", "--show-suppressed"]);
        Assert.Equal("check", command.Command);
    }

    private static Finding FindingAt(int index) => new(
        "CSAI0001", "Public record property exposes a mutable setter.", FindingSeverity.Warning,
        RuleCertainty.Deterministic, RuleDisposition.Block, false, "Domain", "net11.0",
        new SourceSpan("Domain/Order" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".cs", 18, 12, 18, 16),
        Fingerprints.Sha256("overhaul-reporting-control-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
}
