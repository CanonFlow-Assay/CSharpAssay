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
}
