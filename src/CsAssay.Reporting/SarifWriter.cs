using System.Text;
using System.Text.Json;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class SarifWriter
{
    public static byte[] Write(AssayVerdict verdict, string? sourceRoot = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Indented = true,
                       SkipValidation = false
                   }))
        {
            writer.WriteStartObject();
            writer.WriteString("version", "2.1.0");
            writer.WriteString(
                "$schema",
                "https://docs.oasis-open.org/sarif/sarif/v2.1.0/errata01/os/schemas/sarif-schema-2.1.0.json");
            writer.WritePropertyName("runs");
            writer.WriteStartArray();
            writer.WriteStartObject();
            WriteTool(writer, verdict.Evidence.ToolVersion);
            WriteInvocation(writer, verdict);
            writer.WritePropertyName("originalUriBaseIds");
            writer.WriteStartObject();
            writer.WritePropertyName("%SRCROOT%");
            writer.WriteStartObject();
            writer.WriteString("uri", sourceRoot is null ? "csassay-source:/" : RootUri(sourceRoot));
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WritePropertyName("results");
            writer.WriteStartArray();
            foreach (var item in DiagnosticPresentation.Findings(verdict))
            {
                WriteResult(writer, item, verdict.Evidence, sourceRoot);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            writer.WriteString("verdict", verdict.Kind.ToString());
            writer.WriteNumber("exitCode", verdict.ExitCode);
            writer.WriteBoolean(
                "authoritative",
                verdict.Evidence.IsAuthoritative);
            writer.WriteString("verificationMode", verdict.Evidence.VerificationMode.ToString().ToLowerInvariant());
            writer.WriteString(
                "requestedProfile",
                verdict.Evidence.RequestedProfile.ToString());
            writer.WriteString(
                "effectiveProfile",
                verdict.Evidence.Profile.ToString());
            writer.WriteString(
                "policyOrigin",
                verdict.Evidence.Policy.Origin);
            writer.WriteString(
                "policyPath",
                verdict.Evidence.Policy.Path);
            writer.WriteString(
                "policySha256",
                verdict.Evidence.Policy.Sha256);
            writer.WriteString("policyTextSha256", verdict.Evidence.PolicyTextSha256);
            writer.WriteString("policyEvaluationDate", verdict.Evidence.PolicyEvaluationDate);
            writer.WriteString("findingIdentityRoot", verdict.Evidence.FindingIdentityRoot);
            writer.WriteNumber(
                "testsTotal",
                verdict.Evidence.Tests.Sum(test => test.Total));
            writer.WriteNumber(
                "testsPassed",
                verdict.Evidence.Tests.Sum(test => test.Passed));
            writer.WriteNumber(
                "testsFailed",
                verdict.Evidence.Tests.Sum(test => test.Failed));
            writer.WriteNumber(
                "testsSkipped",
                verdict.Evidence.Tests.Sum(test => test.Skipped));
            writer.WritePropertyName("analyzers");
            writer.WriteStartArray();
            foreach (var analyzer in verdict.Evidence.Analyzers)
            {
                writer.WriteStartObject();
                writer.WriteString("identity", analyzer.Identity);
                writer.WriteString(
                    "assemblyVersion",
                    analyzer.AssemblyVersion);
                writer.WriteString("sha256", analyzer.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var json = Encoding.UTF8.GetString(stream.ToArray()) + "\n";
        return Encoding.UTF8.GetBytes(json);
    }

    public static async Task WriteAsync(
        string path,
        AssayVerdict verdict,
        CancellationToken cancellationToken)
    {
        await AtomicReportWriter.WriteAsync([new RequestedReport(path, Write(verdict))], cancellationToken).ConfigureAwait(false);
    }

    private static void WriteTool(Utf8JsonWriter writer, string version)
    {
        writer.WritePropertyName("tool");
        writer.WriteStartObject();
        writer.WritePropertyName("driver");
        writer.WriteStartObject();
        writer.WriteString("name", "CSharpAssay");
        writer.WriteString("semanticVersion", version);
        writer.WriteString(
            "informationUri",
            "https://github.com/CanonFlow-Assay/CSharpAssay");
        writer.WritePropertyName("rules");
        writer.WriteStartArray();
        foreach (var rule in RuleCatalogue.All.OrderBy(
                     rule => rule.Id,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("id", rule.Id);
            writer.WriteString("name", rule.Title);
            writer.WritePropertyName("shortDescription");
            writer.WriteStartObject();
            writer.WriteString("text", rule.Title);
            writer.WriteEndObject();
            writer.WritePropertyName("fullDescription");
            writer.WriteStartObject();
            writer.WriteString("text", rule.Mechanism);
            writer.WriteEndObject();
            writer.WriteString(
                "helpUri",
                RuleCatalogue.DocumentationUrl(rule));
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            writer.WriteString("status", rule.Status.ToString());
            writer.WriteString("certainty", rule.Certainty.ToString());
            writer.WriteString("disposition", rule.Disposition.ToString());
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteResult(Utf8JsonWriter writer, PresentedFinding item, EvidenceBundle evidence, string? sourceRoot)
    {
        var finding = item.Finding;
        writer.WriteStartObject();
        writer.WriteString("ruleId", finding.RuleId);
        var ruleIndex = RuleCatalogue.All.OrderBy(rule => rule.Id, StringComparer.Ordinal).Select(rule => rule.Id).ToList().IndexOf(finding.RuleId);
        if (ruleIndex >= 0) { writer.WriteNumber("ruleIndex", ruleIndex); }
        writer.WriteString("level", item.Level);
        writer.WritePropertyName("message");
        writer.WriteStartObject();
        writer.WriteString("text", item.Message);
        writer.WriteEndObject();

        if (!string.IsNullOrEmpty(finding.Location.Path))
        {
            writer.WritePropertyName("locations");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WritePropertyName("physicalLocation");
            writer.WriteStartObject();
            writer.WritePropertyName("artifactLocation");
            writer.WriteStartObject();
            var artifactPath = sourceRoot is not null && !string.IsNullOrEmpty(finding.PhysicalPath)
                ? Fingerprints.NormalizePath(Path.GetRelativePath(sourceRoot, finding.PhysicalPath)) : finding.Location.Path;
            writer.WriteString("uri", ArtifactUri(artifactPath));
            if (!IsAbsoluteArtifact(artifactPath)) { writer.WriteString("uriBaseId", "%SRCROOT%"); }
            writer.WriteEndObject();
            writer.WritePropertyName("region");
            writer.WriteStartObject();
            writer.WriteNumber("startLine", Math.Max(1, finding.Location.StartLine));
            writer.WriteNumber("startColumn", Math.Max(1, finding.Location.StartColumn));
            writer.WriteNumber("endLine", Math.Max(Math.Max(1, finding.Location.StartLine), finding.Location.EndLine));
            writer.WriteNumber("endColumn", Math.Max(finding.Location.EndLine <= finding.Location.StartLine ? Math.Max(1, finding.Location.StartColumn) : 1, finding.Location.EndColumn));
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        if (item.AcceptedSuppression)
        {
            writer.WritePropertyName("suppressions");
            writer.WriteStartArray();
            writer.WriteStartObject();
            var authorization = evidence.Suppressions.FirstOrDefault(suppression => suppression.Authorized && suppression.RuleId == finding.RuleId &&
                (suppression.Fingerprint.Length > 0 ? suppression.Fingerprint == finding.Fingerprint : suppression.Location == finding.Location));
            writer.WriteString("kind", authorization?.Form == "effective-diagnostic-suppression" ? "inSource" : "external");
            writer.WriteString("status", "accepted");
            writer.WriteString("justification", DiagnosticPresentation.Sanitize(authorization?.Justification ?? "Accepted policy grant; see evidence suppression records."));
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        writer.WritePropertyName("partialFingerprints");
        writer.WriteStartObject();
        writer.WriteString("csAssay/v1", finding.Fingerprint);
        writer.WriteEndObject();
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteString("project", finding.Project);
        writer.WriteString("targetFramework", finding.TargetFramework);
        writer.WriteString("certainty", finding.Certainty.ToString());
        writer.WriteString("disposition", finding.Disposition.ToString());
        writer.WriteString("scope", finding.Scope);
        writer.WriteString("identityPath", finding.IdentityPath);
        writer.WriteString("effectiveSeverity", item.Level);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteInvocation(Utf8JsonWriter writer, AssayVerdict verdict)
    {
        writer.WritePropertyName("invocations");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteNumber("exitCode", verdict.ExitCode);
        writer.WriteBoolean("executionSuccessful", DiagnosticPresentation.ExecutionSuccessful(verdict));
        writer.WriteString("exitCodeDescription", DiagnosticPresentation.Summary(verdict));
        var diagnostics = DiagnosticPresentation.ExecutionDiagnostics(verdict);
        foreach (var kind in new[] { "execution", "configuration" })
        {
            writer.WritePropertyName(kind == "execution" ? "toolExecutionNotifications" : "toolConfigurationNotifications");
            writer.WriteStartArray();
            foreach (var diagnostic in diagnostics.Where(diagnostic => kind == "execution" ? diagnostic.Kind is "execution" or "test" : diagnostic.Kind == "configuration"))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("descriptor");
                writer.WriteStartObject();
                writer.WriteString("id", diagnostic.Code);
                writer.WriteEndObject();
                writer.WriteString("level", "error");
                writer.WritePropertyName("message");
                writer.WriteStartObject();
                writer.WriteString("text", diagnostic.Message);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    private static bool IsAbsoluteArtifact(string path) => path.StartsWith('/') ||
        path.StartsWith(@"\\", StringComparison.Ordinal) || path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static string ArtifactUri(string path)
    {
        var normalized = Fingerprints.NormalizePath(path);
        if (normalized.Length >= 3 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':')
        {
            return "file:///" + normalized[..2] + "/" + string.Join("/", normalized[3..].Split('/').Select(Uri.EscapeDataString));
        }
        if (normalized.StartsWith("//", StringComparison.Ordinal)) { return new Uri("file:" + normalized).AbsoluteUri; }
        if (normalized.StartsWith('/')) { return new Uri(normalized).AbsoluteUri; }
        return string.Join("/", normalized.Split('/').Select(part => Uri.EscapeDataString(part)));
    }

    private static string RootUri(string root) => ArtifactUri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
}
