using CsAssay.Runner;

namespace CsAssay.Runner.Tests;

public sealed class MigrationCommandTests
{
    [Fact]
    public void Migration_command_accepts_only_report_and_json()
    {
        var command = CommandLine.Parse(
        [
            "migrate",
            "--report",
            "Sample.csproj",
            "--json",
            "migration.json"
        ]);

        Assert.True(command.Report);
        Assert.Throws<ArgumentException>(() =>
            CommandLine.Parse(
            [
                "migrate",
                "--report",
                "Sample.csproj",
                "--sarif",
                "migration.sarif"
            ]));
        Assert.Throws<ArgumentException>(() =>
            CommandLine.Parse(
            [
                "migrate",
                "--report",
                "Sample.csproj",
                "--profile",
                "native"
            ]));
    }

    [Fact]
    public async Task Migration_json_is_deterministic_and_source_is_unchanged()
    {
        var root = FindRoot();
        var fixture = Fixture(root);
        var source = Path.Combine(
            Path.GetDirectoryName(fixture) is string directory
                ? directory
                : throw new InvalidOperationException(
                    "Migration fixture has no directory."),
            "PublicApi.cs");
        var sourceBefore = File.ReadAllText(source);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "csassay-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var firstPath = Path.Combine(temporaryDirectory, "first.json");
            var secondPath = Path.Combine(temporaryDirectory, "second.json");
            Assert.Equal(0, await RunAsync(fixture, firstPath));
            Assert.Equal(0, await RunAsync(fixture, secondPath));
            Assert.Equal(
                File.ReadAllText(firstPath),
                File.ReadAllText(secondPath));
            Assert.Equal(sourceBefore, File.ReadAllText(source));
            Assert.Contains(
                "\"mode\": \"report-only\"",
                File.ReadAllText(firstPath),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Migration_rejects_non_json_output_and_reports_failures()
    {
        var root = FindRoot();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var invalidOutputExit = await CommandApp.RunAsync(
            [
                "migrate",
                "--report",
                Fixture(root),
                "--json",
                "source.cs"
            ],
            output,
            error,
            TestContext.Current.CancellationToken);
        Assert.Equal(64, invalidOutputExit);
        Assert.Contains(".json", error.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        var missingInputExit = await CommandApp.RunAsync(
            [
                "migrate",
                "--report",
                Path.Combine(root, "missing-migration.csproj")
            ],
            output,
            error,
            TestContext.Current.CancellationToken);
        Assert.Equal(3, missingInputExit);
        Assert.Contains(
            "no source change",
            output.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Migration_artifact_write_failure_returns_tool_failure_exit()
    {
        var root = FindRoot();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "cs-assay-migration-write-" + Guid.NewGuid().ToString("N"));
        var directoryWithJsonExtension = Path.Combine(
            temporaryDirectory,
            "report.json");
        Directory.CreateDirectory(directoryWithJsonExtension);

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = await CommandApp.RunAsync(
                [
                    "migrate",
                    "--report",
                    Fixture(root),
                    "--json",
                    directoryWithJsonExtension
                ],
                output,
                error,
                TestContext.Current.CancellationToken);

            Assert.Equal(3, exitCode);
            Assert.Contains(
                "report write failed",
                error.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static async Task<int> RunAsync(string fixture, string reportPath)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CommandApp.RunAsync(
            ["migrate", "--report", fixture, "--json", reportPath],
            output,
            error,
            TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains(
            "report-only analysis made no source change",
            output.ToString(),
            StringComparison.Ordinal);
        return exitCode;
    }

    private static string Fixture(string root) =>
        Path.Combine(
            root,
            "specimens",
            "Projects",
            "MigrationSurface",
            "MigrationSurface.csproj");

    private static string FindRoot() =>
        FindRoot(new DirectoryInfo(AppContext.BaseDirectory));

    private static string FindRoot(DirectoryInfo directory)
    {
        if (File.Exists(Path.Combine(directory.FullName, "CSharpAssay.slnx")))
        {
            return directory.FullName;
        }

        return directory.Parent is DirectoryInfo parent
            ? FindRoot(parent)
            : throw new InvalidOperationException(
                "Could not locate CSharpAssay.slnx.");
    }
}
