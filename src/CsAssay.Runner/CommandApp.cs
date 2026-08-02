using System.Text;
using System.Text.Json;
using CsAssay.Catalogue;
using CsAssay.Domain;
using CsAssay.Reporting;
using CsAssay.SdkAdapter;
using CsAssay.Workspaces;

namespace CsAssay.Runner;

public static class CommandApp
{
    private static readonly JsonSerializerOptions MigrationJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        CommandLine commandLine;
        try
        {
            commandLine = CommandLine.Parse(args);
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            await WriteHelpAsync(error).ConfigureAwait(false);
            return 64;
        }

        if (commandLine.Help || commandLine.Command == "help")
        {
            await WriteHelpAsync(output).ConfigureAwait(false);
            return 0;
        }

        return commandLine.Command switch
        {
            "doctor" => await DoctorAsync(output).ConfigureAwait(false),
            "catalog" => await CatalogAsync(commandLine, output).ConfigureAwait(false),
            "explain" => await ExplainAsync(commandLine, output, error).ConfigureAwait(false),
            "check" => await AssayAsync(
                commandLine,
                authoritative: false,
                output,
                error,
                cancellationToken).ConfigureAwait(false),
            "verify" => await AssayAsync(
                commandLine,
                authoritative: true,
                output,
                error,
                cancellationToken).ConfigureAwait(false),
            "migrate" => await MigrateAsync(
                commandLine,
                output,
                error,
                cancellationToken).ConfigureAwait(false),
            _ => await UnknownCommandAsync(commandLine.Command, error).ConfigureAwait(false)
        };
    }

    private static Task<int> DoctorAsync(TextWriter output)
    {
        try
        {
            var instance = WorkspaceLoader.RegisterMsBuild();
            output.WriteLine("CSharpAssay doctor");
            output.WriteLine("Runtime: " + Environment.Version);
            output.WriteLine("MSBuild: " + instance.Version);
            output.WriteLine(
                "Roslyn: " +
                typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation)
                    .Assembly.GetName().Version);
            output.WriteLine(
                "Native union symbol surface: " +
                (UnionCapabilities.CompilerExposesUnionSymbols()
                    ? "available (qualification still required)"
                    : "unavailable"));
            output.WriteLine("Stable compat lane: ready");
            return Task.FromResult(0);
        }
        catch (Exception exception)
        {
            output.WriteLine("CSharpAssay doctor failed: " + exception.Message);
            return Task.FromResult(3);
        }
    }

    private static Task<int> CatalogAsync(
        CommandLine commandLine,
        TextWriter output)
    {
        var profile = commandLine.Profile switch
        {
            Presence<AssayProfile>.Present
            {
                Value: AssayProfile.Native
            } => EffectiveProfile.NativePreview,
            _ => EffectiveProfile.Compat
        };

        foreach (var rule in RuleCatalogue.All
                     .Where(rule => rule.Profiles.Contains(profile))
                     .OrderBy(rule => rule.Id, StringComparer.Ordinal))
        {
            output.WriteLine(
                rule.Id + "  " + rule.Status + "  " + rule.Certainty + "  " +
                rule.Disposition + "  " + rule.Title);
        }

        return Task.FromResult(0);
    }

    private static Task<int> ExplainAsync(
        CommandLine commandLine,
        TextWriter output,
        TextWriter error)
    {
        if (commandLine.RuleId is not Presence<string>.Present requested)
        {
            error.WriteLine("explain requires a rule ID.");
            return Task.FromResult(64);
        }

        if (RuleCatalogue.Find(requested.Value) is not
            Presence<RuleRecord>.Present found)
        {
            error.WriteLine("Unknown rule ID: " + requested.Value);
            return Task.FromResult(64);
        }

        var rule = found.Value;
        output.WriteLine(rule.Id + " — " + rule.Title);
        output.WriteLine("Status: " + rule.Status);
        output.WriteLine("Certainty: " + rule.Certainty);
        output.WriteLine("Disposition: " + rule.Disposition);
        output.WriteLine("Evidence: " + rule.RequiredEvidence);
        output.WriteLine("Mechanism: " + rule.Mechanism);
        output.WriteLine("Suppression: " + rule.SuppressionPolicy);
        output.WriteLine(
            "Documentation: " + RuleCatalogue.DocumentationUrl(rule));
        return Task.FromResult(0);
    }

    private static async Task<int> AssayAsync(
        CommandLine commandLine,
        bool authoritative,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (commandLine.Input is not Presence<string>.Present input)
        {
            await error.WriteLineAsync(
                    commandLine.Command + " requires a project or solution path.")
                .ConfigureAwait(false);
            return 64;
        }

        VerificationResult result;
        try
        {
            result = await VerificationEngine.VerifyAsync(
                new VerificationRequest(
                    input.Value,
                    commandLine.PolicyPath,
                    authoritative,
                    ExecuteTests: authoritative,
                    commandLine.Profile),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("CSharpAssay was cancelled.").ConfigureAwait(false);
            return 3;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(
                "CSharpAssay verification failed: " + exception.Message)
                .ConfigureAwait(false);
            return 3;
        }

        ConsoleReporter.Write(output, result.Verdict);
        try
        {
            if (commandLine.JsonPath is Presence<string>.Present jsonPath)
            {
                await JsonEvidenceWriter.WriteAsync(
                    jsonPath.Value,
                    result.Verdict,
                    cancellationToken).ConfigureAwait(false);
            }

            if (commandLine.SarifPath is Presence<string>.Present sarifPath)
            {
                await SarifWriter.WriteAsync(
                    sarifPath.Value,
                    result.Verdict,
                    cancellationToken).ConfigureAwait(false);
            }

            if (commandLine.HtmlPath is Presence<string>.Present htmlPath)
            {
                var directory = Path.GetDirectoryName(
                    Path.GetFullPath(htmlPath.Value));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(
                    htmlPath.Value,
                    HtmlWriter.Write(result.Verdict),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException &&
            (exception is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException))
        {
            await error.WriteLineAsync(
                "CSharpAssay artifact write failed: " + exception.Message)
                .ConfigureAwait(false);
            return 3;
        }

        return result.Verdict.ExitCode;
    }

    private static async Task<int> MigrateAsync(
        CommandLine commandLine,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!commandLine.Report ||
            commandLine.Input is not Presence<string>.Present input)
        {
            await error.WriteLineAsync(
                "Usage: cs-assay migrate --report <project-or-solution> [--json path]")
                .ConfigureAwait(false);
            return 64;
        }

        if (commandLine.JsonPath is Presence<string>.Present requestedPath &&
            !string.Equals(
                Path.GetExtension(requestedPath.Value),
                ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync(
                "migrate --json requires a .json report path.")
                .ConfigureAwait(false);
            return 64;
        }

        MigrationReport report;
        try
        {
            report = await MigrationInventory
                .AnalyzeAsync(input.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync(
                "CSharpAssay migration reporting was cancelled.")
                .ConfigureAwait(false);
            return 3;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(
                "CSharpAssay migration reporting failed: " + exception.Message)
                .ConfigureAwait(false);
            return 3;
        }

        output.WriteLine(
            "Migration report: " + report.Exposures.Length +
            " public OneOf/ValueOf exposures across " +
            report.Sources.Length +
            " source files; report-only analysis made no source change.");
        foreach (var exposure in report.Exposures)
        {
            output.WriteLine(
                exposure.Location.Path + ":" + exposure.Location.StartLine + " " +
                exposure.Representation + " " + exposure.ApiRole + " " +
                exposure.Api);
            output.WriteLine(
                "  evidence: " + string.Join(" | ", exposure.Evidence));
            foreach (var risk in exposure.Risks)
            {
                output.WriteLine("  risk " + risk.Id + ": " + risk.Statement);
            }

            output.WriteLine(
                "  comparison: " + exposure.Comparison.Decision);
            foreach (var adapter in exposure.AdapterAssessments)
            {
                output.WriteLine(
                    "  adapter " + adapter.Adapter + ": " + adapter.Status);
            }

            foreach (var recommendation in exposure.Recommendations)
            {
                output.WriteLine(
                    "  recommendation " + recommendation.Id + ": " +
                    recommendation.Statement);
            }
        }

        if (commandLine.JsonPath is Presence<string>.Present jsonPath)
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    report,
                    MigrationJsonOptions) + "\n";
                var directory = Path.GetDirectoryName(
                    Path.GetFullPath(jsonPath.Value));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(
                    jsonPath.Value,
                    json,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException &&
                (exception is IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException))
            {
                await error.WriteLineAsync(
                    "CSharpAssay migration report write failed: " +
                    exception.Message).ConfigureAwait(false);
                return 3;
            }
        }

        return report.Failures.IsDefaultOrEmpty ? 0 : 3;
    }

    private static async Task<int> UnknownCommandAsync(
        string command,
        TextWriter error)
    {
        await error.WriteLineAsync("Unknown command: " + command).ConfigureAwait(false);
        await WriteHelpAsync(error).ConfigureAwait(false);
        return 64;
    }

    private static Task WriteHelpAsync(TextWriter output) =>
        output.WriteAsync(
            """
            CSharpAssay — deterministic functional-first C# verification

            Usage:
              cs-assay doctor
              cs-assay catalog [--profile compat|native]
              cs-assay explain <rule-id>
              cs-assay check <project-or-solution> [options]
              cs-assay verify <project-or-solution> [options]
              cs-assay migrate --report <project-or-solution> [--json path]

            Options:
              --report          Required marker for read-only migration inventory
              --policy <path>   Explicit .csassay.json
              --profile <name>  auto, compat, or native
              --json <path>     Deterministic JSON evidence
              --sarif <path>    SARIF 2.1.0 results
              --html <path>     Static HTML projection

            Command authority:
              check   Provisional analysis; configured release tests are not run
              verify  Authoritative all-TFM analysis; configured release tests run

            Verdict exit codes:
              0 Pass, 1 Fail, 2 Inconclusive, 3 ToolFailure
            """);
}
