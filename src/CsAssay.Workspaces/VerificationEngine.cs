using System.Collections.Immutable;
using System.Security.Cryptography;
using CsAssay.Analyzers;
using CsAssay.Catalogue;
using CsAssay.Domain;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.Workspaces;

public sealed record VerificationRequest(
    string InputPath,
    Presence<string> PolicyPath,
    bool IsAuthoritative,
    bool ExecuteTests,
    Presence<AssayProfile> ProfileOverride);

public sealed record VerificationResult(
    AssayVerdict Verdict,
    AssayPolicy Policy,
    Presence<string> PolicyPath);

public static class VerificationEngine
{
    public static Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken) =>
        VerifyAsync(request, TimeProvider.System, cancellationToken);

    public static async Task<VerificationResult> VerifyAsync(
        VerificationRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var evaluationDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var fullInputPath = Path.GetFullPath(request.InputPath);
        var rootPath = Path.GetDirectoryName(fullInputPath) is string directory
            ? directory
            : Directory.GetCurrentDirectory();
        var policyResult = PolicyLoader.Load(
            fullInputPath,
            request.PolicyPath,
            request.IsAuthoritative);
        var policy = request.ProfileOverride switch
        {
            Presence<AssayProfile>.Present profile => policyResult.Policy with
            {
                Profile = profile.Value
            },
            _ => policyResult.Policy
        };
        var policyRoot = policyResult.Path is Presence<string>.Present declaredPolicy
            ? Path.GetDirectoryName(declaredPolicy.Value) ?? rootPath : rootPath;
        var normalizedPolicy = policyResult.Evaluation is Presence<PolicyEvaluation>.Present evaluated
            ? evaluated.Value : PolicyEvaluation.Normalize(policy, RuleCatalogue.All.Select(rule => rule.Id).ToImmutableArray(), OperatingSystem.IsWindows());
        var policyTextHash = policyResult.TextSha256;
        var failures = policyResult.Failures.ToBuilder();
        var missing = ImmutableArray.CreateBuilder<MissingEvidence>();
        var findings = ImmutableArray.CreateBuilder<Finding>();
        var projects = ImmutableArray.CreateBuilder<ProjectEvidence>();
        var suppressions = ImmutableArray.CreateBuilder<SuppressionEvidence>();
        var generatedCode = ImmutableArray.CreateBuilder<GeneratedCodeEvidence>();
        var workspaceDiagnostics =
            ImmutableArray.CreateBuilder<WorkspaceDiagnosticEvidence>();
        var sources = new Dictionary<string, SourceEvidence>(StringComparer.Ordinal);
        var profiles = ImmutableArray.CreateBuilder<EffectiveProfile>();
        var analyzerFailed = false;

        WorkspaceLoadResult workspaceResult;
        try
        {
            workspaceResult = await WorkspaceLoader
                .LoadAsync(fullInputPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failures.Add(new EvaluationFailure(
                "CSASSAY-WORKSPACE-CRASH",
                exception.Message,
                "MSBuildWorkspace",
                Presence.Missing<string>()));
            workspaceResult = new WorkspaceLoadResult(
                ImmutableArray<WorkspaceCompilation>.Empty,
                ImmutableArray<WorkspaceMessage>.Empty,
                string.Empty,
                string.Empty);
        }

        AddWorkspaceMessages(
            workspaceResult.Messages,
            failures,
            missing,
            workspaceDiagnostics,
            rootPath);
        if (request.IsAuthoritative)
        {
            CheckRequiredTargetFrameworks(
                policy,
                workspaceResult.Compilations,
                failures);
        }

        var unmatchedProjects = normalizedPolicy.UnmatchedProjects(workspaceResult.Compilations.Select(unit => RelativizePath(unit.ProjectPath, policyRoot)));
        var solutionScope = Path.GetExtension(fullInputPath) is ".sln" or ".slnx";
        foreach (var selector in unmatchedProjects)
        {
            var message = "Project selector did not match the evaluated graph: " + selector;
            workspaceDiagnostics.Add(new WorkspaceDiagnosticEvidence("UnusedProjectSelector", message, "", "", solutionScope));
            if (solutionScope)
            {
                missing.Add(new MissingEvidence("CSASSAY-BOUNDARY-PROJECT-UNMATCHED", message, "", ""));
            }
        }
        var declaredNamespaces = workspaceResult.Compilations.SelectMany(unit => EnumerateNamespaces(unit.Compilation.Assembly.GlobalNamespace))
            .Select(symbol => symbol.ToDisplayString()).ToImmutableArray();
        foreach (var selector in policy.Boundaries.CoreNamespaces.Concat(policy.Boundaries.ShellNamespaces).Order(StringComparer.Ordinal))
        {
            if (!declaredNamespaces.Any(name => PolicyEvaluation.MatchesNamespace(name, [selector])))
            {
                workspaceDiagnostics.Add(new WorkspaceDiagnosticEvidence("UnusedNamespaceSelector",
                    "Namespace selector was not observed in this evaluated compilation graph: " + selector + ". Other configurations and unloaded references were not inspected.", "", "", false));
            }
        }

        foreach (var unit in workspaceResult.Compilations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var negotiation = ProfileNegotiator.Negotiate(
                policy.Profile,
                unit,
                policy.Release.AllowPreviewToolchain);
            profiles.Add(negotiation.Profile);
            if (negotiation.Missing is Presence<MissingEvidence>.Present profileMissing)
            {
                missing.Add(profileMissing.Value);
            }

            var compilerDiagnostics = unit.Compilation
                .GetDiagnostics(cancellationToken)
                .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ThenBy(
                    diagnostic => diagnostic.Location.SourceTree is SyntaxTree tree
                        ? tree.FilePath
                        : string.Empty,
                    StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .Select(diagnostic => new CompilerEvidence(
                    diagnostic.Id,
                    diagnostic.Severity.ToString(),
                    diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
                    Relativize(
                        DiagnosticProjection.ToSourceSpan(diagnostic.Location),
                        rootPath)))
                .ToImmutableArray();

            if (compilerDiagnostics.Any(diagnostic =>
                    string.Equals(
                        diagnostic.Severity,
                        DiagnosticSeverity.Error.ToString(),
                        StringComparison.Ordinal)))
            {
                missing.Add(new MissingEvidence(
                    "CSASSAY-COMPILER-ERRORS",
                    "Compiler errors prevent complete semantic evidence.",
                    unit.Name,
                    unit.TargetFramework));
            }

            projects.Add(new ProjectEvidence(
                unit.Name,
                RelativizePath(unit.ProjectPath, rootPath),
                unit.TargetFramework,
                negotiation.Profile,
                negotiation.EvidenceName,
                GetLanguageVersion(unit.Compilation),
                unit.Compilation.Options.NullableContextOptions.ToString(),
                Loaded: true,
                unit.ProjectReferences
                    .Select(reference => RelativizePath(reference, rootPath))
                    .OrderBy(reference => reference, StringComparer.Ordinal)
                    .ToImmutableArray(),
                compilerDiagnostics));

            CaptureSourceAndGeneratedEvidence(
                unit,
                rootPath,
                sources,
                generatedCode);

            AnalyzerExecutionResult analyzerResult;
            try
            {
                analyzerResult = await AnalyzerExecutor.ExecuteAsync(
                    unit.Compilation,
                    [new FunctionalPolicyAnalyzer(new AnalysisPolicyContext(normalizedPolicy, policyRoot, unit.ProjectPath, unit.Name,
                        unit.TargetFramework, evaluationDate, true, policyResult.UsedDefault ? "built-in-observe" : "explicit-policy", policyTextHash))],
                    PolicyAnalyzerOptions.Create(policy),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                analyzerFailed = true;
                failures.Add(new EvaluationFailure(
                    "CSASSAY-ANALYZER-RUN-FAILED",
                    exception.Message,
                    "analyzer-host",
                    Presence.Missing<string>()));
                continue;
            }

            foreach (var analyzerFailure in analyzerResult.Failures)
            {
                analyzerFailed = true;
                failures.Add(new EvaluationFailure(
                    "CSASSAY-ANALYZER-CRASH",
                    analyzerFailure.ExceptionType + ": " + analyzerFailure.Message,
                    analyzerFailure.Analyzer,
                    analyzerFailure.DiagnosticId));
            }

            foreach (var diagnostic in analyzerResult.Diagnostics)
            {
                if (RuleCatalogue.Find(diagnostic.Id) is not
                    Presence<RuleRecord>.Present)
                {
                    continue;
                }

                var span = Relativize(
                    DiagnosticProjection.ToSourceSpan(diagnostic.Location),
                    rootPath);
                var rule = RuleCatalogue.Find(diagnostic.Id) is Presence<RuleRecord>.Present foundRule
                    ? foundRule.Value : throw new InvalidOperationException("Unknown rule.");
                string Property(string key) => diagnostic.Properties.TryGetValue(key, out var value) ? value ?? "" : "";
                var accepted = Property("csassay_accepted") == "true" && diagnostic.Id != RuleIds.UnauthorizedSuppression;
                if (diagnostic.Id == RuleIds.NullableDisabled)
                {
                    missing.Add(new MissingEvidence("CSASSAY-NULLABLE-FLOW-INCOMPLETE", "Nullable analysis was disabled in the evaluated compilation; boundaries and grants cannot restore that evidence.", unit.Name, unit.TargetFramework));
                }
                var disposition = Enum.TryParse<RuleDisposition>(Property("csassay_disposition"), out var effective) ? effective : rule.Disposition;
                var finding = new Finding(diagnostic.Id, diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
                    rule.Disposition == RuleDisposition.Block ? FindingSeverity.Warning : FindingSeverity.Info,
                    rule.Certainty, disposition, accepted, unit.Name, unit.TargetFramework, span, Property("csassay_fingerprint"))
                {
                    Scope = Property("csassay_scope"),
                    IdentityPath = string.IsNullOrEmpty(diagnostic.Location.GetLineSpan().Path) ? "" : Fingerprints.NormalizePath(Path.GetRelativePath(policyRoot, diagnostic.Location.GetLineSpan().Path)),
                    PhysicalPath = diagnostic.Location.GetLineSpan().Path
                };
                findings.Add(finding);
                if (accepted || diagnostic.IsSuppressed)
                {
                    suppressions.Add(new SuppressionEvidence(diagnostic.Id,
                        diagnostic.IsSuppressed ? "effective-diagnostic-suppression" : "policy-grant",
                        accepted ? Property("csassay_justification") : "No matching unexpired grant.", accepted, span)
                    {
                        Fingerprint = finding.Fingerprint,
                        Owner = Property("csassay_owner"),
                        EvaluationDate = PolicyEvaluation.EvaluationDateText(evaluationDate)
                    });
                    if (!accepted && diagnostic.Id != RuleIds.UnauthorizedSuppression)
                    {
                        var message = "Suppressed " + diagnostic.Id + " has no unexpired fingerprinted grant.";
                        findings.Add(new Finding(RuleIds.UnauthorizedSuppression, message, FindingSeverity.Warning,
                            RuleCertainty.Deterministic, RuleDisposition.Block, false, unit.Name, unit.TargetFramework, span,
                            Fingerprints.Finding(RuleIds.UnauthorizedSuppression, unit.Name, unit.TargetFramework,
                                Relativize(DiagnosticProjection.ToSourceSpan(diagnostic.Location), policyRoot), message))
                        { Scope = finding.Scope, IdentityPath = finding.IdentityPath });
                    }
                }
            }
        }

        if (request.IsAuthoritative &&
            !RuleCatalogue.All.Any(rule => rule.Status == RuleStatus.Admitted))
        {
            missing.Add(new MissingEvidence(
                "CSASSAY-NO-ADMITTED-RULES",
                "The 0.1 research preview has no admitted blocking rules and cannot issue release authority.",
                string.Empty,
                string.Empty));
        }

        var overallProfile = EffectiveProfile.Native;
        var orderedFindings = findings
            .OrderBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Project, StringComparer.Ordinal)
            .ThenBy(finding => finding.TargetFramework, StringComparer.Ordinal)
            .ThenBy(finding => finding.Location.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.Location.StartLine)
            .ThenBy(finding => finding.Location.StartColumn)
            .ThenBy(finding => finding.Message, StringComparer.Ordinal)
            .ToImmutableArray();
        AddRequiredRuleGaps(
            policy.Release.RequiredRules,
            overallProfile,
            missing,
            failures);
        var ruleEvidence = BuildRuleEvidence(
            overallProfile,
            orderedFindings,
            policy.Release.RequiredRules,
            analyzerFailed);
        var testResult = await TestExecutor.ExecuteAsync(
            rootPath,
            policyResult.Path,
            policy.Release.Tests,
            request.IsAuthoritative,
            request.ExecuteTests,
            prerequisitesComplete:
                failures.Count == 0 && missing.Count == 0,
            cancellationToken).ConfigureAwait(false);
        missing.AddRange(testResult.Missing);
        failures.AddRange(testResult.Failures);
        var toolchain = new ToolchainEvidence(
            workspaceResult.SdkVersion,
            Environment.Version.ToString(),
            workspaceResult.MsBuildVersion,
            typeof(CSharpCompilation).Assembly.GetName().Version is Version version
                ? version.ToString()
                : string.Empty,
            Environment.OSVersion.ToString());
        var evidence = new EvidenceBundle(
            SchemaVersion: "1.2.0",
            ToolVersion: "0.2.0-rc.1",
            Input: Path.GetFileName(fullInputPath),
            RequestedProfile: policy.Profile,
            Profile: overallProfile,
            IsAuthoritative: request.IsAuthoritative,
            Policy: CreatePolicyEvidence(policyResult, rootPath),
            Toolchain: toolchain,
            Analyzers: CreateAnalyzerEvidence(),
            Projects: projects
                .OrderBy(project => project.Path, StringComparer.Ordinal)
                .ThenBy(project => project.TargetFramework, StringComparer.Ordinal)
                .ToImmutableArray(),
            Rules: ruleEvidence,
            Findings: orderedFindings,
            Missing: missing
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.Project, StringComparer.Ordinal)
                .ThenBy(item => item.TargetFramework, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToImmutableArray(),
            Failures: failures
                .OrderBy(failure => failure.Code, StringComparer.Ordinal)
                .ThenBy(failure => failure.Component, StringComparer.Ordinal)
                .ThenBy(
                    failure => OptionalText(failure.RuleId),
                    StringComparer.Ordinal)
                .ThenBy(failure => failure.Message, StringComparer.Ordinal)
                .ToImmutableArray(),
            Suppressions: suppressions
                .OrderBy(item => item.RuleId, StringComparer.Ordinal)
                .ThenBy(item => item.Location.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Location.StartLine)
                .ToImmutableArray(),
            GeneratedCode: generatedCode
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Reason, StringComparer.Ordinal)
                .ToImmutableArray(),
            Tests: testResult.Tests
                .OrderBy(item => item.Input, StringComparer.Ordinal)
                .ThenBy(item => item.Configuration, StringComparer.Ordinal)
                .ToImmutableArray(),
            WorkspaceDiagnostics: workspaceDiagnostics
                .OrderBy(item => item.Project, StringComparer.Ordinal)
                .ThenBy(item => item.TargetFramework, StringComparer.Ordinal)
                .ThenBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToImmutableArray(),
            Sources: sources.Values
                .OrderBy(source => source.Path, StringComparer.Ordinal)
                .ToImmutableArray())
        {
            PolicyEvaluationDate = PolicyEvaluation.EvaluationDateText(evaluationDate),
            PolicyTextSha256 = policyTextHash,
            VerificationMode = request.IsAuthoritative ? VerificationMode.Verification : VerificationMode.Analysis
        };
        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);
        return new VerificationResult(verdict, policy, policyResult.Path);
    }

    private static IEnumerable<INamespaceSymbol> EnumerateNamespaces(
        INamespaceSymbol root)
    {
        foreach (var child in root.GetNamespaceMembers())
        {
            yield return child;
            foreach (var nested in EnumerateNamespaces(child))
            {
                yield return nested;
            }
        }
    }

    private static void CaptureSourceAndGeneratedEvidence(
        WorkspaceCompilation unit,
        string rootPath,
        Dictionary<string, SourceEvidence> sources,
        ImmutableArray<GeneratedCodeEvidence>.Builder generatedCode)
    {
        var documentPaths = unit.DocumentPaths
            .Select(Path.GetFullPath)
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tree in unit.Compilation.SyntaxTrees.OrderBy(
                     tree => tree.FilePath,
                     StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(tree.FilePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(tree.FilePath);
            var relativePath = RelativizePath(fullPath, rootPath);
            if (File.Exists(fullPath) && !sources.ContainsKey(relativePath))
            {
                sources.Add(
                    relativePath,
                    new SourceEvidence(relativePath, HashFile(fullPath)));
            }

            if (!documentPaths.Contains(fullPath))
            {
                generatedCode.Add(new GeneratedCodeEvidence(
                    relativePath,
                    "source-generator output",
                    Excluded: true));
                continue;
            }

            var text = tree.GetText().ToString();
            var prefix = text.Length > 512 ? text[..512] : text;
            if (prefix.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase))
            {
                generatedCode.Add(new GeneratedCodeEvidence(
                    relativePath,
                    "compiler-recognized auto-generated header",
                    Excluded: true));
            }
        }
    }

    private static ImmutableArray<RuleEvidence> BuildRuleEvidence(
        EffectiveProfile profile,
        ImmutableArray<Finding> findings,
        ImmutableArray<string> requiredRules,
        bool analyzerFailed) =>
        RuleCatalogue.All
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(rule =>
            {
                var applies = rule.Profiles.Contains(profile);
                var required = requiredRules.Contains(
                    rule.Id,
                    StringComparer.Ordinal);
                return new RuleEvidence(
                    rule.Id,
                    required,
                    analyzerFailed && applies
                        ? RuleOutcome.Failed
                        : applies
                            ? RuleOutcome.Completed
                            : RuleOutcome.Skipped,
                    applies
                        ? findings.Count(finding => string.Equals(
                            finding.RuleId,
                            rule.Id,
                            StringComparison.Ordinal))
                        : 0,
                    applies
                        ? Presence.Missing<string>()
                        : Presence.Of(
                            "Rule does not apply to " + profile + "."));
            })
            .ToImmutableArray();

    private static void AddRequiredRuleGaps(
        ImmutableArray<string> requiredRules,
        EffectiveProfile profile,
        ImmutableArray<MissingEvidence>.Builder missing,
        ImmutableArray<EvaluationFailure>.Builder failures)
    {
        foreach (var ruleId in requiredRules.OrderBy(
                     item => item,
                     StringComparer.Ordinal))
        {
            if (RuleCatalogue.Find(ruleId) is not
                Presence<RuleRecord>.Present found)
            {
                failures.Add(new EvaluationFailure(
                    "CSASSAY-REQUIRED-RULE-UNKNOWN",
                    "Required rule is not in the catalogue: " + ruleId,
                    "policy",
                    Presence.Of(ruleId)));
                continue;
            }

            if (found.Value.Status != RuleStatus.Admitted)
            {
                missing.Add(new MissingEvidence(
                    "CSASSAY-REQUIRED-RULE-NOT-ADMITTED",
                    "Required rule is not admitted: " + ruleId,
                    string.Empty,
                    string.Empty));
                continue;
            }

            if (!found.Value.Profiles.Contains(profile))
            {
                missing.Add(new MissingEvidence(
                    "CSASSAY-REQUIRED-RULE-SKIPPED",
                    "Required rule does not apply to the effective profile: " +
                        ruleId,
                    string.Empty,
                    string.Empty));
            }
        }
    }

    private static ImmutableArray<AnalyzerEvidence> CreateAnalyzerEvidence()
    {
        var assembly = typeof(FunctionalPolicyAnalyzer).Assembly;
        var name = assembly.GetName();
        var location = assembly.Location;
        return
        [
            new AnalyzerEvidence(
                name.FullName ?? name.Name ?? "CsAssay.Analyzers",
                name.Version?.ToString() ?? string.Empty,
                string.IsNullOrEmpty(location) || !File.Exists(location)
                    ? string.Empty
                    : HashFile(location))
        ];
    }

    private static void AddWorkspaceMessages(
        ImmutableArray<WorkspaceMessage> messages,
        ImmutableArray<EvaluationFailure>.Builder failures,
        ImmutableArray<MissingEvidence>.Builder missing,
        ImmutableArray<WorkspaceDiagnosticEvidence>.Builder diagnostics,
        string rootPath)
    {
        foreach (var message in messages)
        {
            var project = message.ProjectPath switch
            {
                Presence<string>.Present present =>
                    RelativizePath(present.Value, rootPath),
                _ => string.Empty
            };
            var targetFramework = OptionalText(message.TargetFramework);
            var normalizedMessage = NormalizeWorkspaceMessage(
                message.Message,
                rootPath);
            diagnostics.Add(new WorkspaceDiagnosticEvidence(
                message.Kind,
                normalizedMessage,
                project,
                targetFramework,
                message.AffectsCompleteness));

            if (string.Equals(message.Kind, "Failure", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(new EvaluationFailure(
                    "CSASSAY-WORKSPACE-FAILURE",
                    normalizedMessage,
                    project.Length > 0
                        ? project
                        : "MSBuildWorkspace",
                    Presence.Missing<string>()));
            }
            else if (message.AffectsCompleteness)
            {
                missing.Add(new MissingEvidence(
                    "CSASSAY-WORKSPACE-WARNING",
                    normalizedMessage,
                    project,
                    targetFramework));
            }
        }
    }

    private static string NormalizeWorkspaceMessage(
        string message,
        string rootPath)
    {
        var windowsRoot = rootPath
            .Replace('/', '\\')
            .TrimEnd('\\') + "\\";
        var unixRoot = rootPath
            .Replace('\\', '/')
            .TrimEnd('/') + "/";
        return Fingerprints.NormalizePath(
            message
                .Replace(
                    windowsRoot,
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                .Replace(
                    unixRoot,
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void CheckRequiredTargetFrameworks(
        AssayPolicy policy,
        ImmutableArray<WorkspaceCompilation> units,
        ImmutableArray<EvaluationFailure>.Builder failures)
    {
        foreach (var required in policy.Release.RequiredTargetFrameworks)
        {
            if (units.Any(unit => string.Equals(
                    unit.TargetFramework,
                    required,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            failures.Add(new EvaluationFailure(
                "CSASSAY-REQUIRED-TFM-MISSING",
                "Required target framework was not loaded: " + required,
                "MSBuildWorkspace",
                Presence.Missing<string>()));
        }
    }

    private static string GetLanguageVersion(CSharpCompilation compilation) =>
        compilation.SyntaxTrees
            .Select(tree => tree.Options)
            .OfType<CSharpParseOptions>()
            .Select(options => options.LanguageVersion.ToDisplayString())
            .DefaultIfEmpty(string.Empty)
            .First();

    private static SourceSpan Relativize(SourceSpan span, string rootPath) =>
        span with
        {
            Path = string.IsNullOrEmpty(span.Path)
                ? string.Empty
                : RelativizePath(span.Path, rootPath)
        };

    private static string RelativizePath(string path, string rootPath)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(rootPath, fullPath);
        var normalized = Fingerprints.NormalizePath(relative);
        if (!Path.IsPathRooted(relative) &&
            !string.Equals(normalized, "..", StringComparison.Ordinal) &&
            !normalized.StartsWith("../", StringComparison.Ordinal))
        {
            return normalized;
        }

        return NormalizeExternalPath(fullPath);
    }

    private static string NormalizeExternalPath(string fullPath)
    {
        var normalized = Fingerprints.NormalizePath(fullPath);
        const string nugetMarker = "/.nuget/packages/";
        var nugetIndex = normalized.IndexOf(
            nugetMarker,
            StringComparison.OrdinalIgnoreCase);
        if (nugetIndex >= 0)
        {
            return "nuget/" + normalized[(nugetIndex + nugetMarker.Length)..];
        }

        var fileName = Path.GetFileName(fullPath);
        return File.Exists(fullPath)
            ? "external/" + HashFile(fullPath)[..16] + "/" + fileName
            : "external/" + fileName;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    private static PolicyEvidence CreatePolicyEvidence(
        PolicyLoadResult policyResult,
        string rootPath) =>
        policyResult.Path switch
        {
            Presence<string>.Present present => new PolicyEvidence(
                "file",
                RelativizePath(present.Value, rootPath),
                policyResult.RawSha256),
            _ => new PolicyEvidence(
                "built-in-observe",
                string.Empty,
                Convert.ToHexStringLower(SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        "csassay:built-in-observe:v1"))))
        };

    private static string OptionalText(Presence<string> value) =>
        value is Presence<string>.Present present
            ? present.Value
            : string.Empty;
}
