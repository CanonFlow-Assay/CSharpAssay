using System.Collections.Immutable;
using System.Security.Cryptography;
using CsAssay.Domain;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis;

namespace CsAssay.Workspaces;

public sealed record MigrationRisk(
    string Id,
    string Statement,
    ImmutableArray<string> Evidence);

public sealed record MigrationRecommendation(
    string Id,
    string AffectedApi,
    string Statement,
    ImmutableArray<string> Evidence,
    ImmutableArray<string> RequiredValidation);

public sealed record MigrationBehaviorComparison(
    string CompatibilityRepresentation,
    string NativeCandidate,
    string Decision,
    ImmutableArray<string> BehaviorsToPreserve);

public sealed record MigrationAdapterAssessment(
    string Adapter,
    string Status,
    string Applicability,
    ImmutableArray<string> RequiredEvidence);

public sealed record MigrationExposure(
    string Representation,
    string Api,
    string ApiRole,
    string ExposedType,
    string MetadataIdentity,
    string AssemblyIdentity,
    string ApiAssemblyIdentity,
    string Project,
    string TargetFramework,
    SourceSpan Location,
    ImmutableArray<string> Evidence,
    ImmutableArray<MigrationRisk> Risks,
    MigrationBehaviorComparison Comparison,
    ImmutableArray<MigrationAdapterAssessment> AdapterAssessments,
    ImmutableArray<MigrationRecommendation> Recommendations);

public sealed record MigrationEcosystemAdapter(
    string Name,
    string Status,
    string Version,
    string Reason);

public sealed record MigrationReport(
    string SchemaVersion,
    string Mode,
    string Input,
    ImmutableArray<SourceEvidence> Sources,
    ImmutableArray<MigrationExposure> Exposures,
    ImmutableArray<MigrationEcosystemAdapter> EcosystemAdapters,
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
                    ? NormalizeRelativePath(rootPath, project.Value)
                    : "MSBuildWorkspace",
                Presence.Missing<string>()))
            .OrderBy(failure => failure.Component, StringComparer.Ordinal)
            .ThenBy(failure => failure.Message, StringComparer.Ordinal)
            .ToImmutableArray();

        foreach (var unit in workspace.Compilations)
        {
            foreach (var type in EnumerateTypes(
                         unit.Compilation.Assembly.GlobalNamespace))
            {
                if (!IsExternallyVisible(type))
                {
                    continue;
                }

                AddRecognizedTypes(
                    type,
                    "declared-type",
                    type,
                    unit,
                    rootPath,
                    exposures);
                if (type.BaseType is INamedTypeSymbol baseType)
                {
                    AddRecognizedTypes(
                        type,
                        "base-type",
                        baseType,
                        unit,
                        rootPath,
                        exposures);
                }

                foreach (var @interface in type.Interfaces)
                {
                    AddRecognizedTypes(
                        type,
                        "interface",
                        @interface,
                        unit,
                        rootPath,
                        exposures);
                }

                foreach (var typeParameter in type.TypeParameters)
                {
                    foreach (var constraint in typeParameter.ConstraintTypes)
                    {
                        AddRecognizedTypes(
                            type,
                            "constraint:" + typeParameter.Name,
                            constraint,
                            unit,
                            rootPath,
                            exposures,
                            typeParameter);
                    }
                }

                foreach (var member in type.GetMembers().Where(IsExternallyVisible))
                {
                    AddMember(member, unit, rootPath, exposures);
                }
            }
        }

        var orderedExposures = exposures
            .Distinct()
            .OrderBy(exposure => exposure.Representation, StringComparer.Ordinal)
            .ThenBy(exposure => exposure.Api, StringComparer.Ordinal)
            .ThenBy(exposure => exposure.ApiRole, StringComparer.Ordinal)
            .ThenBy(exposure => exposure.Project, StringComparer.Ordinal)
            .ThenBy(exposure => exposure.TargetFramework, StringComparer.Ordinal)
            .ThenBy(exposure => exposure.Location.Path, StringComparer.Ordinal)
            .ThenBy(exposure => exposure.Location.StartLine)
            .ThenBy(exposure => exposure.Location.StartColumn)
            .ToImmutableArray();
        return new MigrationReport(
            "1.0.0",
            "report-only",
            Path.GetFileName(fullInputPath),
            CaptureSources(workspace, rootPath),
            orderedExposures,
            MigrationGuidance.EcosystemAdapters(orderedExposures),
            failures);
    }

    private static void AddMember(
        ISymbol member,
        WorkspaceCompilation unit,
        string rootPath,
        ImmutableArray<MigrationExposure>.Builder exposures)
    {
        switch (member)
        {
            case IMethodSymbol method:
                if (method.AssociatedSymbol is ISymbol)
                {
                    break;
                }

                if (method.MethodKind is not MethodKind.Constructor and
                    not MethodKind.StaticConstructor)
                {
                    AddRecognizedTypes(
                        method,
                        "return",
                        method.ReturnType,
                        unit,
                        rootPath,
                        exposures);
                }

                foreach (var parameter in method.Parameters)
                {
                    AddRecognizedTypes(
                        method,
                        "parameter:" + parameter.Name,
                        parameter.Type,
                        unit,
                        rootPath,
                        exposures,
                        parameter);
                }

                foreach (var typeParameter in method.TypeParameters)
                {
                    foreach (var constraint in typeParameter.ConstraintTypes)
                    {
                        AddRecognizedTypes(
                            method,
                            "constraint:" + typeParameter.Name,
                            constraint,
                            unit,
                            rootPath,
                            exposures,
                            typeParameter);
                    }
                }

                break;
            case IPropertySymbol property:
                AddRecognizedTypes(
                    property,
                    "property",
                    property.Type,
                    unit,
                    rootPath,
                    exposures);
                foreach (var parameter in property.Parameters)
                {
                    AddRecognizedTypes(
                        property,
                        "parameter:" + parameter.Name,
                        parameter.Type,
                        unit,
                        rootPath,
                        exposures,
                        parameter);
                }

                break;
            case IFieldSymbol field:
                AddRecognizedTypes(
                    field,
                    "field",
                    field.Type,
                    unit,
                    rootPath,
                    exposures);
                break;
            case IEventSymbol @event:
                AddRecognizedTypes(
                    @event,
                    "event",
                    @event.Type,
                    unit,
                    rootPath,
                    exposures);
                break;
            case INamedTypeSymbol nestedType:
                AddRecognizedTypes(
                    nestedType,
                    "declared-type",
                    nestedType,
                    unit,
                    rootPath,
                    exposures);
                break;
        }
    }

    private static void AddRecognizedTypes(
        ISymbol api,
        string apiRole,
        ITypeSymbol type,
        WorkspaceCompilation unit,
        string rootPath,
        ImmutableArray<MigrationExposure>.Builder exposures,
        ISymbol locationSymbol)
    {
        foreach (var representation in Recognize(type))
        {
            var location = SourceLocation(locationSymbol, rootPath);
            var apiDisplay = api.ToDisplayString(
                SymbolDisplayFormat.CSharpErrorMessageFormat);
            var exposedType = type.ToDisplayString(
                SymbolDisplayFormat.CSharpErrorMessageFormat);
            var evidence = ImmutableArray.Create(
                    "source:" + location.Path + ":" +
                        location.StartLine.ToString(
                            System.Globalization.CultureInfo.InvariantCulture) + ":" +
                        location.StartColumn.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                    "api:" + apiDisplay,
                    "role:" + apiRole,
                    "type:" + exposedType,
                    "metadata:" + representation.MetadataIdentity,
                    "assembly:" + representation.AssemblyIdentity,
                    "api-assembly:" + ApiAssemblyIdentity(api),
                    "target-framework:" + unit.TargetFramework)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToImmutableArray();
            var risks = MigrationGuidance.Risks(
                representation.Name,
                evidence);
            exposures.Add(new MigrationExposure(
                representation.Name,
                apiDisplay,
                apiRole,
                exposedType,
                representation.MetadataIdentity,
                representation.AssemblyIdentity,
                ApiAssemblyIdentity(api),
                unit.Name,
                unit.TargetFramework,
                location,
                evidence,
                risks,
                MigrationGuidance.Comparison(representation.Name),
                MigrationGuidance.AdapterAssessments(representation.Name),
                MigrationGuidance.Recommendations(
                    representation.Name,
                    apiDisplay,
                    evidence)));
        }
    }

    private static void AddRecognizedTypes(
        ISymbol api,
        string apiRole,
        ITypeSymbol type,
        WorkspaceCompilation unit,
        string rootPath,
        ImmutableArray<MigrationExposure>.Builder exposures) =>
        AddRecognizedTypes(
            api,
            apiRole,
            type,
            unit,
            rootPath,
            exposures,
            api);

    private static ImmutableArray<RepresentationMatch> Recognize(ITypeSymbol type)
    {
        var matches = ImmutableArray.CreateBuilder<RepresentationMatch>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        Visit(type, visited, matches);
        return matches
            .Distinct()
            .OrderBy(match => match.Name, StringComparer.Ordinal)
            .ThenBy(match => match.MetadataIdentity, StringComparer.Ordinal)
            .ThenBy(match => match.AssemblyIdentity, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static void Visit(
        ITypeSymbol type,
        HashSet<ITypeSymbol> visited,
        ImmutableArray<RepresentationMatch>.Builder matches)
    {
        if (!visited.Add(type))
        {
            return;
        }

        Presence<ITypeSymbol> current = Presence.Of(type);
        while (current is Presence<ITypeSymbol>.Present found)
        {
            if (found.Value is INamedTypeSymbol named)
            {
                var metadataIdentity = named.OriginalDefinition
                    .GetFullMetadataName();
                if (IsOneOfIdentity(metadataIdentity))
                {
                    matches.Add(new RepresentationMatch(
                        "OneOf",
                        metadataIdentity,
                        AssemblyIdentity(named)));
                    break;
                }
            }

            current = found.Value.BaseType is INamedTypeSymbol baseType
                ? Presence.Of<ITypeSymbol>(baseType)
                : Presence.Missing<ITypeSymbol>();
        }

        current = Presence.Of(type);
        while (current is Presence<ITypeSymbol>.Present found)
        {
            if (found.Value is INamedTypeSymbol named)
            {
                var metadataIdentity = named.OriginalDefinition
                    .GetFullMetadataName();
                if (metadataIdentity.StartsWith(
                        "ValueOf.ValueOf`",
                        StringComparison.Ordinal))
                {
                    matches.Add(new RepresentationMatch(
                        "ValueOf",
                        metadataIdentity,
                        AssemblyIdentity(named)));
                    break;
                }
            }

            current = found.Value.BaseType is INamedTypeSymbol baseType
                ? Presence.Of<ITypeSymbol>(baseType)
                : Presence.Missing<ITypeSymbol>();
        }

        switch (type)
        {
            case IArrayTypeSymbol array:
                Visit(array.ElementType, visited, matches);
                break;
            case IPointerTypeSymbol pointer:
                Visit(pointer.PointedAtType, visited, matches);
                break;
            case INamedTypeSymbol named:
                foreach (var argument in named.TypeArguments)
                {
                    Visit(argument, visited, matches);
                }

                break;
            case ITypeParameterSymbol:
                break;
        }
    }

    private static bool IsOneOfIdentity(string metadataIdentity) =>
        metadataIdentity.StartsWith("OneOf.OneOf`", StringComparison.Ordinal) ||
        metadataIdentity.StartsWith(
            "OneOf.OneOfBase`",
            StringComparison.Ordinal);

    private static string AssemblyIdentity(ITypeSymbol type)
    {
        var identity = type.OriginalDefinition.ContainingAssembly.Identity;
        return identity.Name + ", Version=" + identity.Version;
    }

    private static string ApiAssemblyIdentity(ISymbol api)
    {
        var identity = api.ContainingAssembly.Identity;
        return identity.Name + ", Version=" + identity.Version;
    }

    private static SourceSpan SourceLocation(ISymbol symbol, string rootPath)
    {
        var location = symbol.Locations.FirstOrDefault(
            candidate => candidate.IsInSource) is Location present
            ? present
            : Location.None;
        var span = DiagnosticProjection.ToSourceSpan(location);
        return span with
        {
            Path = string.IsNullOrEmpty(span.Path)
                ? string.Empty
                : NormalizeRelativePath(rootPath, span.Path)
        };
    }

    private static ImmutableArray<SourceEvidence> CaptureSources(
        WorkspaceLoadResult workspace,
        string rootPath) =>
        workspace.Compilations
            .SelectMany(compilation => compilation.DocumentPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Where(path => !IsIntermediateSource(rootPath, path))
            .Select(path => new SourceEvidence(
                NormalizeRelativePath(rootPath, path),
                HashFile(path)))
            .OrderBy(source => source.Path, StringComparer.Ordinal)
            .ToImmutableArray();

    private static bool IsIntermediateSource(string rootPath, string path) =>
        NormalizeRelativePath(rootPath, path)
            .Split('/')
            .Any(segment => string.Equals(
                segment,
                "obj",
                StringComparison.OrdinalIgnoreCase));

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string NormalizeRelativePath(string rootPath, string path) =>
        Fingerprints.NormalizePath(
            Path.GetRelativePath(rootPath, Path.GetFullPath(path)));

    private static bool IsExternallyVisible(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility is not (
                Accessibility.Public or
                Accessibility.Protected or
                Accessibility.ProtectedOrInternal))
        {
            return false;
        }

        Presence<INamedTypeSymbol> containing = symbol.ContainingType is
            INamedTypeSymbol containingType
            ? Presence.Of(containingType)
            : Presence.Missing<INamedTypeSymbol>();
        while (containing is Presence<INamedTypeSymbol>.Present type)
        {
            if (type.Value.DeclaredAccessibility is not (
                    Accessibility.Public or
                    Accessibility.Protected or
                    Accessibility.ProtectedOrInternal))
            {
                return false;
            }

            containing = type.Value.ContainingType is INamedTypeSymbol parent
                ? Presence.Of(parent)
                : Presence.Missing<INamedTypeSymbol>();
        }

        return true;
    }

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

    private sealed record RepresentationMatch(
        string Name,
        string MetadataIdentity,
        string AssemblyIdentity);
}
