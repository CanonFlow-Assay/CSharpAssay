using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Runner;

/// <summary>Canonical repository guides, embedded at build time; no network access.</summary>
public static class RuleDocumentation
{
    public static string Read(RuleRecord rule)
    {
        using var stream = typeof(RuleCatalogue).Assembly.GetManifestResourceStream("CsAssay.RuleGuides." + rule.Id + ".md")
            ?? throw new InvalidOperationException("Bundled rule guide is missing: " + rule.Id);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
