using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using CsAssay.Catalogue;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.Analyzers;

/// <summary>Host inputs; the normalized evaluator itself never reads files or clocks.</summary>
public sealed record AnalysisPolicyContext(
    PolicyEvaluation Evaluation,
    string PolicyRoot,
    string ProjectPath,
    string ProjectName,
    string TargetFramework,
    DateOnly UtcEvaluationDate,
    bool Enforce,
    string PolicyOrigin,
    string PolicyTextSha256);

internal sealed record SourceSuppression(string RuleId, Location Location, SyntaxTree Tree, int Start, int End, string NamespaceName = "");
internal sealed record ObservedPolicyFinding(string RuleId, Location Location, bool Authorized);

internal sealed class AnalysisPolicySession
{
    private readonly Compilation compilation;
    private readonly AnalyzerOptions options;
    private readonly ConcurrentBag<SourceSuppression> sourceSuppressions = [];
    private readonly ConcurrentBag<ObservedPolicyFinding> observed = [];
    private readonly bool declaresCoreNamespace;
    private readonly string projectIdentity;
    private readonly bool resolveNamespace;
    private readonly string evaluationDate;
    private readonly BoundaryDecision projectBoundary;

    private AnalysisPolicySession(Compilation compilation, AnalyzerOptions options, AnalysisPolicyContext host, string failure)
    {
        this.compilation = compilation;
        this.options = options;
        Host = host;
        Failure = failure;
        projectIdentity = Relative(host.ProjectPath);
        resolveNamespace = host.Evaluation.RequiresNamespaceEvaluation(projectIdentity);
        evaluationDate = PolicyEvaluation.EvaluationDateText(host.UtcEvaluationDate);
        declaresCoreNamespace = resolveNamespace && !host.Evaluation.Policy.Boundaries.CoreNamespaces.IsDefaultOrEmpty &&
            Namespaces(compilation.Assembly.GlobalNamespace).Any(name => PolicyEvaluation.MatchesNamespace(name, host.Evaluation.Policy.Boundaries.CoreNamespaces));
        projectBoundary = host.Evaluation.Classify(projectIdentity, "", false, declaresCoreNamespace);
    }

    public AnalysisPolicyContext Host { get; }
    public string Failure { get; }

    public static AnalysisPolicySession Create(Compilation compilation, AnalyzerOptions options, AnalysisPolicyContext? supplied, CancellationToken cancellationToken)
    {
        if (supplied is not null) { return new(compilation, options, supplied, ""); }
        var date = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var global = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        string Property(string name) => global.TryGetValue("build_property." + name, out var value) ? value : "";
        var project = Property("MSBuildProjectFullPath");
        var projectDirectory = string.IsNullOrEmpty(project) ? "" : Path.GetDirectoryName(project) ?? "";
        var policyPath = Property("CsAssayPolicyPath");
        var enforcementText = Property("CsAssayEnforceOnBuild");
        var enforce = string.Equals(enforcementText, "true", StringComparison.OrdinalIgnoreCase);
        var policy = AssayPolicy.Observe;
        var origin = "default";
        var textHash = "";
        var failure = "";
        if (enforcementText.Length > 0 && !enforce && !string.Equals(enforcementText, "false", StringComparison.OrdinalIgnoreCase))
        {
            enforce = true;
            failure = "CsAssayEnforceOnBuild must be true or false; malformed configuration cannot disable enforcement.";
        }
        var root = projectDirectory;
        try
        {
            if (policyPath.Length > 0)
            {
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                var texts = options.AdditionalFiles.Where(file => string.Equals(
                    Path.GetFullPath(file.Path), Path.GetFullPath(policyPath), comparison)).ToArray();
                if (texts.Length != 1 || texts[0].GetText(cancellationToken) is not { } text)
                {
                    throw new InvalidDataException("Declared policy is missing, unreadable or duplicated in AdditionalFiles.");
                }

                var json = text.ToString();
                policy = PolicyParser.Parse(json, RuleCatalogue.All.Select(rule => rule.Id).ToImmutableArray());
                root = Path.GetDirectoryName(Path.GetFullPath(policyPath)) ?? projectDirectory;
                origin = "explicit-additional-file";
                textHash = Fingerprints.Sha256(json);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            failure = "Policy could not be loaded: " + exception.Message;
        }

        var evaluation = PolicyEvaluation.Normalize(policy, RuleCatalogue.All.Select(rule => rule.Id).ToImmutableArray(), OperatingSystem.IsWindows());
        return new(compilation, options, new(evaluation, root, project,
            Property("MSBuildProjectName") is { Length: > 0 } name ? name : compilation.AssemblyName ?? "",
            Property("TargetFramework"), date, enforce, origin, textHash), failure);
    }

    public string NamespaceOf(ISymbol? symbol) => !resolveNamespace ? "" :
        (symbol as INamespaceSymbol ?? symbol?.ContainingNamespace)?.ToDisplayString() ?? "";

    public Diagnostic Create(string ruleId, Location location, string detail, string namespaceName = "")
    {
        var descriptor = DescriptorProvider.Get(ruleId);
        var rule = RuleCatalogue.Find(ruleId) is Presence<RuleRecord>.Present found
            ? found.Value : throw new InvalidOperationException("Unknown rule " + ruleId);
        var span = Span(location);
        var boundary = resolveNamespace && location.IsInSource
            ? Host.Evaluation.Classify(projectIdentity, namespaceName, true, declaresCoreNamespace) : projectBoundary;
        var disposition = PolicyEvaluation.Disposition(ruleId, rule.Disposition, boundary);
        var message = string.Format(CultureInfo.InvariantCulture, descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture), detail);
        var fingerprint = Fingerprints.Finding(ruleId, Host.ProjectName, Host.TargetFramework, span, message);
        var grant = Host.Evaluation.AcceptedGrant(ruleId, fingerprint, Host.UtcEvaluationDate);
        var authorized = grant is Presence<SuppressionGrant>.Present;
        observed.Add(new(ruleId, location, authorized));
        var severity = authorized || rule.Status != RuleStatus.Admitted ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning;
        if (Host.Enforce && !authorized && rule.Status == RuleStatus.Admitted && disposition == RuleDisposition.Block)
        {
            severity = DiagnosticSeverity.Error;
        }

        var properties = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        properties.Add("csassay_scope", boundary.Scope);
        properties.Add("csassay_disposition", disposition.ToString());
        properties.Add("csassay_fingerprint", fingerprint);
        properties.Add("csassay_accepted", authorized ? "true" : "false");
        properties.Add("csassay_evaluation_date", evaluationDate);
        if (grant is Presence<SuppressionGrant>.Present accepted)
        {
            properties.Add("csassay_justification", accepted.Value.Reason);
            properties.Add("csassay_owner", accepted.Value.Owner);
        }

        return Diagnostic.Create(DescriptorProvider.ForEffectiveSeverity(ruleId, severity, Host.Enforce && !authorized), location, properties.ToImmutable(), detail);
    }

    public void RegisterSourceSuppression(SourceSuppression suppression) => sourceSuppressions.Add(suppression);

    public void Complete(CompilationAnalysisContext context)
    {
        void Integrity(Location location, string detail, string namespaceName = "") => context.ReportDiagnostic(Create(RuleIds.UnauthorizedSuppression, location, detail, namespaceName));
        if (Failure.Length > 0) { Integrity(Location.None, Failure); }
        foreach (var marker in sourceSuppressions.OrderBy(item => item.Tree.FilePath, StringComparer.Ordinal).ThenBy(item => item.Start))
        {
            var affected = observed.Where(item => item.RuleId == marker.RuleId && item.Location.SourceTree == marker.Tree &&
                item.Location.SourceSpan.Start >= marker.Start && item.Location.SourceSpan.Start < marker.End).ToArray();
            if (marker.RuleId == RuleIds.UnauthorizedSuppression || affected.Length == 0 || affected.Any(item => !item.Authorized))
            {
                Integrity(marker.Location, "Source suppression for " + marker.RuleId + " lacks an unexpired grant for every affected finding.", marker.NamespaceName);
            }
        }

        if (!Host.Enforce) { return; }
        if (compilation.Options is CSharpCompilationOptions { NullableContextOptions: not NullableContextOptions.Enable } ||
            observed.Any(item => item.RuleId == RuleIds.NullableDisabled))
        {
            Integrity(Location.None, "Nullable-flow evidence is unavailable; a shell declaration or grant cannot restore compiler analysis.");
        }
        foreach (var tree in compilation.SyntaxTrees)
        {
            var configuration = options.AnalyzerConfigOptionsProvider.GetOptions(tree);
            foreach (var rule in RuleCatalogue.All.Where(rule => rule.Status == RuleStatus.Admitted))
            {
                if (compilation.Options.SyntaxTreeOptionsProvider is { } treeOptions &&
                    treeOptions.TryGetDiagnosticValue(tree, rule.Id, context.CancellationToken, out var configuredSeverity) &&
                    configuredSeverity is not (ReportDiagnostic.Default or ReportDiagnostic.Error))
                {
                    Integrity(tree.GetRoot(context.CancellationToken).GetLocation(), "Compiler configuration demotes admitted rule " + rule.Id + ".");
                }
                if (compilation.Options.SpecificDiagnosticOptions.TryGetValue(rule.Id, out var globalSeverity) &&
                    globalSeverity is not (ReportDiagnostic.Default or ReportDiagnostic.Error))
                {
                    Integrity(tree.GetRoot(context.CancellationToken).GetLocation(), "Compiler options demote admitted rule " + rule.Id + ".");
                }
                foreach (var key in new[] { "dotnet_diagnostic." + rule.Id + ".severity", "dotnet_analyzer_diagnostic.category-" + rule.Category + ".severity", "dotnet_analyzer_diagnostic.severity" })
                {
                    if ((configuration.TryGetValue(key, out var value) || options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(key, out value)) &&
                        value is not ("error" or "default"))
                    {
                        Integrity(tree.GetRoot(context.CancellationToken).GetLocation(), "Analyzer configuration demotes admitted rule " + rule.Id + " through " + key + "=" + value + ".");
                    }
                }
            }
        }
    }

    private SourceSpan Span(Location location)
    {
        if (!location.IsInSource) { return SourceSpan.None; }
        var span = location.GetLineSpan();
        return new(Relative(span.Path), span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1);
    }

    private string Relative(string path) => string.IsNullOrEmpty(path) ? "" :
        Fingerprints.NormalizePath(string.IsNullOrEmpty(Host.PolicyRoot) ? path : Path.GetRelativePath(Host.PolicyRoot, path));

    private static IEnumerable<string> Namespaces(INamespaceSymbol root)
    {
        foreach (var child in root.GetNamespaceMembers())
        {
            yield return child.ToDisplayString();
            foreach (var nested in Namespaces(child)) { yield return nested; }
        }
    }
}
