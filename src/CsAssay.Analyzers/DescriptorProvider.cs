using System.Collections.Immutable;
using CsAssay.Catalogue;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;

namespace CsAssay.Analyzers;

internal static class DescriptorProvider
{
    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        RuleCatalogue.All.Select(Create).ToImmutableArray();

    public static DiagnosticDescriptor Get(string id) =>
        All.First(descriptor => string.Equals(descriptor.Id, id, StringComparison.Ordinal));

    private static DiagnosticDescriptor Create(RuleRecord rule) =>
        new(
            id: rule.Id,
            title: rule.Title,
            messageFormat: rule.Title + ": {0}",
            category: rule.Category.ToString(),
            defaultSeverity: ToDiagnosticSeverity(rule.Disposition),
            isEnabledByDefault: rule.Status != RuleStatus.Retired,
            description: rule.Mechanism,
            helpLinkUri: RuleCatalogue.DocumentationUrl(rule));

    private static DiagnosticSeverity ToDiagnosticSeverity(RuleDisposition disposition) =>
        disposition switch
        {
            RuleDisposition.Block => DiagnosticSeverity.Warning,
            RuleDisposition.Advise => DiagnosticSeverity.Info,
            RuleDisposition.Inconclusive => DiagnosticSeverity.Info,
            _ => DiagnosticSeverity.Warning
        };
}
