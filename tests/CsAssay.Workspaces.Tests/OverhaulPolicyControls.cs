namespace CsAssay.Workspaces.Tests;

public sealed class OverhaulPolicyControls
{
    [Fact]
    public void Contradictory_project_boundaries_are_invalid()
        => Assert.Throws<InvalidDataException>(() => PolicyLoader.Parse("""{"boundaries":{"coreProjects":["Domain.csproj"],"shellProjects":["Domain.csproj"]}}"""));

    [Fact]
    public void Contradictory_namespace_boundaries_are_invalid()
        => Assert.Throws<InvalidDataException>(() => PolicyLoader.Parse("""{"boundaries":{"coreNamespaces":["Example.Domain"],"shellNamespaces":["Example.Domain"]}}"""));

    [Theory]
    [InlineData("CSAI9999")]
    [InlineData("CSAP0001")]
    public void Unknown_or_self_suppressing_grant_cannot_authorize_a_finding(string rule)
        => Assert.Throws<InvalidDataException>(() => PolicyLoader.Parse("{\"suppressions\":[" + Grant(rule) + "]}"));

    [Fact]
    public void Duplicate_grants_are_rejected_instead_of_first_match_winning()
        => Assert.Throws<InvalidDataException>(() => PolicyLoader.Parse("{\"suppressions\":[" + Grant("CSAI0001") + "," + Grant("CSAI0001") + "]}"));

    private static string Grant(string rule) => "{\"ruleId\":\"" + rule + "\",\"fingerprint\":\"" + new string('a', 64) + "\",\"owner\":\"test-owner\",\"reason\":\"Synthetic parser control, not a live grant\",\"expires\":\"2099-01-01\"}";
}
