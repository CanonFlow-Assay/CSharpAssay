namespace CsAssay.Runner.Tests;

public sealed class CommandExitTests
{
    [Fact]
    public async Task Check_and_verify_expose_all_four_exit_codes()
    {
        var root = FindRoot();

        Assert.Equal(
            0,
            await RunAsync(
                "check",
                Fixture(root, "BoundaryScope")));
        Assert.Equal(
            1,
            await RunAsync(
                "verify",
                Fixture(root, "BlockingFinding")));
        Assert.Equal(
            2,
            await RunAsync(
                "verify",
                Fixture(root, "CompilerError")));
        Assert.Equal(
            3,
            await RunAsync(
                "verify",
                Path.Combine(root, "does-not-exist.csproj")));
    }

    [Fact]
    public async Task Artifact_write_failure_returns_tool_failure_exit()
    {
        var root = FindRoot();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CommandApp.RunAsync(
            [
                "check",
                Fixture(root, "BoundaryScope"),
                "--json",
                root
            ],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, exitCode);
        Assert.Contains(
            "artifact write failed",
            error.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> RunAsync(
        string command,
        string input)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        return await CommandApp.RunAsync(
            [command, input],
            output,
            error,
            TestContext.Current.CancellationToken);
    }

    private static string Fixture(string root, string name) =>
        Path.Combine(
            root,
            "specimens",
            "Projects",
            name,
            name + ".csproj");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CSharpAssay.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate CSharpAssay.slnx.");
    }
}
