using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class ConsoleReporter
{
    public static void Write(TextWriter output, AssayVerdict verdict)
    {
        var authority = verdict.Evidence.IsAuthoritative
            ? "authoritative"
            : "provisional";
        output.WriteLine(
            "CSharpAssay " + verdict.Evidence.ToolVersion + " — " +
            verdict.Kind + " (" + authority + ")");
        output.WriteLine(
            "Projects: " + verdict.Evidence.Projects.Length +
            "  Findings: " + verdict.Evidence.Findings.Length +
            "  Missing: " + verdict.Evidence.Missing.Length +
            "  Failures: " + verdict.Evidence.Failures.Length);

        foreach (var finding in verdict.Evidence.Findings.Take(50))
        {
            var location = string.IsNullOrEmpty(finding.Location.Path)
                ? string.Empty
                : finding.Location.Path + ":" + finding.Location.StartLine + ":" +
                    finding.Location.StartColumn + " ";
            var suppression = finding.Suppressed ? " [suppressed]" : string.Empty;
            output.WriteLine(
                location + finding.RuleId + " " + finding.Message + suppression);
        }

        if (verdict.Evidence.Findings.Length > 50)
        {
            output.WriteLine(
                "... " + (verdict.Evidence.Findings.Length - 50) +
                " additional findings are in the evidence artifact.");
        }

        foreach (var item in verdict.Evidence.Missing)
        {
            output.WriteLine("missing " + item.Code + ": " + item.Message);
        }

        foreach (var failure in verdict.Evidence.Failures)
        {
            output.WriteLine("failure " + failure.Code + ": " + failure.Message);
        }
    }
}
