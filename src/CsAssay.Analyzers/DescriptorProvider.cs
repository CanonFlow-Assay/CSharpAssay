using System.Collections.Immutable;
using System.Collections.Concurrent;
using CsAssay.Catalogue;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;

namespace CsAssay.Analyzers;

internal static class DescriptorProvider
{
    public static ImmutableArray<DiagnosticDescriptor> All { get; } =
        RuleCatalogue.All.Select(Create).ToImmutableArray();

    private static readonly ImmutableDictionary<string, DiagnosticDescriptor> ById = All.ToImmutableDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(string Id, DiagnosticSeverity Severity, bool Protect), DiagnosticDescriptor> Effective = new();

    public static DiagnosticDescriptor Get(string id) =>
        ById[id];

    public static DiagnosticDescriptor ForEffectiveSeverity(string id, DiagnosticSeverity severity, bool protect)
    {
        return Effective.GetOrAdd((id, severity, protect), static key =>
        {
            var descriptor = Get(key.Id);
            return new(descriptor.Id, descriptor.Title, descriptor.MessageFormat, descriptor.Category,
                key.Severity, descriptor.IsEnabledByDefault, descriptor.Description, descriptor.HelpLinkUri,
                key.Protect ? [WellKnownDiagnosticTags.NotConfigurable] : []);
        });
    }

    private static DiagnosticDescriptor Create(RuleRecord rule) =>
        new(
            id: rule.Id,
            title: rule.Title,
            messageFormat: rule.Title + ": {0}",
            category: rule.Category.ToString(),
            defaultSeverity: ToDiagnosticSeverity(rule.Disposition),
            isEnabledByDefault: rule.Status != RuleStatus.Retired,
            description: rule.Mechanism,
            helpLinkUri: RuleCatalogue.DocumentationUrl(rule),
            customTags: rule.Id == RuleIds.UnauthorizedSuppression ? [WellKnownDiagnosticTags.NotConfigurable] : []);

    private static DiagnosticSeverity ToDiagnosticSeverity(RuleDisposition disposition) =>
        disposition switch
        {
            RuleDisposition.Block => DiagnosticSeverity.Warning,
            RuleDisposition.Advise => DiagnosticSeverity.Info,
            RuleDisposition.Inconclusive => DiagnosticSeverity.Info,
            _ => DiagnosticSeverity.Warning
        };
}
