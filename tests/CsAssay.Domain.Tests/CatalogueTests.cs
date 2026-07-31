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
    public void Only_the_proven_phase_two_slice_is_admitted()
    {
        var admitted = RuleCatalogue.All
            .Where(rule => rule.Status == RuleStatus.Admitted)
            .Select(rule => rule.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                RuleIds.MutableSetter,
                RuleIds.MutableCollectionExposure,
                RuleIds.NullableDisabled,
                RuleIds.NullForgiving,
                RuleIds.NullValueIntroduction,
                RuleIds.NullableCoreContract,
                RuleIds.UnauthorizedSuppression
            ],
            admitted);
        Assert.DoesNotContain(
            RuleCatalogue.All,
            rule => rule.Status == RuleStatus.Retired);
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
