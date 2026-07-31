using CsAssay.Domain;
using CsAssay.Reporting;
using CsAssay.Workspaces;

namespace CsAssay.Integration.Tests;

public sealed class VerificationIntegrationTests
{
    [Fact]
    public async Task Check_is_provisional_and_deterministic()
    {
        var root = FindRoot();
        var project = Path.Combine(
            root,
            "src",
            "CsAssay.Domain",
            "CsAssay.Domain.csproj");
        var request = new VerificationRequest(
            project,
            PolicyPath: Presence.Missing<string>(),
            IsAuthoritative: false,
            ExecuteTests: false,
            ProfileOverride: Presence.Of(AssayProfile.Compat));

        var first = await VerificationEngine.VerifyAsync(
            request,
            TestContext.Current.CancellationToken);
        var second = await VerificationEngine.VerifyAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(first.Verdict.Evidence.IsAuthoritative);
        var configuredTest = Assert.Single(first.Verdict.Evidence.Tests);
        Assert.Equal(TestRunOutcome.NotRun, configuredTest.Outcome);
        Assert.Equal(
            JsonEvidenceWriter.Write(first.Verdict),
            JsonEvidenceWriter.Write(second.Verdict));
    }

    [Fact]
    public async Task Authoritative_self_assay_can_pass_with_admitted_rules()
    {
        var root = FindRoot();
        var solution = Path.Combine(root, "CSharpAssay.slnx");

        var result = await VerificationEngine.VerifyAsync(
            new VerificationRequest(
                solution,
                PolicyPath: Presence.Missing<string>(),
                IsAuthoritative: true,
                ExecuteTests: true,
                ProfileOverride: Presence.Of(AssayProfile.Compat)),
            TestContext.Current.CancellationToken);

        Assert.Equal(AssayVerdictKind.Pass, result.Verdict.Kind);
        Assert.DoesNotContain(
            result.Verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-NO-ADMITTED-RULES");
        Assert.DoesNotContain(
            result.Verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-WORKSPACE-WARNING");
        Assert.NotEmpty(result.Verdict.Evidence.WorkspaceDiagnostics);
        Assert.Contains(
            result.Verdict.Evidence.Projects,
            project => project.ProjectReferences.Length > 0);
        var test = Assert.Single(result.Verdict.Evidence.Tests);
        Assert.Equal(TestRunOutcome.Passed, test.Outcome);
        Assert.True(test.Total >= 18);
        Assert.All(
            result.Verdict.Evidence.WorkspaceDiagnostics,
            diagnostic => Assert.False(diagnostic.AffectsCompleteness));
        Assert.All(
            result.Verdict.Evidence.Sources,
            source =>
            {
                Assert.False(Path.IsPathRooted(source.Path));
                Assert.False(
                    source.Path.StartsWith("../", StringComparison.Ordinal));
            });
    }

    [Fact]
    public async Task Project_level_nullable_evidence_respects_shell_scope()
    {
        var root = FindRoot();
        var project = Path.Combine(
            root,
            "specimens",
            "Projects",
            "BoundaryScope",
            "BoundaryScope.csproj");

        var result = await VerificationEngine.VerifyAsync(
            new VerificationRequest(
                project,
                PolicyPath: Presence.Missing<string>(),
                IsAuthoritative: false,
                ExecuteTests: false,
                ProfileOverride: Presence.Of(AssayProfile.Compat)),
            TestContext.Current.CancellationToken);

        var nullableDisabled = Assert.Single(
            result.Verdict.Evidence.Findings,
            finding => finding.RuleId == "CSAN0001");
        Assert.Equal(RuleDisposition.Advise, nullableDisabled.Disposition);
        Assert.DoesNotContain(
            result.Verdict.Evidence.Findings,
            finding =>
                finding.RuleId == "CSAN0003" &&
                finding.Disposition == RuleDisposition.Block);
    }

    [Fact]
    public async Task Policy_domain_glossary_reaches_advisory_analysis_without_blocking()
    {
        var root = FindRoot();
        var project = Path.Combine(
            root,
            "specimens",
            "Projects",
            "GuidancePolicy",
            "GuidancePolicy.csproj");

        var result = await VerificationEngine.VerifyAsync(
            new VerificationRequest(
                project,
                PolicyPath: Presence.Missing<string>(),
                IsAuthoritative: false,
                ExecuteTests: false,
                ProfileOverride: Presence.Of(AssayProfile.Compat)),
            TestContext.Current.CancellationToken);

        Assert.Equal(AssayVerdictKind.Pass, result.Verdict.Kind);
        var finding = Assert.Single(
            result.Verdict.Evidence.Findings,
            item => item.RuleId == "CSAD0001");
        Assert.Equal(RuleCertainty.Contextual, finding.Certainty);
        Assert.Equal(RuleDisposition.Advise, finding.Disposition);
        Assert.Equal(FindingSeverity.Info, finding.Severity);
    }

    [Fact]
    public async Task Compiler_error_is_inconclusive_and_cannot_pass()
    {
        var result = await VerifyFixtureAsync(
            "CompilerError",
            Presence.Missing<string>());

        Assert.Equal(AssayVerdictKind.Inconclusive, result.Verdict.Kind);
        Assert.Equal(2, result.Verdict.ExitCode);
        Assert.Contains(
            result.Verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-COMPILER-ERRORS");
        Assert.Contains(
            result.Verdict.Evidence.Projects.SelectMany(
                project => project.CompilerDiagnostics),
            diagnostic => diagnostic.Severity == "Error");
    }

    [Fact]
    public async Task Required_target_framework_gap_is_a_tool_failure()
    {
        var root = FindRoot();
        var policy = Path.Combine(
            root,
            "tests",
            "CsAssay.Integration.Tests",
            "Policies",
            "required-net11.json");
        var result = await VerifyFixtureAsync(
            "BoundaryScope",
            Presence.Of(policy));

        Assert.Equal(AssayVerdictKind.ToolFailure, result.Verdict.Kind);
        Assert.Equal(3, result.Verdict.ExitCode);
        Assert.Contains(
            result.Verdict.Evidence.Failures,
            item => item.Code == "CSASSAY-REQUIRED-TFM-MISSING");
    }

    [Fact]
    public async Task Prototype_advisory_rule_cannot_be_promoted_by_consumer_policy()
    {
        var root = FindRoot();
        var policy = Path.Combine(
            root,
            "tests",
            "CsAssay.Integration.Tests",
            "Policies",
            "advisory-rule-required.json");
        var result = await VerifyFixtureAsync(
            "BoundaryScope",
            Presence.Of(policy));

        Assert.Equal(AssayVerdictKind.Inconclusive, result.Verdict.Kind);
        Assert.Contains(
            result.Verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-REQUIRED-RULE-NOT-ADMITTED");
    }

    [Fact]
    public async Task Admitted_blocking_finding_fails()
    {
        var result = await VerifyFixtureAsync(
            "BlockingFinding",
            Presence.Missing<string>());

        Assert.Equal(AssayVerdictKind.Fail, result.Verdict.Kind);
        Assert.Equal(1, result.Verdict.ExitCode);
        Assert.Contains(
            result.Verdict.Evidence.Findings,
            finding =>
                finding.RuleId == "CSAI0001" &&
                finding.Disposition == RuleDisposition.Block);
    }

    private static Task<VerificationResult> VerifyFixtureAsync(
        string name,
        Presence<string> policyPath)
    {
        var root = FindRoot();
        var project = Path.Combine(
            root,
            "specimens",
            "Projects",
            name,
            name + ".csproj");
        return VerificationEngine.VerifyAsync(
            new VerificationRequest(
                project,
                policyPath,
                IsAuthoritative: true,
                ExecuteTests: false,
                ProfileOverride: Presence.Of(AssayProfile.Compat)),
            TestContext.Current.CancellationToken);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CSharpAssay.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate CSharpAssay.slnx.");
    }
}
