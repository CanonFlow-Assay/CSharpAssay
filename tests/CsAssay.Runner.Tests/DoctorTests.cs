namespace CsAssay.Runner.Tests;

public sealed class DoctorTests
{
    [Fact] public async Task Supported_context_reports_separate_facts_without_application_authority()
    {
        using var fixture = new Context(); var probe = new Probe();
        var before = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var result = await fixture.Run(probe);
        Assert.Equal(0, result.Exit); Assert.Empty(result.Error);
        Assert.Contains("Tool: injected-tool", result.Output); Assert.Contains("Running runtime: injected .NET 11", result.Output);
        Assert.Contains("SDK selected in context: " + DoctorCommand.SupportedSdk, result.Output);
        Assert.Equal(Path.GetDirectoryName(fixture.Project), probe.Context);
        Assert.Contains("MSBuild selected process instance: injected instance", result.Output);
        Assert.Contains("capability observation", result.Output); Assert.Contains("selector matches are not established", result.Output);
        Assert.Contains("Declared core projects: MissingButNotLoaded.csproj", result.Output);
        Assert.Contains("Declared shell namespaces: Adapter", result.Output);
        Assert.EndsWith(DoctorCommand.Conclusion + Environment.NewLine, result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("lane: ready", result.Output, StringComparison.Ordinal);
        Assert.Equal(before, Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("sdk", "CSASSAY-DOCTOR-SDK-UNSUPPORTED")]
    [InlineData("absent-sdk", "CSASSAY-DOCTOR-SDK-UNAVAILABLE")]
    [InlineData("pin", "CSASSAY-DOCTOR-PIN-MISMATCH")]
    [InlineData("bad-pin", "CSASSAY-DOCTOR-PIN-INVALID")]
    [InlineData("missing-pin", "CSASSAY-DOCTOR-PIN-MISSING")]
    [InlineData("policy", "CSASSAY-CONFIG-INVALID")]
    [InlineData("missing-policy", "CSASSAY-CONFIG-MISSING")]
    [InlineData("runtime", "CSASSAY-DOCTOR-RUNTIME-UNSUPPORTED")]
    [InlineData("union", "CSASSAY-DOCTOR-UNION-UNAVAILABLE")]
    public async Task Prerequisite_failures_are_distinct_and_never_claim_verification(string failure, string code)
    {
        using var fixture = new Context(); var probe = new Probe();
        switch (failure)
        {
            case "sdk": probe.Facts = probe.Facts with { SelectedSdk = "10.0.999" }; break;
            case "absent-sdk": probe.Facts = probe.Facts with { SelectedSdk = "", SdkExitCode = 145, SdkDetail = "requested SDK is not installed" }; break;
            case "pin": File.WriteAllText(fixture.Pin, "{\"sdk\":{\"version\":\"10.0.999\",\"rollForward\":\"disable\",\"allowPrerelease\":true}}"); break;
            case "bad-pin": File.WriteAllText(fixture.Pin, "{"); break;
            case "missing-pin": File.Delete(fixture.Pin); break;
            case "policy": File.WriteAllText(fixture.Policy, "{"); break;
            case "missing-policy": File.Delete(fixture.Policy); break;
            case "runtime": probe.Facts = probe.Facts with { RuntimeMajor = 12 }; break;
            case "union": probe.Facts = probe.Facts with { UnionSymbols = false }; break;
        }
        var result = await fixture.Run(probe);
        Assert.Equal(2, result.Exit); Assert.Contains(code, result.Output); Assert.Empty(result.Error);
        Assert.EndsWith(DoctorCommand.Conclusion + Environment.NewLine, result.Output, StringComparison.Ordinal);
        if (failure == "sdk") Assert.Contains("Running runtime: injected .NET 11", result.Output);
    }

    [Fact] public async Task Missing_input_is_a_prerequisite_gap_without_invoking_host_probe()
    {
        using var fixture = new Context(); File.Delete(fixture.Project); var probe = new Probe();
        var result = await fixture.Run(probe);
        Assert.Equal(2, result.Exit); Assert.Contains("CSASSAY-DOCTOR-INPUT-MISSING", result.Output); Assert.Null(probe.Context);
    }

    [Fact] public async Task Execution_failure_is_exit_three_with_final_prerequisite_limit()
    {
        using var fixture = new Context(); var result = await fixture.Run(new Probe { Failure = true });
        Assert.Equal(3, result.Exit); Assert.Contains("CSASSAY-DOCTOR-EXECUTION", result.Error);
        Assert.EndsWith(DoctorCommand.Conclusion + Environment.NewLine, result.Output, StringComparison.Ordinal);
    }

    [Fact] public async Task No_input_form_and_explicit_policy_remain_contextual_prerequisite_checks()
    {
        using var fixture = new Context(); var probe = new Probe(); using var output = new StringWriter(); using var error = new StringWriter();
        var exit = await DoctorCommand.RunAsync(CommandLine.Parse(["doctor", "--policy", fixture.Policy]), probe, output, error,
            TestContext.Current.CancellationToken, currentDirectory: Path.GetDirectoryName(fixture.Project));
        Assert.Equal(0, exit); Assert.Equal(Path.GetDirectoryName(fixture.Project), probe.Context);
        Assert.Contains("Policy selected: " + fixture.Policy, output.ToString());
    }

    [Theory] [InlineData("--profile", "native")] [InlineData("--html", "x.html")] [InlineData("--json", "x.json")] [InlineData("--invent", "x")]
    public void Unsupported_doctor_options_remain_usage_errors(string option, string value) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["doctor", option, value]));

    private sealed class Probe : IDoctorProbe
    {
        public DoctorHostFacts Facts { get; set; } = new("injected-tool", "injected .NET 11", 11, DoctorCommand.SupportedSdk, 0, "", "injected instance", "5.9-injected", true);
        public string? Context { get; private set; }
        public bool Failure { get; init; }
        public Task<DoctorHostFacts> InspectAsync(string contextDirectory, CancellationToken cancellationToken)
        { Context = contextDirectory; return Failure ? throw new IOException("Controlled execution failure") : Task.FromResult(Facts); }
    }

    private sealed class Context : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("csassay-doctor-").FullName;
        public string Project => Path.Combine(Root, "nested", "Sample.csproj");
        public string Pin => Path.Combine(Root, "global.json");
        public string Policy => Path.Combine(Root, ".csassay.json");
        public Context()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Project)!);
            File.WriteAllText(Project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");
            File.WriteAllText(Pin, "{\"sdk\":{\"version\":\"" + DoctorCommand.SupportedSdk + "\",\"rollForward\":\"disable\",\"allowPrerelease\":true}}");
            File.WriteAllText(Policy, "{\"profile\":\"native\",\"boundaries\":{\"coreProjects\":[\"MissingButNotLoaded.csproj\"],\"shellNamespaces\":[\"Adapter\"]}}");
        }
        public async Task<(int Exit, string Output, string Error)> Run(Probe probe)
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            var exit = await DoctorCommand.RunAsync(CommandLine.Parse(["doctor", Project]), probe, output, error, TestContext.Current.CancellationToken);
            return (exit, output.ToString(), error.ToString());
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
