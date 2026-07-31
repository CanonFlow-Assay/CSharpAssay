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

        if (!evidence.Missing.IsDefaultOrEmpty)
        {
            return new InconclusiveVerdict(evidence.Missing, evidence);
        }

        return new PassVerdict(evidence);
    }
}
