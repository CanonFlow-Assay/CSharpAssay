using System.Collections.Immutable;

namespace CsAssay.Domain;

public enum RuleCategory
{
    Nullability,
    Immutability,
    Union,
    Effects,
    Functions,
    DomainModeling,
    Async,
    Policy
}

public enum RuleStatus
{
    Prototype,
    Admitted,
    Retired
}

public enum RuleCertainty
{
    Deterministic,
    Contextual,
    Heuristic
}

public enum RuleDisposition
{
    Block,
    Advise,
    Inconclusive
}

public enum AssayProfile
{
    Auto,
    Compat,
    Native
}

public enum EffectiveProfile
{
    Compat,
    NativePreview
}

public enum RuleOutcome
{
    Completed,
    Incomplete,
    Skipped,
    Failed
}

public sealed record RuleRecord(
    string Id,
    string Title,
    RuleCategory Category,
    RuleStatus Status,
    RuleCertainty Certainty,
    RuleDisposition Disposition,
    ImmutableArray<EffectiveProfile> Profiles,
    string RequiredEvidence,
    string Mechanism,
    string SuppressionPolicy,
    string PositiveSpecimen,
    string NegativeSpecimen,
    string Documentation,
    Presence<string> DelegatedTo);

public sealed record RuleEvidence(
    string RuleId,
    bool Required,
    RuleOutcome Outcome,
    int FindingCount,
    Presence<string> Reason);
