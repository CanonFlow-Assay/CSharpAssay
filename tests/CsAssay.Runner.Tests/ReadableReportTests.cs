using System.Collections.Immutable;
using System.Text.Json;
using CsAssay.Domain;
using CsAssay.Reporting;

namespace CsAssay.Runner.Tests;

public sealed class ReadableReportTests
{
    [Theory]
    [InlineData("pass", "Verification passed")]
    [InlineData("policy", "Verification failed")]
    [InlineData("tests", "Verification failed")]
    [InlineData("missing", "Verification incomplete")]
    [InlineData("tool", "Tool failed")]
    [InlineData("analysis", "Analysis only")]
    [InlineData("not-run", "Verification incomplete")]
    public void Reports_preserve_each_completed_outcome_and_authority(string scenario, string headline)
    {
        var evidence = EvidenceBundle.Empty("Requested/App.slnx", true) with
        { Tests = [new("Tests.csproj", "Release", true, TestRunOutcome.Passed, 0, 2, 2, 0, 0)] };
        AssayVerdict verdict = scenario switch
        {
            "policy" => new FailVerdict([Finding()], evidence with { Findings = [Finding()] }),
            "tests" => new FailVerdict([], evidence with { Tests = [new("Tests.csproj", "Release", true, TestRunOutcome.Failed, 1, 2, 1, 1, 0)] }),
            "missing" => new InconclusiveVerdict([Missing()], evidence with { IsAuthoritative = false, Missing = [Missing()], Findings = [] }),
            "tool" => new ToolFailureVerdict([Failure()], evidence with { IsAuthoritative = false, Failures = [Failure()] }),
            "analysis" => new PassVerdict(evidence with { VerificationMode = VerificationMode.Analysis, IsAuthoritative = false,
                Findings = [Finding() with { RuleId = "CSAA0001" }], Tests = [new("Tests.csproj", "Release", true, TestRunOutcome.NotRun, 0, 0, 0, 0, 0)] }),
            "not-run" => new InconclusiveVerdict([], evidence with { IsAuthoritative = false, Tests = [new("Tests.csproj", "Release", true, TestRunOutcome.NotRun, 0, 0, 0, 0, 0)] }),
            _ => new PassVerdict(evidence)
        };
        var before = JsonEvidenceWriter.Write(verdict);
        var html = HtmlWriter.Write(verdict); var markdown = MarkdownWriter.Write(verdict);
        Assert.Contains("<h1>CSharpAssay: " + headline + "</h1>", html);
        Assert.StartsWith("# CSharpAssay: " + headline + "\n", markdown, StringComparison.Ordinal);
        Assert.Contains("Requested/App.slnx", html); Assert.Contains("Requested/App.slnx", markdown);
        Assert.Contains("Authority flag", html); Assert.Contains("Verification mode", markdown);
        Assert.Equal(html, HtmlWriter.Write(verdict)); Assert.Equal(markdown, MarkdownWriter.Write(verdict));
        Assert.Equal(before, JsonEvidenceWriter.Write(verdict));
        if (scenario == "missing") { Assert.Contains("CSASSAY-TEST-GAP", html); Assert.DoesNotContain("Verification passed", markdown, StringComparison.Ordinal); }
        if (scenario is "analysis" or "not-run") { Assert.Contains("Not run", html); Assert.Contains("Not run", markdown); }
        if (scenario == "tests") Assert.Contains("CSASSAY-REQUIRED-TESTS-FAILED", markdown);
        if (scenario == "tool") Assert.Contains("CSASSAY-CONTROLLED-FAILURE", html);
    }

    [Fact] public void No_tests_and_unknown_policy_are_not_reported_as_executed_behavior()
    {
        var verdict = new PassVerdict(EvidenceBundle.Empty("App", true));
        Assert.Contains("None configured", MarkdownWriter.Write(verdict)); Assert.Contains("None configured", HtmlWriter.Write(verdict));
        var invalid = Failure() with { Component = "policy" };
        var failed = new ToolFailureVerdict([invalid], verdict.Evidence with { Failures = [invalid] });
        Assert.Contains("configuration was not established", MarkdownWriter.Write(failed));
        Assert.DoesNotContain("None configured", HtmlWriter.Write(failed), StringComparison.Ordinal);
    }

    [Fact] public void Effective_counts_match_console_sarif_html_and_markdown_without_reclassifying_default()
    {
        var core = Finding(); var shell = core with { Scope = "shell", Disposition = RuleDisposition.Advise, Message = "Shell setter" };
        var advice = core with { RuleId = "CSAA0001", Message = "Prototype still carries raw Block" };
        var accepted = core with { Suppressed = true, Message = "Authorized carrier exception" };
        var other = core with { Scope = "default", Disposition = RuleDisposition.Advise, Message = "Default is not shell" };
        var evidence = EvidenceBundle.Empty("App", true) with
        {
            Findings = [core, shell, advice, accepted, other],
            Suppressions = [new("CSAI0001", "policy-grant", "Reviewed boundary", true, core.Location) { Owner = "owner", Fingerprint = core.Fingerprint }]
        };
        var verdict = new FailVerdict([core], evidence); var presented = DiagnosticPresentation.Findings(verdict);
        Assert.Equal(new DiagnosticCounts(1, 1, 1, 0, 1, 1), DiagnosticPresentation.Counts(presented));
        using var console = new StringWriter(); ConsoleReporter.Write(console, verdict);
        Assert.Contains("1 errors, 2 warnings, 1 notes, 1 accepted suppression", console.ToString());
        using var sarif = JsonDocument.Parse(SarifWriter.Write(verdict)); var results = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray().ToArray();
        Assert.Single(results, r => r.GetProperty("level").GetString() == "error");
        Assert.Equal(2, results.Count(r => r.GetProperty("level").GetString() == "warning"));
        Assert.Equal(2, results.Count(r => r.GetProperty("level").GetString() == "note"));
        var html = HtmlWriter.Write(verdict); var markdown = MarkdownWriter.Write(verdict);
        foreach (var label in new[] { "Active blocking findings", "Shell warnings", "Default-scope warnings", "Advisory findings", "Accepted suppressions" })
        {
            Assert.Contains("<td>" + label + "</td><td>1</td>", html);
            Assert.Contains("| " + label + " | 1 |", markdown);
        }
        Assert.Contains("Accepted suppressed observations", html); Assert.Contains("policy-grant", markdown);
        Assert.Contains("| warning | default |", markdown); Assert.Contains("| note | core |", markdown);
        Assert.Contains("https://github.com/CanonFlow-Assay/CSharpAssay/blob/main/docs/rules/CSAI0001.md", html);
    }

    [Fact] public void Hostile_paths_messages_and_artifacts_cannot_create_markup_or_unsafe_links()
    {
        const string hostile = "evil|[x](javascript:alert(1))\n<img src=x onerror=alert(1)>\u001b\u202e";
        var finding = Finding() with { Message = hostile, Location = Finding().Location with { Path = hostile } };
        var verdict = new FailVerdict([finding], EvidenceBundle.Empty(hostile, true) with { Findings = [finding] });
        var html = HtmlWriter.Write(verdict, [new("JSON", hostile)]); var markdown = MarkdownWriter.Write(verdict, [new("JSON", hostile)]);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal); Assert.Contains("&lt;img", html);
        Assert.DoesNotContain("<img", markdown, StringComparison.Ordinal); Assert.Contains("&lt;img", markdown);
        Assert.DoesNotContain("](javascript:", markdown, StringComparison.Ordinal); Assert.Contains("\\|", markdown);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', html); Assert.DoesNotContain('\u001b', markdown); Assert.DoesNotContain('\u202e', markdown);
        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
    }

    [Theory] [InlineData("check")] [InlineData("verify")] [InlineData("assess")]
    public void Markdown_option_is_optional_and_available_only_to_existing_assay_commands(string command)
    {
        var parsed = CommandLine.Parse([command, "App.slnx", "--markdown", "summary.md"]);
        Assert.Equal(new Presence<string>.Present("summary.md"), parsed.MarkdownPath);
        Assert.Throws<ArgumentException>(() => CommandLine.Parse([command, "App.slnx", "--markdown"]));
        Assert.Throws<ArgumentException>(() => CommandLine.Parse([command, "App.slnx", "--markdown", "one", "--markdown", "two"]));
    }

    [Theory] [InlineData("doctor")] [InlineData("catalog")] [InlineData("explain")] [InlineData("help")]
    public void Non_assay_commands_reject_markdown(string command) => Assert.Throws<ArgumentException>(() => CommandLine.Parse([command, "--markdown", "x.md"]));

    [Fact] public async Task Requested_markdown_failure_preserves_prior_json_and_prints_no_success_summary()
    {
        var root = Directory.CreateTempSubdirectory("csassay-markdown-failure-").FullName;
        try
        {
            var source = FindRoot(); var prior = Path.Combine(root, "prior.json"); await File.WriteAllTextAsync(prior, "prior", TestContext.Current.CancellationToken);
            using var output = new StringWriter(); using var error = new StringWriter();
            var exit = await CommandApp.RunAsync(["check", Path.Combine(source, "specimens/Projects/CompleteBoundaryScope/CompleteBoundaryScope.csproj"),
                "--json", prior, "--markdown", root], output, error, TestContext.Current.CancellationToken);
            Assert.Equal(3, exit); Assert.Contains("artifact write failed", error.ToString());
            Assert.DoesNotContain("Analysis only:", output.ToString(), StringComparison.Ordinal); Assert.DoesNotContain("Verification passed:", output.ToString(), StringComparison.Ordinal);
            Assert.Equal("prior", await File.ReadAllTextAsync(prior, TestContext.Current.CancellationToken)); Assert.Empty(Directory.GetFiles(root, ".csassay-*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact] public async Task Actual_check_zero_with_visible_finding_writes_analysis_only_and_assess_keeps_its_envelope()
    {
        var root = Directory.CreateTempSubdirectory("csassay-readable-cli-").FullName;
        try
        {
            var input = Path.Combine(FindRoot(), "specimens/Projects/CompleteBoundaryScope/CompleteBoundaryScope.csproj");
            using var output = new StringWriter(); using var error = new StringWriter();
            var md = Path.Combine(root, "check.md"); var html = Path.Combine(root, "check.html");
            var exit = await CommandApp.RunAsync(["check", input, "--markdown", md, "--html", html], output, error, TestContext.Current.CancellationToken);
            Assert.Equal(0, exit); Assert.Contains("Analysis only:", output.ToString()); Assert.Contains("CSAI0001", output.ToString());
            Assert.Contains("# CSharpAssay: Analysis only", await File.ReadAllTextAsync(md, TestContext.Current.CancellationToken));
            Assert.Contains("CSharpAssay: Analysis only", await File.ReadAllTextAsync(html, TestContext.Current.CancellationToken));
            var json = Path.Combine(root, "assessment.json");
            var assessed = await CommandApp.RunAsync(["assess", input, "--json", json, "--markdown", Path.Combine(root, "assess.md")], output, error, TestContext.Current.CancellationToken);
            Assert.Equal(0, assessed); using var envelope = JsonDocument.Parse(await File.ReadAllBytesAsync(json, TestContext.Current.CancellationToken));
            Assert.True(envelope.RootElement.TryGetProperty("assessment", out _)); Assert.True(envelope.RootElement.TryGetProperty("receipt", out _));
        }
        finally { Directory.Delete(root, true); }
    }

    private static Finding Finding() => new("CSAI0001", "Public setter", FindingSeverity.Warning, RuleCertainty.Deterministic, RuleDisposition.Block,
        false, "Domain", "net11.0", new("Domain/Claim.cs", 3, 4, 3, 8), "existing-fingerprint-v1") { Scope = "core" };
    private static MissingEvidence Missing() => new("CSASSAY-TEST-GAP", "A required input is missing", "Domain", "net11.0");
    private static EvaluationFailure Failure() => new("CSASSAY-CONTROLLED-FAILURE", "Execution did not complete", "workspace", Presence.Missing<string>());
    private static string FindRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "CSharpAssay.slnx"))) return folder.FullName;
        throw new InvalidOperationException("Engine root not found.");
    }
}
