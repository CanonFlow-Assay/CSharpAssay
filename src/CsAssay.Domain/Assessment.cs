using System.Collections.Immutable;

namespace CsAssay.Domain;

// Additive protocol: the original verification receipt remains the authority.
public sealed record AssessmentSnapshot(
    string Schema,
    string Module,
    string ModuleVersion,
    AssayVerdictKind Verdict,
    bool Authoritative,
    string Scope,
    ImmutableArray<SourceEvidence> Sources,
    ImmutableArray<MissingEvidence> Missing,
    ImmutableArray<EvaluationFailure> Failures)
{
    public static AssessmentSnapshot FromVerdict(AssayVerdict verdict) => new(
        "csassay-assessment/0.1",
        "CSharpAssay",
        verdict.Evidence.ToolVersion,
        verdict.Kind,
        verdict.Evidence.IsAuthoritative,
        verdict.Evidence.Input,
        verdict.Evidence.Sources,
        verdict.Evidence.Missing,
        verdict.Evidence.Failures);
}
