using System.Diagnostics;

namespace CsAssay.Runner.Tests;

public sealed class QualificationExitTests
{
    [Fact]
    public async Task InvalidQualificationDataReturnsFailureInsteadOfAborting()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CSharpAssay.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var qualifier = Path.Combine(root.FullName, "eng/CsAssay.Qualification/bin/Release/net11.0/CsAssay.Qualification.dll");
        var scratch = Directory.CreateTempSubdirectory("assay-invalid-qualification-");
        try
        {
            File.WriteAllText(Path.Combine(scratch.FullName, "CSharpAssay.slnx"), "<Solution/>");
            var evidence = Directory.CreateDirectory(Path.Combine(scratch.FullName, "artifacts/csharp-tooling-qualified"));
            File.WriteAllText(Path.Combine(evidence.FullName, "qualification.json"), "{}");
            Assert.Equal(0, (await Run(scratch.FullName, "git", "init")).Code);
            Assert.Equal(0, (await Run(scratch.FullName, "git", "-c", "user.name=Assay Fixture", "-c", "user.email=fixture@localhost.invalid", "commit", "--allow-empty", "-m", "fixture")).Code);
            var (code, output) = await Run(scratch.FullName, "dotnet", qualifier, "copy-qualified", "--destination", Path.Combine(scratch.FullName, "feed"));
            Assert.Equal(2, code);
            Assert.Contains("exact clean qualified engine commit", output);
        }
        finally { scratch.Delete(true); }
    }

    private static async Task<(int Code, string Output)> Run(string cwd, string command, params string[] arguments)
    {
        using var process = new Process { StartInfo = new(command) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments) { process.StartInfo.ArgumentList.Add(argument); }
        process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) { process.Kill(true); } await process.WaitForExitAsync(CancellationToken.None); throw; }
        return (process.ExitCode, await stdout + await stderr);
    }
}
