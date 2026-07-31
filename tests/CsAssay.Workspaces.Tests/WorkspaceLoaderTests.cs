namespace CsAssay.Workspaces.Tests;

public sealed class WorkspaceLoaderTests
{
    [Fact]
    public async Task Loads_project_and_enumerates_target_framework()
    {
        var root = RepositoryRoot.Find();
        var project = Path.Combine(
            root,
            "src",
            "CsAssay.Domain",
            "CsAssay.Domain.csproj");

        var result = await WorkspaceLoader.LoadAsync(
            project,
            TestContext.Current.CancellationToken);

        Assert.Empty(
            result.Messages.Where(message =>
                string.Equals(
                    message.Kind,
                    "Failure",
                    StringComparison.OrdinalIgnoreCase)));
        var compilation = Assert.Single(result.Compilations);
        Assert.Equal("netstandard2.0", compilation.TargetFramework);
        Assert.NotEmpty(compilation.Compilation.SyntaxTrees);
    }
}

internal static class RepositoryRoot
{
    public static string Find()
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
