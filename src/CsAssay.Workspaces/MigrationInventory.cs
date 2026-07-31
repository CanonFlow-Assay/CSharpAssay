using System.Collections.Immutable;
using CsAssay.Domain;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis;

namespace CsAssay.Workspaces;

public sealed record MigrationExposure(
    string Representation,
    string Api,
    string Project,
    string TargetFramework,
    SourceSpan Location,
    ImmutableArray<string> Risks);

public sealed record MigrationReport(
    string Input,
    ImmutableArray<MigrationExposure> Exposures,
    ImmutableArray<EvaluationFailure> Failures);

public static class MigrationInventory
{
    public static async Task<MigrationReport> AnalyzeAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        var rootPath = Path.GetDirectoryName(fullInputPath) is string directory
            ? directory
            : Directory.GetCurrentDirectory();
        var workspace = await WorkspaceLoader
            .LoadAsync(fullInputPath, cancellationToken)
            .ConfigureAwait(false);
        var exposures = ImmutableArray.CreateBuilder<MigrationExposure>();
        var failures = workspace.Messages
            .Where(message => string.Equals(
                message.Kind,
                "Failure",
                StringComparison.OrdinalIgnoreCase))
            .Select(message => new EvaluationFailure(
                "CSASSAY-MIGRATION-WORKSPACE",
                message.Message,
                message.ProjectPath is Presence<string>.Present project
                    ? project.Value
                    : "MSBuildWorkspace",
                Presence.Missing<string>()))
            .ToImmutableArray();

        foreach (var unit in workspace.Compilations)
        {
            foreach (var type in EnumerateTypes(
                         unit.Compilation.Assembly.GlobalNamespace))
            {
                if (!IsPublicApi(type))
                {
                    continue;
                }

                AddIfRecognized(type, type, unit, rootPath, exposures);
                foreach (var member in type.GetMembers().Where(IsPublicApi))
                {
                    switch (member)
                    {
                        case IMethodSymbol method:
                            AddIfRecognized(
                                method,
                                method.ReturnType,
                                unit,
                                rootPath,
                                exposures);
                            foreach (var parameter in method.Parameters)
                            {
                                AddIfRecognized(
                                    method,
                                    parameter.Type,
                                    unit,
                                    rootPath,
                                    exposures);
                            }

                            break;
                        case IPropertySymbol property:
                            AddIfRecognized(
                                property,
                                property.Type,
                                unit,
                                rootPath,
                                exposures);
                            break;
                        case IFieldSymbol field:
                            AddIfRecognized(
                                field,
                                field.Type,
                                unit,
                                rootPath,
                                exposures);
                            break;
                        case IEventSymbol @event:
                            AddIfRecognized(
                                @event,
                                @event.Type,
                                unit,
                                rootPath,
                                exposures);
                            break;
                    }
                }
            }
        }

        return new MigrationReport(
            Path.GetFileName(fullInputPath),
            exposures
                .Distinct()
                .OrderBy(exposure => exposure.Representation, StringComparer.Ordinal)
                .ThenBy(exposure => exposure.Api, StringComparer.Ordinal)
                .ThenBy(exposure => exposure.Project, StringComparer.Ordinal)
                .ThenBy(exposure => exposure.TargetFramework, StringComparer.Ordinal)
                .ThenBy(exposure => exposure.Location.Path, StringComparer.Ordinal)
                .ThenBy(exposure => exposure.Location.StartLine)
                .ToImmutableArray(),
            failures);
    }

    private static void AddIfRecognized(
        ISymbol api,
        ITypeSymbol type,
        WorkspaceCompilation unit,
        string rootPath,
        ImmutableArray<MigrationExposure>.Builder exposures)
    {
        if (Recognize(type) is not Presence<string>.Present representation)
        {
            return;
        }

        var location = api.Locations.FirstOrDefault(
            candidate => candidate.IsInSource) ?? Location.None;
        var span = DiagnosticProjection.ToSourceSpan(location);
        span = span with
        {
            Path = string.IsNullOrEmpty(span.Path)
                ? string.Empty
                : Fingerprints.NormalizePath(
                    Path.GetRelativePath(rootPath, Path.GetFullPath(span.Path)))
        };
        exposures.Add(new MigrationExposure(
            representation.Value,
            api.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            unit.Name,
            unit.TargetFramework,
            span,
            representation.Value == "OneOf"
                ?
                [
                    "Changing the public return or parameter type is an API break.",
                    "Alternative order may have leaked through Tn/AsTn members.",
                    "Native representation, boxing, and serialization require behavioral qualification."
                ]
                :
                [
                    "Class-to-struct migration changes reference and value semantics.",
                    "default(T), boxing, serializer, ORM, and generic constraints require qualification.",
                    "ValueOf validation is exception-based unless wrapped by a total factory."
                ]));
    }

    private static Presence<string> Recognize(ITypeSymbol type)
    {
        if (MetadataIdentity.IsOneOf(type) ||
            type is INamedTypeSymbol named &&
            named.TypeArguments.Any(argument => MetadataIdentity.IsOneOf(argument)))
        {
            return Presence.Of("OneOf");
        }

        Presence<ITypeSymbol> current = Presence.Of(type);
        while (current is Presence<ITypeSymbol>.Present found)
        {
            if (found.Value.OriginalDefinition.GetFullMetadataName()
                .StartsWith("ValueOf.ValueOf`", StringComparison.Ordinal))
            {
                return Presence.Of("ValueOf");
            }

            current = found.Value.BaseType is INamedTypeSymbol baseType
                ? Presence.Of<ITypeSymbol>(baseType)
                : Presence.Missing<ITypeSymbol>();
        }

        if (type is INamedTypeSymbol generic &&
            generic.TypeArguments.Any(argument =>
                Recognize(argument) is Presence<string>.Present
                {
                    Value: "ValueOf"
                }))
        {
            return Presence.Of("ValueOf");
        }

        return Presence.Missing<string>();
    }

    private static bool IsPublicApi(ISymbol symbol) =>
        symbol.DeclaredAccessibility is Accessibility.Public or
            Accessibility.Protected or
            Accessibility.ProtectedOrInternal;

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(
        INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol namespaceSymbol)
            {
                foreach (var nested in EnumerateTypes(namespaceSymbol))
                {
                    yield return nested;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in EnumerateTypes(type))
                {
                    yield return nested;
                }
            }
        }
    }
}
