using System.Text;
using System.Text.Json;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class SarifWriter
{
    public static byte[] Write(AssayVerdict verdict)
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
                "https://json.schemastore.org/sarif-2.1.0.json");
            writer.WritePropertyName("runs");
            writer.WriteStartArray();
            writer.WriteStartObject();
            WriteTool(writer, verdict.Evidence.ToolVersion);
            writer.WritePropertyName("results");
            writer.WriteStartArray();
            foreach (var finding in verdict.Evidence.Findings)
            {
                WriteResult(writer, finding);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            writer.WriteString("verdict", verdict.Kind.ToString());
            writer.WriteNumber("exitCode", verdict.ExitCode);
            writer.WriteBoolean(
                "authoritative",
                verdict.Evidence.IsAuthoritative);
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
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(
            path,
            Write(verdict),
            cancellationToken).ConfigureAwait(false);
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

    private static void WriteResult(Utf8JsonWriter writer, Finding finding)
    {
        writer.WriteStartObject();
        writer.WriteString("ruleId", finding.RuleId);
        writer.WriteString("level", ToLevel(finding.Severity));
        writer.WritePropertyName("message");
        writer.WriteStartObject();
        writer.WriteString("text", finding.Message);
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
            writer.WriteString("uri", finding.Location.Path);
            writer.WriteString("uriBaseId", "%SRCROOT%");
            writer.WriteEndObject();
            writer.WritePropertyName("region");
            writer.WriteStartObject();
            writer.WriteNumber("startLine", Math.Max(1, finding.Location.StartLine));
            writer.WriteNumber("startColumn", Math.Max(1, finding.Location.StartColumn));
            writer.WriteNumber("endLine", Math.Max(1, finding.Location.EndLine));
            writer.WriteNumber("endColumn", Math.Max(1, finding.Location.EndColumn));
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        if (finding.Suppressed)
        {
            writer.WritePropertyName("suppressions");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("kind", "inSource");
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
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string ToLevel(FindingSeverity severity) =>
        severity switch
        {
            FindingSeverity.Error => "error",
            FindingSeverity.Warning => "warning",
            FindingSeverity.Info => "note",
            FindingSeverity.Hidden => "none",
            _ => "warning"
        };
}
