using CsAssay.Domain;

namespace CsAssay.Workspaces.Tests;

public sealed class TestExecutorTests
{
    [Fact]
    public void Parses_only_stable_test_counts_from_trx()
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
}
