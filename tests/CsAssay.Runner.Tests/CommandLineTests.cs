using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Runner.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void Parses_verify_options()
    {
        var command = CommandLine.Parse(
        [
            "verify",
            "Sample.slnx",
            "--profile",
            "compat",
            "--json",
            "assay.json",
            "--sarif",
            "assay.sarif"
        ]);

        Assert.Equal("verify", command.Command);
        Assert.Equal(
            new Presence<string>.Present("Sample.slnx"),
            command.Input);
        Assert.Equal(
            new Presence<AssayProfile>.Present(AssayProfile.Compat),
            command.Profile);
        Assert.Equal(
            new Presence<string>.Present("assay.json"),
            command.JsonPath);
        Assert.Equal(
            new Presence<string>.Present("assay.sarif"),
            command.SarifPath);
    }

    [Fact]
    public void Rejects_unknown_option()
    {
        Assert.Throws<ArgumentException>(() =>
            CommandLine.Parse(["verify", "Sample.csproj", "--invent"]));
    }

    [Fact]
    public void Rejects_duplicate_options()
    {
        Assert.Throws<ArgumentException>(() =>
            CommandLine.Parse(
            [
                "verify",
                "Sample.csproj",
                "--json",
                "one.json",
                "--json",
                "two.json"
            ]));
    }

    [Fact]
    public void Rejects_options_not_supported_by_command()
    {
        Assert.Throws<ArgumentException>(() =>
            CommandLine.Parse(["doctor", "--profile", "compat"]));
    }

    [Fact]
    public async Task Catalog_command_is_operational()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CommandApp.RunAsync(
            ["catalog", "--profile", "compat"],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("CSAN0001", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Help_aliases_render_once_on_stdout(string argument)
    {
        var expected = await RunAsync(["help"]);
        var actual = await RunAsync([argument]);

        Assert.Equal(0, actual.ExitCode);
        Assert.Equal(expected.Output, actual.Output);
        Assert.Equal(string.Empty, actual.Error);
        Assert.Equal(
            1,
            CountOccurrences(
                actual.Output,
                "CSharpAssay — deterministic functional-first C# verification"));
    }

    [Theory]
    [InlineData("CSAN0001")]
    [InlineData("CSAN0004")]
    public async Task Explain_uses_canonical_https_documentation_url(string ruleId)
    {
        var result = await RunAsync(["explain", ruleId]);
        var rule = Assert.IsType<Presence<RuleRecord>.Present>(
            RuleCatalogue.Find(ruleId)).Value;
        var expectedUrl = RuleCatalogue.DocumentationUrl(rule);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Error);
        Assert.Contains("Documentation: " + expectedUrl, result.Output);
        Assert.True(
            Uri.TryCreate(expectedUrl, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }

    [Fact]
    public async Task Unknown_command_retains_usage_error()
    {
        var result = await RunAsync(["invent"]);

        Assert.Equal(64, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains("Unknown command: invent", result.Error);
        Assert.Equal(
            1,
            CountOccurrences(
                result.Error,
                "CSharpAssay — deterministic functional-first C# verification"));
    }

    [Fact]
    public async Task Unknown_rule_retains_usage_error()
    {
        var result = await RunAsync(["explain", "UNKNOWN0000"]);

        Assert.Equal(64, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal(
            "Unknown rule ID: UNKNOWN0000" + Environment.NewLine,
            result.Error);
    }

    private static async Task<CommandResult> RunAsync(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CommandApp.RunAsync(
            args,
            output,
            error,
            TestContext.Current.CancellationToken);
        return new CommandResult(
            exitCode,
            output.ToString(),
            error.ToString());
    }

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(
                   expected,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += expected.Length;
        }

        return count;
    }

    private sealed record CommandResult(
        int ExitCode,
        string Output,
        string Error);
}
