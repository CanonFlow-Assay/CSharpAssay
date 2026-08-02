using System.Collections.Immutable;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Domain.Tests;

public sealed class VerdictTests
{
    [Fact]
    public void Tool_failure_has_highest_precedence()
    {
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Failures =
            [
                new EvaluationFailure(
                    "TEST",
                    "analyzer crashed",
                    "test",
                    Presence.Missing<string>())
            ],
            Missing =
            [
                new MissingEvidence(
                    "MISSING",
                    "evidence missing",
                    "sample",
                    "net10.0")
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<ToolFailureVerdict>(verdict);
        Assert.Equal(3, verdict.ExitCode);
    }

    [Fact]
    public void Admitted_deterministic_blocking_finding_fails()
    {
        var admitted = RuleCatalogue.All[0] with
        {
            Status = RuleStatus.Admitted
        };
        var location = new SourceSpan("File.cs", 1, 1, 1, 2);
        var finding = new Finding(
            admitted.Id,
            admitted.Title,
            FindingSeverity.Warning,
            admitted.Certainty,
            admitted.Disposition,
            Suppressed: false,
            "Sample",
            "net10.0",
            location,
            Fingerprints.Finding(
                admitted.Id,
                "Sample",
                "net10.0",
                location,
                admitted.Title));
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Findings = [finding],
            Missing =
            [
                new MissingEvidence(
                    "MISSING",
                    "lower precedence than fail",
                    "Sample",
                    "net10.0")
            ]
        };

        var verdict = VerdictFactory.Create(evidence, [admitted]);

        var failed = Assert.IsType<FailVerdict>(verdict);
        Assert.Single(failed.Blocking);
        Assert.Equal(1, failed.ExitCode);
    }

    [Fact]
    public void Prototype_finding_cannot_fail_release()
    {
        var prototype = RuleCatalogue.All.First(
            rule => rule.Status == RuleStatus.Prototype);
        var location = new SourceSpan("File.cs", 1, 1, 1, 2);
        var finding = new Finding(
            prototype.Id,
            prototype.Title,
            FindingSeverity.Warning,
            prototype.Certainty,
            prototype.Disposition,
            Suppressed: false,
            "Sample",
            "net10.0",
            location,
            Fingerprints.Finding(
                prototype.Id,
                "Sample",
                "net10.0",
                location,
                prototype.Title));
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Projects = [CompleteProject()],
            Findings = [finding]
        };

        var verdict = VerdictFactory.Create(evidence, [prototype]);

        Assert.IsType<PassVerdict>(verdict);
    }

    [Fact]
    public void Suppressed_finding_cannot_become_a_blocker()
    {
        var admitted = RuleCatalogue.All[0] with
        {
            Status = RuleStatus.Admitted
        };
        var location = new SourceSpan("File.cs", 1, 1, 1, 2);
        var finding = new Finding(
            admitted.Id,
            admitted.Title,
            FindingSeverity.Warning,
            admitted.Certainty,
            admitted.Disposition,
            Suppressed: true,
            "Sample",
            "net10.0",
            location,
            Fingerprints.Finding(
                admitted.Id,
                "Sample",
                "net10.0",
                location,
                admitted.Title));
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Projects = [CompleteProject()],
            Findings = [finding]
        };

        var verdict = VerdictFactory.Create(evidence, [admitted]);

        Assert.IsType<PassVerdict>(verdict);
    }

    [Fact]
    public void Required_test_failure_fails_release()
    {
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Tests =
            [
                new TestRunEvidence(
                    "tests/Sample.Tests/Sample.Tests.csproj",
                    "Release",
                    Required: true,
                    TestRunOutcome.Failed,
                    ExitCode: 1,
                    Total: 2,
                    Passed: 1,
                    Failed: 1,
                    Skipped: 0)
            ],
            Missing =
            [
                new MissingEvidence(
                    "LOWER-PRECEDENCE",
                    "A failed required test is decisive.",
                    "Sample.Tests",
                    "net10.0")
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<FailVerdict>(verdict);
        Assert.Equal(1, verdict.ExitCode);
    }

    [Fact]
    public void Required_skipped_rule_is_inconclusive()
    {
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Projects = [CompleteProject()],
            Rules =
            [
                new RuleEvidence(
                    "CSAN0001",
                    Required: true,
                    RuleOutcome.Skipped,
                    FindingCount: 0,
                    Presence.Of("profile mismatch"))
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<InconclusiveVerdict>(verdict);
        Assert.Equal(2, verdict.ExitCode);
        Assert.Contains(
            verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-REQUIRED-RULE-SKIPPED");
    }

    [Fact]
    public void Authoritative_required_test_not_run_is_inconclusive()
    {
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Projects = [CompleteProject()],
            Tests =
            [
                new TestRunEvidence(
                    "tests/Sample.Tests/Sample.Tests.csproj",
                    "Release",
                    Required: true,
                    TestRunOutcome.NotRun,
                    ExitCode: -1,
                    Total: 0,
                    Passed: 0,
                    Failed: 0,
                    Skipped: 0)
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<InconclusiveVerdict>(verdict);
        Assert.False(verdict.Evidence.IsAuthoritative);
        Assert.Contains(
            verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-REQUIRED-TESTS-NOT-RUN");
    }

    [Fact]
    public void Ncalc_shaped_fail_is_not_authoritative_when_evidence_is_incomplete()
    {
        var admitted = RuleCatalogue.All.Single(rule => rule.Id == "CSAN0001");
        var project = CompleteProject() with
        {
            CompilerDiagnostics =
            [
                new CompilerEvidence(
                    "CS0103",
                    "Error",
                    "The name 'BinaryOperators' does not exist in the current context.",
                    new SourceSpan("src/NCalc.Core/Expression.cs", 10, 9, 10, 24)),
                new CompilerEvidence(
                    "CS0117",
                    "Error",
                    "'TypeHelper' does not contain a definition for a generated member.",
                    new SourceSpan("src/NCalc.Core/TypeHelper.cs", 12, 9, 12, 19))
            ]
        };
        var evidence = EvidenceBundle.Empty("NCalc.slnx", true) with
        {
            Projects = [project],
            Findings =
            [
                new Finding(
                    admitted.Id,
                    admitted.Title,
                    FindingSeverity.Warning,
                    admitted.Certainty,
                    admitted.Disposition,
                    Suppressed: false,
                    "NCalc.Core",
                    "net10.0",
                    new SourceSpan("src/NCalc.Core/Expression.cs", 20, 5, 20, 15),
                    "ncalc-shaped-fingerprint")
            ],
            Rules =
            [
                new RuleEvidence(
                    "CSAN0001",
                    Required: true,
                    RuleOutcome.Completed,
                    FindingCount: 0,
                    Presence.Missing<string>())
            ],
            Tests =
            [
                new TestRunEvidence(
                    "test/NCalc.Tests/NCalc.Tests.csproj",
                    "Release",
                    Required: true,
                    TestRunOutcome.NotRun,
                    ExitCode: -1,
                    Total: 0,
                    Passed: 0,
                    Failed: 0,
                    Skipped: 0)
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<FailVerdict>(verdict);
        Assert.False(verdict.Evidence.IsAuthoritative);
        Assert.Contains(
            verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-COMPILER-ERRORS");
        Assert.Contains(
            verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-REQUIRED-TESTS-NOT-RUN");
        var rule = Assert.Single(verdict.Evidence.Rules);
        Assert.Equal(RuleOutcome.Incomplete, rule.Outcome);
        Assert.IsType<Presence<string>.Present>(rule.Reason);
    }

    [Fact]
    public void Completed_required_test_failure_remains_authoritative_evidence()
    {
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Projects = [CompleteProject()],
            Tests =
            [
                new TestRunEvidence(
                    "tests/Sample.Tests/Sample.Tests.csproj",
                    "Release",
                    Required: true,
                    TestRunOutcome.Failed,
                    ExitCode: 1,
                    Total: 2,
                    Passed: 1,
                    Failed: 1,
                    Skipped: 0)
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<FailVerdict>(verdict);
        Assert.True(verdict.Evidence.IsAuthoritative);
    }

    [Fact]
    public void Provisional_rules_are_incomplete_when_project_evidence_is_unavailable()
    {
        var project = CompleteProject() with
        {
            CompilerDiagnostics =
            [
                new CompilerEvidence(
                    "CS0103",
                    "Error",
                    "A generated member is unavailable.",
                    new SourceSpan("src/Sample/GeneratedConsumer.cs", 4, 9, 4, 18))
            ]
        };
        var evidence = EvidenceBundle.Empty("sample.csproj", false) with
        {
            Projects = [project],
            Rules =
            [
                new RuleEvidence(
                    "CSAN0001",
                    Required: false,
                    RuleOutcome.Completed,
                    FindingCount: 0,
                    Presence.Missing<string>())
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.False(verdict.Evidence.IsAuthoritative);
        Assert.Equal(
            RuleOutcome.Incomplete,
            Assert.Single(verdict.Evidence.Rules).Outcome);
    }

    [Fact]
    public void Zero_loaded_projects_prevents_authority_and_completion()
    {
        var evidence = EvidenceBundle.Empty("NCalc.slnx", true) with
        {
            Rules =
            [
                new RuleEvidence(
                    "CSAN0001",
                    Required: true,
                    RuleOutcome.Completed,
                    FindingCount: 0,
                    Presence.Missing<string>())
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<InconclusiveVerdict>(verdict);
        Assert.False(verdict.Evidence.IsAuthoritative);
        Assert.Contains(
            verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-NO-PROJECTS-LOADED");
        Assert.Equal(
            RuleOutcome.Incomplete,
            Assert.Single(verdict.Evidence.Rules).Outcome);
    }

    [Fact]
    public void Any_missing_evidence_prevents_authority()
    {
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Projects = [CompleteProject()],
            Missing =
            [
                new MissingEvidence(
                    "CSASSAY-EVIDENCE-GAP",
                    "Required evidence is unavailable.",
                    "Sample",
                    "net10.0")
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<InconclusiveVerdict>(verdict);
        Assert.False(verdict.Evidence.IsAuthoritative);
    }

    [Fact]
    public void Completeness_affecting_workspace_diagnostic_prevents_authority()
    {
        var evidence = EvidenceBundle.Empty("sample.csproj", true) with
        {
            Projects = [CompleteProject()],
            Rules =
            [
                new RuleEvidence(
                    "CSAN0001",
                    Required: false,
                    RuleOutcome.Completed,
                    FindingCount: 0,
                    Presence.Missing<string>())
            ],
            WorkspaceDiagnostics =
            [
                new WorkspaceDiagnosticEvidence(
                    "Warning",
                    "Generated document could not be loaded.",
                    "Sample",
                    "net10.0",
                    AffectsCompleteness: true)
            ]
        };

        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);

        Assert.IsType<InconclusiveVerdict>(verdict);
        Assert.False(verdict.Evidence.IsAuthoritative);
        Assert.Equal(
            RuleOutcome.Incomplete,
            Assert.Single(verdict.Evidence.Rules).Outcome);
        Assert.Contains(
            verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-WORKSPACE-INCOMPLETE");
    }

    [Fact]
    public void Fingerprints_ignore_platform_path_separator()
    {
        var slash = Fingerprints.Finding(
            "CSAN0001",
            "Sample",
            "net10.0",
            new SourceSpan("src/File.cs", 2, 3, 2, 4),
            "message");
        var backslash = Fingerprints.Finding(
            "CSAN0001",
            "Sample",
            "net10.0",
            new SourceSpan(@"src\File.cs", 2, 3, 2, 4),
            "message");

        Assert.Equal(slash, backslash);
    }

    private static ProjectEvidence CompleteProject() => new(
        "Sample",
        "src/Sample/Sample.csproj",
        "net10.0",
        EffectiveProfile.Compat,
        "qualified",
        "14.0",
        "Enable",
        Loaded: true,
        ImmutableArray<string>.Empty,
        ImmutableArray<CompilerEvidence>.Empty);
}
