using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using CsAssay.Domain;

namespace CsAssay.Workspaces;

public sealed record TestExecutionResult(
    ImmutableArray<TestRunEvidence> Tests,
    ImmutableArray<MissingEvidence> Missing,
    ImmutableArray<EvaluationFailure> Failures);

public static class TestExecutor
{
    private const string TrxNamespace =
        "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static async Task<TestExecutionResult> ExecuteAsync(
        string rootPath,
        Presence<string> policyPath,
        ImmutableArray<TestRequirement> requirements,
        bool isAuthoritative,
        bool executeTests,
        bool prerequisitesComplete,
        CancellationToken cancellationToken)
    {
        var tests = ImmutableArray.CreateBuilder<TestRunEvidence>();
        var missing = ImmutableArray.CreateBuilder<MissingEvidence>();
        var failures = ImmutableArray.CreateBuilder<EvaluationFailure>();
        var policyDirectory = policyPath switch
        {
            Presence<string>.Present present =>
                Path.GetDirectoryName(present.Value) ?? rootPath,
            _ => rootPath
        };

        foreach (var requirement in requirements.OrderBy(
                     item => item.Input,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!isAuthoritative)
            {
                tests.Add(NotRun(requirement));
                continue;
            }

            if (!executeTests)
            {
                tests.Add(NotRun(requirement));
                missing.Add(new MissingEvidence(
                    "CSASSAY-REQUIRED-TESTS-NOT-RUN",
                    "Authoritative verification did not execute required tests.",
                    requirement.Input,
                    string.Empty));
                continue;
            }

            if (!prerequisitesComplete)
            {
                tests.Add(NotRun(requirement));
                continue;
            }

            var inputPath = Path.GetFullPath(
                Path.Combine(
                    policyDirectory,
                    requirement.Input.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            if (!File.Exists(inputPath))
            {
                tests.Add(NotRun(requirement));
                failures.Add(new EvaluationFailure(
                    "CSASSAY-TEST-INPUT-MISSING",
                    "Required test input does not exist: " + requirement.Input,
                    "test-runner",
                    Presence.Missing<string>()));
                continue;
            }

            var execution = await ExecuteOneAsync(
                requirement,
                inputPath,
                cancellationToken).ConfigureAwait(false);
            tests.Add(execution.Evidence);
            if (execution.Failure is
                Presence<EvaluationFailure>.Present failure)
            {
                failures.Add(failure.Value);
            }

            if (execution.Missing is Presence<MissingEvidence>.Present gap)
            {
                missing.Add(gap.Value);
            }
        }

        return new TestExecutionResult(
            tests.ToImmutable(),
            missing.ToImmutable(),
            failures.ToImmutable());
    }

    private static async Task<SingleTestExecution> ExecuteOneAsync(
        TestRequirement requirement,
        string inputPath,
        CancellationToken cancellationToken)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "csassay-tests-" + Guid.NewGuid().ToString("N"));
        var reportPath = Path.Combine(tempDirectory, "results.trx");
        Directory.CreateDirectory(tempDirectory);

        Presence<Process> process = Presence.Missing<Process>();
        try
        {
            var activeProcess = new Process
            {
                StartInfo = CreateStartInfo(
                    requirement,
                    inputPath,
                    tempDirectory)
            };
            process = Presence.Of(activeProcess);
            if (!activeProcess.Start())
            {
                return FailedExecution(
                    requirement,
                    "CSASSAY-TEST-RUNNER-START",
                    "The dotnet test process did not start.");
            }

            var standardOutput = activeProcess.StandardOutput.ReadToEndAsync(
                cancellationToken);
            var standardError = activeProcess.StandardError.ReadToEndAsync(
                cancellationToken);
            await activeProcess.WaitForExitAsync(cancellationToken)
                .ConfigureAwait(false);
            await Task.WhenAll(standardOutput, standardError)
                .ConfigureAwait(false);

            if (!File.Exists(reportPath))
            {
                return FailedExecution(
                    requirement,
                    "CSASSAY-TEST-EVIDENCE-MISSING",
                    "dotnet test produced no TRX evidence (exit " +
                        activeProcess.ExitCode.ToString(
                            CultureInfo.InvariantCulture) +
                        ").");
            }

            TestRunEvidence evidence;
            try
            {
                evidence = ParseTrx(
                    reportPath,
                    requirement,
                    activeProcess.ExitCode);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or
                XmlException or
                IOException or
                UnauthorizedAccessException)
            {
                return FailedExecution(
                    requirement,
                    "CSASSAY-TEST-EVIDENCE-INVALID",
                    exception.Message);
            }

            if (evidence.Failed == 0 && activeProcess.ExitCode != 0)
            {
                return new SingleTestExecution(
                    evidence with { Outcome = TestRunOutcome.NotRun },
                    Presence.Missing<MissingEvidence>(),
                    Presence.Of(new EvaluationFailure(
                        "CSASSAY-TEST-RUN-FAILED",
                        "dotnet test failed before reporting a failed test (exit " +
                            activeProcess.ExitCode.ToString(
                                CultureInfo.InvariantCulture) + ").",
                        requirement.Input,
                        Presence.Missing<string>())));
            }

            var incomplete = evidence.Total <
                requirement.MinimumExpectedTests
                ? Presence.Of(new MissingEvidence(
                    "CSASSAY-TEST-COUNT-INCOMPLETE",
                    "Required test evidence contains " +
                        evidence.Total.ToString(CultureInfo.InvariantCulture) +
                        " tests; policy requires at least " +
                        requirement.MinimumExpectedTests.ToString(
                            CultureInfo.InvariantCulture) + ".",
                    requirement.Input,
                    string.Empty))
                : Presence.Missing<MissingEvidence>();
            return new SingleTestExecution(
                evidence,
                incomplete,
                Presence.Missing<EvaluationFailure>());
        }
        catch (OperationCanceledException)
        {
            if (process is Presence<Process>.Present running)
            {
                _ = TryTerminate(running.Value);
            }

            throw;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException &&
            (exception is InvalidOperationException or
                System.ComponentModel.Win32Exception or
                IOException or
                UnauthorizedAccessException))
        {
            return FailedExecution(
                requirement,
                "CSASSAY-TEST-RUNNER-FAILURE",
                exception.Message);
        }
        finally
        {
            if (process is Presence<Process>.Present running)
            {
                running.Value.Dispose();
            }

            _ = TryDeleteDirectory(tempDirectory);
        }
    }

    public static TestRunEvidence ParseTrx(
        string reportPath,
        TestRequirement requirement,
        int exitCode)
    {
        var document = XDocument.Load(
            reportPath,
            LoadOptions.None);
        XNamespace trx = TrxNamespace;
        var counters = document
            .Root?
            .Element(trx + "ResultSummary")?
            .Element(trx + "Counters") ??
            throw new InvalidDataException(
                "TRX evidence has no ResultSummary/Counters element.");
        var total = ReadCounter(counters, "total");
        var passed = ReadCounter(counters, "passed");
        var failed = ReadCounter(counters, "failed");
        var skipped = ReadCounter(counters, "notExecuted");

        return new TestRunEvidence(
            requirement.Input,
            requirement.Configuration,
            Required: true,
            failed > 0
                ? TestRunOutcome.Failed
                : TestRunOutcome.Passed,
            exitCode,
            total,
            passed,
            failed,
            skipped);
    }

    private static ProcessStartInfo CreateStartInfo(
        TestRequirement requirement,
        string inputPath,
        string resultsDirectory)
    {
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        var startInfo = new ProcessStartInfo(
            string.IsNullOrWhiteSpace(dotnetHost) ? "dotnet" : dotnetHost)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(requirement.Configuration);
        if (requirement.NoBuild)
        {
            startInfo.ArgumentList.Add("--no-build");
        }

        startInfo.ArgumentList.Add("--results-directory");
        startInfo.ArgumentList.Add(resultsDirectory);
        startInfo.ArgumentList.Add("--report-xunit-trx");
        startInfo.ArgumentList.Add("--report-xunit-trx-filename");
        startInfo.ArgumentList.Add("results.trx");
        return startInfo;
    }

    private static int ReadCounter(XElement counters, string name)
    {
        var text = counters.Attribute(name)?.Value ??
            throw new InvalidDataException(
                "TRX evidence is missing the " + name + " counter.");
        return int.TryParse(
            text,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : throw new InvalidDataException(
                "TRX evidence has an invalid " + name + " counter.");
    }

    private static bool TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            System.ComponentModel.Win32Exception or
            NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static TestRunEvidence NotRun(TestRequirement requirement) => new(
        requirement.Input,
        requirement.Configuration,
        Required: true,
        TestRunOutcome.NotRun,
        ExitCode: -1,
        Total: 0,
        Passed: 0,
        Failed: 0,
        Skipped: 0);

    private static SingleTestExecution FailedExecution(
        TestRequirement requirement,
        string code,
        string message) => new(
        NotRun(requirement),
        Presence.Missing<MissingEvidence>(),
        Presence.Of(new EvaluationFailure(
            code,
            message,
            requirement.Input,
            Presence.Missing<string>())));

    private sealed record SingleTestExecution(
        TestRunEvidence Evidence,
        Presence<MissingEvidence> Missing,
        Presence<EvaluationFailure> Failure);
}
