using System.Collections.Immutable;

namespace CsAssay.Domain;

public enum FindingSeverity
{
    Hidden,
    Info,
    Warning,
    Error
}

public sealed record SourceSpan(
    string Path,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn)
{
    public static SourceSpan None { get; } = new(
        string.Empty,
        0,
        0,
        0,
        0);
}

public sealed record Finding(
    string RuleId,
    string Message,
    FindingSeverity Severity,
    RuleCertainty Certainty,
    RuleDisposition Disposition,
    bool Suppressed,
    string Project,
    string TargetFramework,
    SourceSpan Location,
    string Fingerprint);

public sealed record MissingEvidence(
    string Code,
    string Message,
    string Project,
    string TargetFramework);

public sealed record EvaluationFailure(
    string Code,
    string Message,
    string Component,
    Presence<string> RuleId);

public sealed record CompilerEvidence(
    string Id,
    string Severity,
    string Message,
    SourceSpan Location);

public sealed record ProjectEvidence(
    string Name,
    string Path,
    string TargetFramework,
    EffectiveProfile Profile,
    string ProfileEvidence,
    string LanguageVersion,
    string NullableContext,
    bool Loaded,
    ImmutableArray<string> ProjectReferences,
    ImmutableArray<CompilerEvidence> CompilerDiagnostics);

public sealed record SuppressionEvidence(
    string RuleId,
    string Form,
    string Justification,
    bool Authorized,
    SourceSpan Location);

public sealed record GeneratedCodeEvidence(
    string Path,
    string Reason,
    bool Excluded);

public sealed record WorkspaceDiagnosticEvidence(
    string Kind,
    string Message,
    string Project,
    string TargetFramework,
    bool AffectsCompleteness);

public sealed record SourceEvidence(
    string Path,
    string Sha256);

public sealed record AnalyzerEvidence(
    string Identity,
    string AssemblyVersion,
    string Sha256);

public enum TestRunOutcome
{
    Passed,
    Failed,
    NotRun
}

public sealed record TestRunEvidence(
    string Input,
    string Configuration,
    bool Required,
    TestRunOutcome Outcome,
    int ExitCode,
    int Total,
    int Passed,
    int Failed,
    int Skipped);

public sealed record ToolchainEvidence(
    string SdkVersion,
    string RuntimeVersion,
    string MsBuildVersion,
    string RoslynVersion,
    string OperatingSystem);

public sealed record PolicyEvidence(
    string Origin,
    string Path,
    string Sha256);

public sealed record EvidenceBundle(
    string SchemaVersion,
    string ToolVersion,
    string Input,
    AssayProfile RequestedProfile,
    EffectiveProfile Profile,
    bool IsAuthoritative,
    PolicyEvidence Policy,
    ToolchainEvidence Toolchain,
    ImmutableArray<AnalyzerEvidence> Analyzers,
    ImmutableArray<ProjectEvidence> Projects,
    ImmutableArray<RuleEvidence> Rules,
    ImmutableArray<Finding> Findings,
    ImmutableArray<MissingEvidence> Missing,
    ImmutableArray<EvaluationFailure> Failures,
    ImmutableArray<SuppressionEvidence> Suppressions,
    ImmutableArray<GeneratedCodeEvidence> GeneratedCode,
    ImmutableArray<TestRunEvidence> Tests,
    ImmutableArray<WorkspaceDiagnosticEvidence> WorkspaceDiagnostics,
    ImmutableArray<SourceEvidence> Sources)
{
    public static EvidenceBundle Empty(string input, bool isAuthoritative) => new(
        SchemaVersion: "1.1.0",
        ToolVersion: "0.1.1",
        Input: input,
        RequestedProfile: AssayProfile.Compat,
        Profile: EffectiveProfile.Compat,
        IsAuthoritative: isAuthoritative,
        Policy: new PolicyEvidence(
            "built-in-observe",
            string.Empty,
            "6889293b67ae1c51b40448fbe0e2f3578b663b04eab0bbdf6c537f5f26f7e273"),
        Toolchain: new ToolchainEvidence(
            string.Empty,
            Environment.Version.ToString(),
            string.Empty,
            string.Empty,
            Environment.OSVersion.Platform.ToString()),
        Analyzers: ImmutableArray<AnalyzerEvidence>.Empty,
        Projects: ImmutableArray<ProjectEvidence>.Empty,
        Rules: ImmutableArray<RuleEvidence>.Empty,
        Findings: ImmutableArray<Finding>.Empty,
        Missing: ImmutableArray<MissingEvidence>.Empty,
        Failures: ImmutableArray<EvaluationFailure>.Empty,
        Suppressions: ImmutableArray<SuppressionEvidence>.Empty,
        GeneratedCode: ImmutableArray<GeneratedCodeEvidence>.Empty,
        Tests: ImmutableArray<TestRunEvidence>.Empty,
        WorkspaceDiagnostics: ImmutableArray<WorkspaceDiagnosticEvidence>.Empty,
        Sources: ImmutableArray<SourceEvidence>.Empty);
}
