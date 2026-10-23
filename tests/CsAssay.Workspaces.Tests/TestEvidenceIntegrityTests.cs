using CsAssay.Domain;

namespace CsAssay.Workspaces.Tests;

public sealed class TestEvidenceIntegrityTests
{
    private static readonly TestRequirement Requirement = new("tests/Fixture.csproj", "Release", true, 1);

    [Theory]
    [InlineData("absent-results")]
    [InlineData("contradictory-counts")]
    [InlineData("contradictory-outcome")]
    [InlineData("duplicate-execution")]
    [InlineData("runner-error")]
    [InlineData("successful-exit-with-failure")]
    public void InvalidEvidenceCannotPass(string corruption)
    {
        var results = "<UnitTestResult testName=\"Fixture.One\" executionId=\"one\" outcome=\"Passed\"/>";
        var total = 1;
        var passed = 1;
        var failed = 0;
        var error = 0;
        switch (corruption)
        {
            case "absent-results": results = ""; break;
            case "contradictory-counts": total = 2; break;
            case "contradictory-outcome": results = results.Replace("Passed", "Failed", StringComparison.Ordinal); break;
            case "duplicate-execution": results += results; total = passed = 2; break;
            case "runner-error": error = 1; break;
            case "successful-exit-with-failure": results = results.Replace("Passed", "Failed", StringComparison.Ordinal); passed = 0; failed = 1; break;
        }
        WithReport(results, total, passed, failed, 0, error, path =>
            Assert.Throws<InvalidDataException>(() => TestExecutor.ParseTrx(path, Requirement, 0)));
    }

    [Fact]
    public void SkippedSelectionCannotPass()
    {
        WithReport("<UnitTestResult testName=\"Fixture.One\" executionId=\"one\" outcome=\"NotExecuted\"/>", 1, 0, 0, 1, 0,
            path => Assert.Equal(TestRunOutcome.NotRun, TestExecutor.ParseTrx(path, Requirement, 0).Outcome));
    }

    [Fact]
    public void EmptySelectionCannotPass()
    {
        WithReport("", 0, 0, 0, 0, 0,
            path => Assert.Equal(TestRunOutcome.NotRun, TestExecutor.ParseTrx(path, Requirement, 0).Outcome));
    }

    private static void WithReport(string results, int total, int passed, int failed, int skipped, int error, Action<string> assertion)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>{results}</Results><ResultSummary><Counters total=\"{total}\" passed=\"{passed}\" failed=\"{failed}\" notExecuted=\"{skipped}\" error=\"{error}\"/></ResultSummary></TestRun>");
            assertion(path);
        }
        finally { File.Delete(path); }
    }
}
