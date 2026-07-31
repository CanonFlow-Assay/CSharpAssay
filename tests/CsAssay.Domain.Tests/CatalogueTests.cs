using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Domain.Tests;

public sealed class CatalogueTests
{
    [Fact]
    public void Rule_ids_are_unique_and_well_formed()
    {
        var ids = RuleCatalogue.All.Select(rule => rule.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^CSA[NUIEFDAP][0-9]{4}$", id));
    }

    [Fact]
    public void Research_slice_is_not_prematurely_admitted()
    {
        Assert.All(
            RuleCatalogue.All,
            rule => Assert.Equal(RuleStatus.Prototype, rule.Status));
    }

    [Fact]
    public void Every_rule_has_evidence_and_documentation_routes()
    {
        Assert.All(
            RuleCatalogue.All,
            rule =>
            {
                Assert.False(string.IsNullOrWhiteSpace(rule.RequiredEvidence));
                Assert.False(string.IsNullOrWhiteSpace(rule.PositiveSpecimen));
                Assert.False(string.IsNullOrWhiteSpace(rule.NegativeSpecimen));
                Assert.EndsWith(
                    rule.Id + ".md",
                    rule.Documentation,
                    StringComparison.Ordinal);
            });
    }
}
