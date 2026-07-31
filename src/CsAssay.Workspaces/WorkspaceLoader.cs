using System.Collections.Immutable;
using CsAssay.Domain;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace CsAssay.Workspaces;

public sealed record WorkspaceCompilation(
    string Name,
    string ProjectPath,
    string TargetFramework,
    CSharpCompilation Compilation,
    ImmutableArray<string> DocumentPaths);

public sealed record WorkspaceMessage(
    string Kind,
    string Message,
    Presence<string> ProjectPath,
    Presence<string> TargetFramework,
    bool AffectsCompleteness);

public sealed record WorkspaceLoadResult(
    ImmutableArray<WorkspaceCompilation> Compilations,
    ImmutableArray<WorkspaceMessage> Messages,
    string SdkVersion,
    string MsBuildVersion);

public static class WorkspaceLoader
{
    private static readonly object RegistrationGate = new();
    private static Presence<VisualStudioInstance> registeredInstance =
        Presence.Missing<VisualStudioInstance>();

    public static async Task<WorkspaceLoadResult> LoadAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullInputPath))
        {
            return new WorkspaceLoadResult(
                ImmutableArray<WorkspaceCompilation>.Empty,
                ImmutableArray.Create(new WorkspaceMessage(
                    "Failure",
                    "Input does not exist: " + fullInputPath,
                    Presence.Missing<string>(),
                    Presence.Missing<string>(),
                    AffectsCompleteness: true)),
                string.Empty,
                string.Empty);
        }

        var instance = RegisterMsBuild();
        var messages = ImmutableArray.CreateBuilder<WorkspaceMessage>();
        var projectPaths = await DiscoverProjectsAsync(
            fullInputPath,
            messages,
            cancellationToken).ConfigureAwait(false);
        var compilations = ImmutableArray.CreateBuilder<WorkspaceCompilation>();

        foreach (var projectPath in projectPaths.OrderBy(
                     path => path,
                     StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetFrameworks = ReadTargetFrameworks(projectPath, messages);
            if (targetFrameworks.IsDefaultOrEmpty)
            {
                targetFrameworks = [string.Empty];
            }

            var requiresTargetFrameworkSelection = targetFrameworks.Length > 1;
            foreach (var targetFramework in targetFrameworks)
            {
                var unit = await LoadProjectAsync(
                    projectPath,
                    targetFramework,
                    requiresTargetFrameworkSelection,
                    messages,
                    cancellationToken).ConfigureAwait(false);
                if (unit is Presence<WorkspaceCompilation>.Present loaded)
                {
                    compilations.Add(loaded.Value);
                }
            }
        }

        var loadedProjectPaths = compilations
            .Select(compilation => compilation.ProjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();
        var classifiedMessages = messages
            .Select(message => ClassifyMessage(
                message,
                projectPaths,
                loadedProjectPaths))
            .ToImmutableArray();
        return new WorkspaceLoadResult(
            compilations
                .OrderBy(unit => unit.ProjectPath, StringComparer.Ordinal)
                .ThenBy(unit => unit.TargetFramework, StringComparer.Ordinal)
                .ToImmutableArray(),
            classifiedMessages
                .OrderBy(
                    message => PresenceText(message.ProjectPath),
                    StringComparer.Ordinal)
                .ThenBy(
                    message => PresenceText(message.TargetFramework),
                    StringComparer.Ordinal)
                .ThenBy(message => message.Kind, StringComparer.Ordinal)
                .ThenBy(message => message.Message, StringComparer.Ordinal)
                .ToImmutableArray(),
            instance.Version.ToString(),
            instance.Version.ToString());
    }

    public static VisualStudioInstance RegisterMsBuild()
    {
        lock (RegistrationGate)
        {
            if (registeredInstance is Presence<VisualStudioInstance>.Present current)
            {
                return current.Value;
            }

            if (MSBuildLocator.IsRegistered)
            {
                throw new InvalidOperationException(
                    "MSBuild was registered outside CSharpAssay; its identity cannot be proven.");
            }

            var instances = MSBuildLocator.QueryVisualStudioInstances()
                .OrderByDescending(instance => instance.Version)
                .ThenBy(instance => instance.MSBuildPath, StringComparer.Ordinal)
                .ToArray();
            var selected = instances.Length > 0
                ? instances[0]
                : MSBuildLocator.RegisterDefaults();
            registeredInstance = Presence.Of(selected);

            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterInstance(selected);
            }

            return selected;
        }
    }

    private static async Task<ImmutableArray<string>> DiscoverProjectsAsync(
        string inputPath,
        ImmutableArray<WorkspaceMessage>.Builder messages,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                Path.GetExtension(inputPath),
                ".csproj",
                StringComparison.OrdinalIgnoreCase))
        {
            return [inputPath];
        }

        if (!string.Equals(
                Path.GetExtension(inputPath),
                ".sln",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                Path.GetExtension(inputPath),
                ".slnx",
                StringComparison.OrdinalIgnoreCase))
        {
            messages.Add(new WorkspaceMessage(
                "Failure",
                "Input must be a .csproj, .sln, or .slnx file.",
                Presence.Of(inputPath),
                Presence.Missing<string>(),
                AffectsCompleteness: true));
            return ImmutableArray<string>.Empty;
        }

        using var workspace = CreateWorkspace(
            Presence.Missing<string>(),
            inputPath,
            messages);

        try
        {
            var solution = await workspace
                .OpenSolutionAsync(inputPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return solution.Projects
                .Where(project => string.Equals(
                    project.Language,
                    LanguageNames.CSharp,
                    StringComparison.Ordinal))
                .Select(project => project.FilePath)
                .OfType<string>()
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToImmutableArray();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            IOException or
            UnauthorizedAccessException)
        {
            messages.Add(new WorkspaceMessage(
                "Failure",
                exception.Message,
                Presence.Of(inputPath),
                Presence.Missing<string>(),
                AffectsCompleteness: true));
            return ImmutableArray<string>.Empty;
        }
    }

    private static async Task<Presence<WorkspaceCompilation>> LoadProjectAsync(
        string projectPath,
        string targetFramework,
        bool selectTargetFramework,
        ImmutableArray<WorkspaceMessage>.Builder messages,
        CancellationToken cancellationToken)
    {
        using var workspace = CreateWorkspace(
            selectTargetFramework && !string.IsNullOrEmpty(targetFramework)
                ? Presence.Of(targetFramework)
                : Presence.Missing<string>(),
            projectPath,
            messages);

        try
        {
            var project = await workspace
                .OpenProjectAsync(projectPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var rawCompilation = await project
                .GetCompilationAsync(cancellationToken)
                .ConfigureAwait(false);

            if (rawCompilation is not CSharpCompilation compilation)
            {
                messages.Add(new WorkspaceMessage(
                    "Failure",
                    rawCompilation is null
                        ? "C# compilation was unavailable."
                        : "Expected a C# compilation but received " +
                            rawCompilation.GetType().AssemblyQualifiedName + ".",
                    Presence.Of(projectPath),
                    Presence.Of(targetFramework),
                    AffectsCompleteness: true));
                return Presence.Missing<WorkspaceCompilation>();
            }

            return Presence.Of(new WorkspaceCompilation(
                project.Name,
                projectPath,
                string.IsNullOrEmpty(targetFramework) ? "unknown" : targetFramework,
                compilation,
                project.Documents
                    .Select(document => document.FilePath)
                    .OfType<string>()
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToImmutableArray()));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            IOException or
            UnauthorizedAccessException)
        {
            messages.Add(new WorkspaceMessage(
                "Failure",
                exception.Message,
                Presence.Of(projectPath),
                Presence.Of(targetFramework),
                AffectsCompleteness: true));
            return Presence.Missing<WorkspaceCompilation>();
        }
    }

    private static MSBuildWorkspace CreateWorkspace(
        Presence<string> targetFramework,
        string inputPath,
        ImmutableArray<WorkspaceMessage>.Builder messages)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        if (targetFramework is Presence<string>.Present framework)
        {
            properties["TargetFramework"] = framework.Value;
        }

        var workspace = MSBuildWorkspace.Create(properties);
        workspace.LoadMetadataForReferencedProjects = false;
        workspace.RegisterWorkspaceFailedHandler(eventArgs =>
            messages.Add(new WorkspaceMessage(
                eventArgs.Diagnostic.Kind.ToString(),
                eventArgs.Diagnostic.Message,
                Presence.Of(inputPath),
                targetFramework,
                AffectsCompleteness: true)));
        return workspace;
    }

    private static ImmutableArray<string> ReadTargetFrameworks(
        string projectPath,
        ImmutableArray<WorkspaceMessage>.Builder messages)
    {
        using var projectCollection = new ProjectCollection();
        try
        {
            var project = projectCollection.LoadProject(projectPath);
            var targetFrameworks = project.GetPropertyValue("TargetFrameworks");
            var evaluatedValue = string.IsNullOrWhiteSpace(targetFrameworks)
                ? project.GetPropertyValue("TargetFramework")
                : targetFrameworks;
            return evaluatedValue
                .Split(
                    [';'],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToImmutableArray();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            IOException or
            UnauthorizedAccessException)
        {
            messages.Add(new WorkspaceMessage(
                "Failure",
                "Could not evaluate target frameworks: " + exception.Message,
                Presence.Of(projectPath),
                Presence.Missing<string>(),
                AffectsCompleteness: true));
            return ImmutableArray<string>.Empty;
        }
        finally
        {
            projectCollection.UnloadAllProjects();
        }
    }

    private static string PresenceText(Presence<string> value) =>
        value is Presence<string>.Present present
            ? present.Value
            : string.Empty;

    private static WorkspaceMessage ClassifyMessage(
        WorkspaceMessage message,
        ImmutableArray<string> discoveredProjectPaths,
        ImmutableArray<string> loadedProjectPaths)
    {
        const string prefix =
            "Found project reference without a matching metadata reference: ";
        if (message.Message.StartsWith(prefix, StringComparison.Ordinal))
        {
            var referencedPath = Path.GetFullPath(
                message.Message.Substring(prefix.Length));
            var discovered = discoveredProjectPaths.Any(path => string.Equals(
                Path.GetFullPath(path),
                referencedPath,
                StringComparison.OrdinalIgnoreCase));
            return discovered
                ? message with
                {
                    Kind = "ProjectGraphInformation",
                    AffectsCompleteness = false
                }
                : message;
        }

        const string evaluationPrefix =
            "Msbuild failed when processing the file '";
        const string messageSeparator = "' with message: ";
        if (!message.Message.StartsWith(
                evaluationPrefix,
                StringComparison.OrdinalIgnoreCase) ||
            !message.Message.Contains(
                "has a known ",
                StringComparison.OrdinalIgnoreCase) ||
            !message.Message.Contains(
                " severity vulnerability, https://github.com/advisories/",
                StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        var separatorIndex = message.Message.IndexOf(
            messageSeparator,
            evaluationPrefix.Length,
            StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return message;
        }

        var evaluatedPath = message.Message.Substring(
            evaluationPrefix.Length,
            separatorIndex - evaluationPrefix.Length);
        var loaded = loadedProjectPaths.Any(path => string.Equals(
            Path.GetFullPath(path),
            Path.GetFullPath(evaluatedPath),
            StringComparison.OrdinalIgnoreCase));
        return loaded
            ? message with
            {
                Kind = "NuGetAuditInformation",
                AffectsCompleteness = false
            }
            : message;
    }
}
