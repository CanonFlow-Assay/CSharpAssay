using System.Collections.Immutable;

namespace CsAssay.Domain;

public enum AssayVerdictKind
{
    Pass,
    Inconclusive,
    Fail,
    ToolFailure
}

public abstract record AssayVerdict(EvidenceBundle Evidence)
{
    public abstract AssayVerdictKind Kind { get; }

    public abstract int ExitCode { get; }
}

public sealed record PassVerdict(EvidenceBundle Evidence) : AssayVerdict(Evidence)
{
    public override AssayVerdictKind Kind => AssayVerdictKind.Pass;

    public override int ExitCode => 0;
}

public sealed record FailVerdict(
    ImmutableArray<Finding> Blocking,
    EvidenceBundle Evidence) : AssayVerdict(Evidence)
{
    public override AssayVerdictKind Kind => AssayVerdictKind.Fail;

    public override int ExitCode => 1;
}

public sealed record InconclusiveVerdict(
    ImmutableArray<MissingEvidence> Missing,
    EvidenceBundle Evidence) : AssayVerdict(Evidence)
{
    public override AssayVerdictKind Kind => AssayVerdictKind.Inconclusive;

    public override int ExitCode => 2;
}

public sealed record ToolFailureVerdict(
    ImmutableArray<EvaluationFailure> Failures,
    EvidenceBundle Evidence) : AssayVerdict(Evidence)
{
    public override AssayVerdictKind Kind => AssayVerdictKind.ToolFailure;

    public override int ExitCode => 3;
}

public static class VerdictFactory
{
    public static AssayVerdict Create(
        EvidenceBundle evidence,
        ImmutableArray<RuleRecord> catalogue)
    {
        evidence = NormalizeAuthority(evidence);

        if (!evidence.Failures.IsDefaultOrEmpty)
        {
            return new ToolFailureVerdict(evidence.Failures, evidence);
        }

        var rules = catalogue.ToImmutableDictionary(rule => rule.Id, StringComparer.Ordinal);
        var blocking = evidence.Findings
            .Where(finding =>
                !finding.Suppressed &&
                finding.Certainty == RuleCertainty.Deterministic &&
                finding.Disposition == RuleDisposition.Block &&
                rules.TryGetValue(finding.RuleId, out var rule) &&
                rule.Status == RuleStatus.Admitted &&
                rule.Certainty == RuleCertainty.Deterministic &&
                rule.Disposition == RuleDisposition.Block)
            .OrderBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Location.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.Location.StartLine)
            .ThenBy(finding => finding.Location.StartColumn)
            .ToImmutableArray();

        if (!blocking.IsDefaultOrEmpty)
        {
            return new FailVerdict(blocking, evidence);
        }

        if (evidence.Tests.Any(test =>
                test.Required &&
                test.Outcome == TestRunOutcome.Failed))
        {
            return new FailVerdict(blocking, evidence);
        }

        var missing = evidence.Missing.ToBuilder();
        if (evidence.IsAuthoritative &&
            evidence.Tests.Any(test =>
                test.Required &&
                test.Outcome == TestRunOutcome.NotRun) &&
            !missing.Any(item =>
                string.Equals(
                    item.Code,
                    "CSASSAY-REQUIRED-TESTS-NOT-RUN",
                    StringComparison.Ordinal)))
        {
            missing.Add(new MissingEvidence(
                "CSASSAY-REQUIRED-TESTS-NOT-RUN",
                "Authoritative verification did not execute required tests.",
                string.Empty,
                string.Empty));
        }

        if (evidence.Rules.Any(rule =>
                rule.Required &&
                rule.Outcome == RuleOutcome.Skipped) &&
            !missing.Any(item =>
                string.Equals(
                    item.Code,
                    "CSASSAY-REQUIRED-RULE-SKIPPED",
                    StringComparison.Ordinal)))
        {
            missing.Add(new MissingEvidence(
                "CSASSAY-REQUIRED-RULE-SKIPPED",
                "At least one required rule was skipped.",
                string.Empty,
                string.Empty));
        }

        if (evidence.Rules.Any(rule =>
                rule.Required &&
                rule.Outcome == RuleOutcome.Incomplete) &&
            !missing.Any(item =>
                string.Equals(
                    item.Code,
                    "CSASSAY-REQUIRED-RULE-INCOMPLETE",
                    StringComparison.Ordinal)))
        {
            missing.Add(new MissingEvidence(
                "CSASSAY-REQUIRED-RULE-INCOMPLETE",
                "At least one required rule has incomplete project evidence.",
                string.Empty,
                string.Empty));
        }

        if (missing.Count > 0)
        {
            var orderedMissing = missing
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.Project, StringComparer.Ordinal)
                .ThenBy(item => item.TargetFramework, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToImmutableArray();
            var normalizedEvidence = evidence with
            {
                Missing = orderedMissing
            };
            return new InconclusiveVerdict(
                orderedMissing,
                normalizedEvidence);
        }

        return new PassVerdict(evidence);
    }

    private static EvidenceBundle NormalizeAuthority(EvidenceBundle evidence)
    {
        var authorityRequested = evidence.IsAuthoritative;
        var missing = evidence.Missing.ToBuilder();
        var loadedProjects = evidence.Projects
            .Where(project => project.Loaded)
            .ToImmutableArray();
        if (loadedProjects.IsDefaultOrEmpty)
        {
            AddMissingOnce(
                missing,
                new MissingEvidence(
                    "CSASSAY-NO-PROJECTS-LOADED",
                    "Verification loaded no projects; project evidence is unavailable.",
                    string.Empty,
                    string.Empty));
        }

        foreach (var project in evidence.Projects.Where(project => !project.Loaded))
        {
            AddMissingOnce(
                missing,
                new MissingEvidence(
                    "CSASSAY-PROJECT-NOT-LOADED",
                    "Project evidence was not loaded.",
                    project.Path,
                    project.TargetFramework));
        }

        foreach (var project in loadedProjects.Where(HasCompilerErrors))
        {
            AddMissingOnce(
                missing,
                new MissingEvidence(
                    "CSASSAY-COMPILER-ERRORS",
                    "Compiler errors prevent complete semantic evidence.",
                    project.Name,
                    project.TargetFramework));
        }

        foreach (var diagnostic in evidence.WorkspaceDiagnostics.Where(
                     diagnostic => diagnostic.AffectsCompleteness))
        {
            AddMissingOnce(
                missing,
                new MissingEvidence(
                    "CSASSAY-WORKSPACE-INCOMPLETE",
                    diagnostic.Message,
                    diagnostic.Project,
                    diagnostic.TargetFramework));
        }

        if (authorityRequested &&
            evidence.Tests.Any(test =>
                test.Required &&
                test.Outcome == TestRunOutcome.NotRun))
        {
            AddMissingOnce(
                missing,
                new MissingEvidence(
                    "CSASSAY-REQUIRED-TESTS-NOT-RUN",
                    "Authoritative verification did not execute required tests.",
                    string.Empty,
                    string.Empty));
        }

        var projectEvidenceUnavailable =
            loadedProjects.IsDefaultOrEmpty ||
            evidence.Projects.Any(project => !project.Loaded) ||
            loadedProjects.Any(HasCompilerErrors) ||
            evidence.WorkspaceDiagnostics.Any(diagnostic =>
                diagnostic.AffectsCompleteness) ||
            evidence.Failures.Any(failure => failure.Code.StartsWith(
                "CSASSAY-WORKSPACE-",
                StringComparison.Ordinal));
        var rules = projectEvidenceUnavailable
            ? evidence.Rules
                .Select(rule => rule.Outcome == RuleOutcome.Completed
                    ? rule with
                    {
                        Outcome = RuleOutcome.Incomplete,
                        Reason = Presence.Of(
                            "Project evidence was unavailable; rule execution " +
                            "cannot be considered complete.")
                    }
                    : rule)
                .ToImmutableArray()
            : evidence.Rules;
        var orderedMissing = missing
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Project, StringComparer.Ordinal)
            .ThenBy(item => item.TargetFramework, StringComparer.Ordinal)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .ToImmutableArray();
        var authorityComplete =
            authorityRequested &&
            !projectEvidenceUnavailable &&
            orderedMissing.IsDefaultOrEmpty &&
            evidence.Failures.IsDefaultOrEmpty &&
            !evidence.Tests.Any(test =>
                test.Required &&
                test.Outcome == TestRunOutcome.NotRun) &&
            !rules.Any(rule =>
                rule.Required &&
                rule.Outcome != RuleOutcome.Completed);

        return evidence with
        {
            IsAuthoritative = authorityComplete,
            Rules = rules,
            Missing = orderedMissing
        };
    }

    private static bool HasCompilerErrors(ProjectEvidence project) =>
        project.CompilerDiagnostics.Any(diagnostic => string.Equals(
            diagnostic.Severity,
            "Error",
            StringComparison.OrdinalIgnoreCase));

    private static void AddMissingOnce(
        ImmutableArray<MissingEvidence>.Builder missing,
        MissingEvidence item)
    {
        if (!missing.Any(existing =>
                string.Equals(existing.Code, item.Code, StringComparison.Ordinal) &&
                string.Equals(
                    existing.Project,
                    item.Project,
                    StringComparison.Ordinal) &&
                string.Equals(
                    existing.TargetFramework,
                    item.TargetFramework,
                    StringComparison.Ordinal)))
        {
            missing.Add(item);
        }
    }
}
