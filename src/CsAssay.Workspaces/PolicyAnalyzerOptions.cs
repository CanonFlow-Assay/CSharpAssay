using System.Collections.Immutable;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.Workspaces;

internal sealed class PolicyAnalyzerConfigOptionsProvider(
    AssayPolicy policy) : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions global = new DictionaryAnalyzerConfigOptions(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["csassay_closed_types"] = string.Join(
                ";",
                policy.Representations.ClosedTypes),
            ["csassay_domain_primitives"] = string.Join(
                ";",
                policy.DomainPrimitives
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => item.Key + "=" + string.Join(",", item.Value)))
        });

    private static readonly AnalyzerConfigOptions Empty =
        new DictionaryAnalyzerConfigOptions(
            new Dictionary<string, string>(StringComparer.Ordinal));

    public override AnalyzerConfigOptions GlobalOptions => global;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
}

internal sealed class DictionaryAnalyzerConfigOptions(
    IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
{
    public override bool TryGetValue(string key, out string value)
    {
        if (values.TryGetValue(key, out var found) &&
            found is string required)
        {
            value = required;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

internal static class PolicyAnalyzerOptions
{
    public static AnalyzerOptions Create(AssayPolicy policy) =>
        new(
            ImmutableArray<AdditionalText>.Empty,
            new PolicyAnalyzerConfigOptionsProvider(policy));
}
