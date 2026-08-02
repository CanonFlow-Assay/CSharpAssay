using System.Text;
using System.Text.Json;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class JsonEvidenceWriter
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
            writer.WriteString("schemaVersion", verdict.Evidence.SchemaVersion);
            writer.WriteString("verdict", ToText(verdict.Kind));
            writer.WriteNumber("exitCode", verdict.ExitCode);
            WriteEvidence(writer, verdict.Evidence);
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
        EnsureDirectory(path);
        await File.WriteAllBytesAsync(
            path,
            Write(verdict),
            cancellationToken).ConfigureAwait(false);
    }

    private static void WriteEvidence(
        Utf8JsonWriter writer,
        EvidenceBundle evidence)
    {
        writer.WritePropertyName("evidence");
        writer.WriteStartObject();
        writer.WriteString("toolVersion", evidence.ToolVersion);
        writer.WriteString("input", evidence.Input);
        writer.WriteString(
            "requestedProfile",
            ToText(evidence.RequestedProfile));
        writer.WriteString("profile", ToText(evidence.Profile));
        writer.WriteBoolean("authoritative", evidence.IsAuthoritative);
        writer.WritePropertyName("policy");
        writer.WriteStartObject();
        writer.WriteString("origin", evidence.Policy.Origin);
        writer.WriteString("path", evidence.Policy.Path);
        writer.WriteString("sha256", evidence.Policy.Sha256);
        writer.WriteEndObject();
        WriteToolchain(writer, evidence.Toolchain);

        writer.WritePropertyName("analyzers");
        writer.WriteStartArray();
        foreach (var analyzer in evidence.Analyzers)
        {
            writer.WriteStartObject();
            writer.WriteString("identity", analyzer.Identity);
            writer.WriteString("assemblyVersion", analyzer.AssemblyVersion);
            writer.WriteString("sha256", analyzer.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("projects");
        writer.WriteStartArray();
        foreach (var project in evidence.Projects)
        {
            writer.WriteStartObject();
            writer.WriteString("name", project.Name);
            writer.WriteString("path", project.Path);
            writer.WriteString("targetFramework", project.TargetFramework);
            writer.WriteString("profile", ToText(project.Profile));
            writer.WriteString("profileEvidence", project.ProfileEvidence);
            writer.WriteString("languageVersion", project.LanguageVersion);
            writer.WriteString("nullableContext", project.NullableContext);
            writer.WriteBoolean("loaded", project.Loaded);
            writer.WritePropertyName("projectReferences");
            writer.WriteStartArray();
            foreach (var reference in project.ProjectReferences)
            {
                writer.WriteStringValue(reference);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("compilerDiagnostics");
            writer.WriteStartArray();
            foreach (var diagnostic in project.CompilerDiagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("id", diagnostic.Id);
                writer.WriteString("severity", diagnostic.Severity);
                writer.WriteString("message", diagnostic.Message);
                WriteLocation(writer, diagnostic.Location);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("rules");
        writer.WriteStartArray();
        foreach (var rule in evidence.Rules)
        {
            writer.WriteStartObject();
            writer.WriteString("id", rule.RuleId);
            writer.WriteBoolean("required", rule.Required);
            writer.WriteString("outcome", ToText(rule.Outcome));
            writer.WriteNumber("findingCount", rule.FindingCount);
            if (rule.Reason is Presence<string>.Present reason)
            {
                writer.WriteString("reason", reason.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("findings");
        writer.WriteStartArray();
        foreach (var finding in evidence.Findings)
        {
            writer.WriteStartObject();
            writer.WriteString("ruleId", finding.RuleId);
            writer.WriteString("message", finding.Message);
            writer.WriteString("severity", ToText(finding.Severity));
            writer.WriteString("certainty", ToText(finding.Certainty));
            writer.WriteString("disposition", ToText(finding.Disposition));
            writer.WriteBoolean("suppressed", finding.Suppressed);
            writer.WriteString("project", finding.Project);
            writer.WriteString("targetFramework", finding.TargetFramework);
            WriteLocation(writer, finding.Location);
            writer.WriteString("fingerprint", finding.Fingerprint);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("missingEvidence");
        writer.WriteStartArray();
        foreach (var item in evidence.Missing)
        {
            writer.WriteStartObject();
            writer.WriteString("code", item.Code);
            writer.WriteString("message", item.Message);
            writer.WriteString("project", item.Project);
            writer.WriteString("targetFramework", item.TargetFramework);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("failures");
        writer.WriteStartArray();
        foreach (var failure in evidence.Failures)
        {
            writer.WriteStartObject();
            writer.WriteString("code", failure.Code);
            writer.WriteString("message", failure.Message);
            writer.WriteString("component", failure.Component);
            if (failure.RuleId is Presence<string>.Present ruleId)
            {
                writer.WriteString("ruleId", ruleId.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("suppressions");
        writer.WriteStartArray();
        foreach (var suppression in evidence.Suppressions)
        {
            writer.WriteStartObject();
            writer.WriteString("ruleId", suppression.RuleId);
            writer.WriteString("form", suppression.Form);
            writer.WriteString("justification", suppression.Justification);
            writer.WriteBoolean("authorized", suppression.Authorized);
            WriteLocation(writer, suppression.Location);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("generatedCode");
        writer.WriteStartArray();
        foreach (var generated in evidence.GeneratedCode)
        {
            writer.WriteStartObject();
            writer.WriteString("path", generated.Path);
            writer.WriteString("reason", generated.Reason);
            writer.WriteBoolean("excluded", generated.Excluded);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("tests");
        writer.WriteStartArray();
        foreach (var test in evidence.Tests)
        {
            writer.WriteStartObject();
            writer.WriteString("input", test.Input);
            writer.WriteString("configuration", test.Configuration);
            writer.WriteBoolean("required", test.Required);
            writer.WriteString("outcome", ToText(test.Outcome));
            writer.WriteNumber("exitCode", test.ExitCode);
            writer.WriteNumber("total", test.Total);
            writer.WriteNumber("passed", test.Passed);
            writer.WriteNumber("failed", test.Failed);
            writer.WriteNumber("skipped", test.Skipped);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("workspaceDiagnostics");
        writer.WriteStartArray();
        foreach (var diagnostic in evidence.WorkspaceDiagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", diagnostic.Kind);
            writer.WriteString("message", diagnostic.Message);
            writer.WriteString("project", diagnostic.Project);
            writer.WriteString("targetFramework", diagnostic.TargetFramework);
            writer.WriteBoolean(
                "affectsCompleteness",
                diagnostic.AffectsCompleteness);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("sources");
        writer.WriteStartArray();
        foreach (var source in evidence.Sources)
        {
            writer.WriteStartObject();
            writer.WriteString("path", source.Path);
            writer.WriteString("sha256", source.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteToolchain(
        Utf8JsonWriter writer,
        ToolchainEvidence toolchain)
    {
        writer.WritePropertyName("toolchain");
        writer.WriteStartObject();
        writer.WriteString("sdkVersion", toolchain.SdkVersion);
        writer.WriteString("runtimeVersion", toolchain.RuntimeVersion);
        writer.WriteString("msbuildVersion", toolchain.MsBuildVersion);
        writer.WriteString("roslynVersion", toolchain.RoslynVersion);
        writer.WriteString("operatingSystem", toolchain.OperatingSystem);
        writer.WriteEndObject();
    }

    private static void WriteLocation(
        Utf8JsonWriter writer,
        SourceSpan location)
    {
        writer.WritePropertyName("location");
        writer.WriteStartObject();
        writer.WriteString("path", location.Path);
        writer.WriteNumber("startLine", location.StartLine);
        writer.WriteNumber("startColumn", location.StartColumn);
        writer.WriteNumber("endLine", location.EndLine);
        writer.WriteNumber("endColumn", location.EndColumn);
        writer.WriteEndObject();
    }

    private static string ToText(AssayVerdictKind value) =>
        value switch
        {
            AssayVerdictKind.Pass => "pass",
            AssayVerdictKind.Inconclusive => "inconclusive",
            AssayVerdictKind.Fail => "fail",
            AssayVerdictKind.ToolFailure => "toolFailure",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported verdict kind.")
        };

    private static string ToText(EffectiveProfile value) =>
        value switch
        {
            EffectiveProfile.Compat => "compat",
            EffectiveProfile.NativePreview => "native-preview",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported effective profile.")
        };

    private static string ToText(AssayProfile value) =>
        value switch
        {
            AssayProfile.Auto => "auto",
            AssayProfile.Compat => "compat",
            AssayProfile.Native => "native",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported requested profile.")
        };

    private static string ToText(TestRunOutcome value) =>
        value switch
        {
            TestRunOutcome.Passed => "passed",
            TestRunOutcome.Failed => "failed",
            TestRunOutcome.NotRun => "notRun",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported test outcome.")
        };

    private static string ToText(RuleOutcome value) =>
        value switch
        {
            RuleOutcome.Completed => "completed",
            RuleOutcome.Incomplete => "incomplete",
            RuleOutcome.Skipped => "skipped",
            RuleOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported rule outcome.")
        };

    private static string ToText(FindingSeverity value) =>
        value.ToString().ToLowerInvariant();

    private static string ToText(RuleCertainty value) =>
        value.ToString().ToLowerInvariant();

    private static string ToText(RuleDisposition value) =>
        value.ToString().ToLowerInvariant();

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
