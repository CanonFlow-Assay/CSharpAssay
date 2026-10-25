using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class ConsoleReporter
{
    public static void Write(TextWriter output, AssayVerdict verdict, ConsoleFormat format = ConsoleFormat.Msbuild, bool showSuppressed = false)
    {
        if (format == ConsoleFormat.Text)
        {
            output.WriteLine("CSharpAssay " + verdict.Evidence.ToolVersion + " — " + DiagnosticPresentation.Headline(verdict) +
                (verdict.Evidence.IsAuthoritative ? " (authoritative)" : " (provisional)"));
            output.WriteLine("Projects: " + verdict.Evidence.Projects.Length + "  Findings: " + verdict.Evidence.Findings.Length +
                "  Tests: " + verdict.Evidence.Tests.Sum(test => test.Total) + "  Missing: " + verdict.Evidence.Missing.Length +
                "  Failures: " + verdict.Evidence.Failures.Length);
        }

        foreach (var item in DiagnosticPresentation.Findings(verdict).Where(item => !item.AcceptedSuppression || showSuppressed))
        {
            var finding = item.Finding;
            var location = finding.Location;
            var validSource = !string.IsNullOrWhiteSpace(location.Path) && location.StartLine > 0 && location.StartColumn > 0;
            var path = DiagnosticPresentation.Sanitize(location.Path);
            var prefix = format == ConsoleFormat.Text
                ? (validSource ? path + ":" + location.StartLine + ":" + location.StartColumn + " " : "")
                : validSource ? path + "(" + location.StartLine + "," + location.StartColumn + "): " : "cs-assay : ";
            output.WriteLine(prefix + (format == ConsoleFormat.Text ? "" : item.Level + " ") + finding.RuleId +
                (format == ConsoleFormat.Text ? " " : ": ") + item.Message + (item.AcceptedSuppression ? " [accepted suppression]" : ""));
        }

        foreach (var diagnostic in DiagnosticPresentation.ExecutionDiagnostics(verdict))
        {
            output.WriteLine(format == ConsoleFormat.Text
                ? diagnostic.Kind + " " + diagnostic.Code + ": " + diagnostic.Message
                : "cs-assay : error " + DiagnosticPresentation.Sanitize(diagnostic.Code) + ": " + diagnostic.Message);
        }

        if (format == ConsoleFormat.Text)
        {
            foreach (var test in verdict.Evidence.Tests)
            {
                output.WriteLine("test " + test.Outcome + " " + DiagnosticPresentation.Sanitize(test.Input) + " (" + test.Passed +
                    " passed, " + test.Failed + " failed, " + test.Skipped + " skipped)");
            }
        }
        output.WriteLine(DiagnosticPresentation.Summary(verdict));
    }
}
