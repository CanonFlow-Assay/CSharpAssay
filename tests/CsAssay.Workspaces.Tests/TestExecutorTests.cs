using CsAssay.Domain;

namespace CsAssay.Workspaces.Tests;

public sealed class TestExecutorTests
{
    [Fact]
    public void Parses_stable_test_counts_from_trx()
    {
        var report = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                report,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
                         id="random"
                         runUser="host\user">
                  <Times creation="2026-07-31T01:02:03Z" />
                  <Results>
                    <UnitTestResult testName="One" executionId="one" outcome="Passed" />
                    <UnitTestResult testName="Two" executionId="two" outcome="Passed" />
                    <UnitTestResult testName="Three" executionId="three" outcome="Passed" />
                    <UnitTestResult testName="Four" executionId="four" outcome="Failed" />
                    <UnitTestResult testName="Five" executionId="five" outcome="NotExecuted" />
                  </Results>
                  <ResultSummary outcome="Completed">
                    <Counters total="5"
                              executed="5"
                              passed="3"
                              failed="1"
                              error="0"
                              timeout="0"
                              aborted="0"
                              inconclusive="0"
                              passedButRunAborted="0"
                              notRunnable="0"
                              notExecuted="1"
                              disconnected="0"
                              warning="0"
                              completed="5"
                              inProgress="0"
                              pending="0" />
                  </ResultSummary>
                </TestRun>
                """);
            var requirement = new TestRequirement(
                "tests/Acme.Tests/Acme.Tests.csproj",
                "Release",
                NoBuild: true,
                MinimumExpectedTests: 5);

            var evidence = TestExecutor.ParseTrx(
                report,
                requirement,
                exitCode: 1);

            Assert.Equal(TestRunOutcome.Failed, evidence.Outcome);
            Assert.Equal(5, evidence.Total);
            Assert.Equal(3, evidence.Passed);
            Assert.Equal(1, evidence.Failed);
            Assert.Equal(1, evidence.Skipped);
            Assert.Equal(requirement.Input, evidence.Input);
        }
        finally
        {
            File.Delete(report);
        }
    }
    [Fact]
    public void Preserves_only_failed_names_and_structured_assertion_messages()
    {
        var evidence = ParseDetails("""
            <UnitTestResult testName="Boundary(amount: 25)" executionId="1" outcome="Failed">
              <Output><ErrorInfo><Message>Expected: Approve
            Actual: MoreInfo</Message><StackTrace>private stack</StackTrace></ErrorInfo><StdOut>unrelated output</StdOut></Output>
            </UnitTestResult>
            <UnitTestResult testName="NoMessage" executionId="2" outcome="Failed" />
            <UnitTestResult testName="Passing" executionId="3" outcome="Passed"><Output><ErrorInfo><Message>not a failed assertion</Message></ErrorInfo></Output></UnitTestResult>
            """, 3, 1, 2);
        Assert.Equal(2, evidence.Failed);
        Assert.Collection(evidence.FailureDetails,
            first => { Assert.Equal("Boundary(amount: 25)", first.TestName); Assert.Equal("Expected: Approve\nActual: MoreInfo", first.Message); },
            second => { Assert.Equal("NoMessage", second.TestName); Assert.Empty(second.Message); });
        Assert.False(evidence.FailureDetailsTruncated);
        Assert.DoesNotContain(evidence.FailureDetails, d => d.Message.Contains("private", StringComparison.Ordinal) || d.Message.Contains("unrelated", StringComparison.Ordinal));
    }

    [Fact]
    public void Detail_bounds_do_not_change_complete_counts_or_split_surrogate_pairs()
    {
        var name = new string('n', 1023) + "\U0001F600";
        var message = new string('m', 4095) + "\U0001F600";
        var results = string.Concat(Enumerable.Range(0, 21).Select(i =>
            $"<UnitTestResult testName=\"{name}\" executionId=\"{i}\" outcome=\"Failed\"><Output><ErrorInfo><Message>{message}</Message></ErrorInfo></Output></UnitTestResult>"));
        var evidence = ParseDetails(results, 21, 0, 21);
        Assert.Equal(21, evidence.Total); Assert.Equal(21, evidence.Failed);
        Assert.Equal(20, evidence.FailureDetails.Length); Assert.True(evidence.FailureDetailsTruncated);
        Assert.All(evidence.FailureDetails, d => { Assert.Equal(1023, d.TestName.Length); Assert.Equal(4095, d.Message.Length); });
    }

    private static TestRunEvidence ParseDetails(string results, int total, int passed, int failed)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"""
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" runUser="private-user">
                  <Results>{results}</Results>
                  <ResultSummary><Counters total="{total}" passed="{passed}" failed="{failed}" notExecuted="0" /></ResultSummary>
                </TestRun>
                """);
            return TestExecutor.ParseTrx(path, new("Tests.csproj", "Release", false, total), 1);
        }
        finally { File.Delete(path); }
    }

}
