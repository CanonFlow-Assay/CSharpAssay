using System.Text;
using CsAssay.Catalogue;
using CsAssay.Domain;
using CsAssay.Reporting;
using CsAssay.Workspaces;

namespace CsAssay.Runner;

public static class CommandApp
{
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
            await error.WriteLineAsync(exception.Message, CancellationToken.None).ConfigureAwait(false);
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
            "doctor" => await DoctorCommand.RunAsync(commandLine, new LocalDoctorProbe(), output, error, cancellationToken).ConfigureAwait(false),
            "catalog" => await CatalogAsync(output).ConfigureAwait(false),
            "explain" => await ExplainAsync(commandLine, output, error).ConfigureAwait(false),
            "check" => await AssayAsync(
                commandLine,
                args,
                authoritative: false,
                output,
                error,
                cancellationToken).ConfigureAwait(false),
            "verify" or "assess" => await AssayAsync(
                commandLine,
                args,
                authoritative: true,
                output,
                error,
                cancellationToken).ConfigureAwait(false),
            _ => await UnknownCommandAsync(commandLine.Command, error).ConfigureAwait(false)
        };
    }

    private static Task<int> CatalogAsync(TextWriter output)
    {
        var profile = EffectiveProfile.Native;

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
        if (rule.Status == RuleStatus.Prototype)
            output.WriteLine("Prototype/advisory: nonblocking. Catalogue disposition alone is not effective release severity.");
        output.WriteLine();
        output.WriteLine(RuleDocumentation.Read(rule));
        return Task.FromResult(0);
    }

    private static async Task<int> AssayAsync(
        CommandLine commandLine,
        string[] args,
        bool authoritative,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (commandLine.Input is not Presence<string>.Present input)
        {
            await error.WriteLineAsync(
                    commandLine.Command + " requires a project or solution path.", CancellationToken.None)
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
            await error.WriteLineAsync("CSharpAssay was cancelled.", CancellationToken.None).ConfigureAwait(false);
            output.WriteLine("cs-assay : error CSASSAY-CANCELLED: Analysis was interrupted.");
            output.WriteLine("Tool failed: analysis did not complete.");
            return 3;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(
                "CSharpAssay verification failed: " + DiagnosticPresentation.Sanitize(exception.Message), CancellationToken.None)
                .ConfigureAwait(false);
            output.WriteLine("cs-assay : error CSASSAY-ANALYSIS-FAILED: Analysis could not complete.");
            output.WriteLine("Tool failed: analysis did not complete.");
            return 3;
        }

        // Attach observed invocation context to the completed result; do not
        // recalculate the verdict or invent a source snapshot from the Git head.
        var verdict = result.Verdict with
        {
            Evidence = result.Verdict.Evidence with
            {
                Invocation = Presence.Of(new CommandEvidence(Environment.CurrentDirectory,
                    Environment.ProcessPath ?? string.Empty,
                    string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)
                        ? [typeof(CommandApp).Assembly.Location, .. args] : [.. args]))
            }
        };
        try
        {
            var reports = new List<RequestedReport>();
            var artifacts = new List<ReportArtifact>();
            if (commandLine.JsonPath is Presence<string>.Present j) artifacts.Add(new("JSON", j.Value));
            if (commandLine.SarifPath is Presence<string>.Present s) artifacts.Add(new("SARIF", s.Value));
            if (commandLine.HtmlPath is Presence<string>.Present h) artifacts.Add(new("HTML", h.Value));
            if (commandLine.MarkdownPath is Presence<string>.Present m) artifacts.Add(new("Markdown", m.Value));
            if (commandLine.JsonPath is Presence<string>.Present jsonPath)
            {
                reports.Add(new(jsonPath.Value, commandLine.Command == "assess"
                    ? AssessmentWriter.Write(verdict) : JsonEvidenceWriter.Write(verdict)));
            }
            if (commandLine.SarifPath is Presence<string>.Present sarifPath)
            {
                reports.Add(new(sarifPath.Value, SarifWriter.Write(verdict, Path.GetDirectoryName(Path.GetFullPath(input.Value)))));
            }
            if (commandLine.HtmlPath is Presence<string>.Present htmlPath)
            {
                reports.Add(new(htmlPath.Value, Encoding.UTF8.GetBytes(HtmlWriter.Write(verdict, [.. artifacts]))));
            }
            if (commandLine.MarkdownPath is Presence<string>.Present markdownPath)
                reports.Add(new(markdownPath.Value, Encoding.UTF8.GetBytes(MarkdownWriter.Write(verdict, [.. artifacts]))));
            await AtomicReportWriter.WriteAsync(reports, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("CSharpAssay report writing was cancelled.", CancellationToken.None).ConfigureAwait(false);
            output.WriteLine("cs-assay : error CSASSAY-CANCELLED: Requested report writing was interrupted.");
            output.WriteLine("Tool failed: requested outputs did not complete.");
            return 3;
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
        {
            await error.WriteLineAsync(
                "CSharpAssay artifact write failed: " + DiagnosticPresentation.Sanitize(exception.Message), CancellationToken.None)
                .ConfigureAwait(false);
            output.WriteLine("cs-assay : error CSASSAY-REPORT-WRITE: A requested report could not be written.");
            output.WriteLine("Tool failed: requested outputs did not complete.");
            return 3;
        }

        ConsoleReporter.Write(output, verdict, commandLine.Format, commandLine.ShowSuppressed);
        return verdict.ExitCode;
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
              cs-assay doctor [project-or-solution] [--policy path]
              cs-assay catalog [--profile native|native]
              cs-assay explain <rule-id>
              cs-assay check <project-or-solution> [options]
              cs-assay verify <project-or-solution> [options]
              cs-assay assess <project-or-solution> [options]

            Options:
              --format <name>  msbuild (default) or text compatibility layout
              --show-suppressed Detailed accepted suppression observations
              --policy <path>   Explicit .csassay.json
              --profile <name>  native (.NET 11 only)
              --json <path>     Deterministic JSON evidence
              --sarif <path>    SARIF 2.1.0 results
              --html <path>     Static HTML projection
              --markdown <path> Static Markdown summary

            Command authority:
              check   Provisional analysis; configured release tests are not run
              verify  Authoritative all-TFM analysis; configured release tests run
              assess  Same verification, with an additive assessment JSON envelope

            Verdict exit codes:
              0 Pass, 1 Fail, 2 Inconclusive, 3 ToolFailure
            """);
}
