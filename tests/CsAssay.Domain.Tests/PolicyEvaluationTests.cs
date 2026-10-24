using System.Collections.Immutable;
using CsAssay.Domain;

namespace CsAssay.Domain.Tests;

public sealed class PolicyEvaluationTests
{
    private static readonly ImmutableArray<string> Known = ["CSAI0001", "CSAP0001"];
    private static readonly DateOnly Date = new(2026, 10, 23);

    [Fact]
    public void Expiry_is_inclusive_and_rule_and_fingerprint_are_ordinal()
    {
        var fingerprint = Fingerprints.Sha256("synthetic-date-control");
        var grant = new SuppressionGrant("CSAI0001", "test-owner", "Boundary conversion reviewed", new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero), fingerprint);
        var evaluation = PolicyEvaluation.Normalize(AssayPolicy.Observe with { Suppressions = [grant] }, Known, false);
        Assert.IsType<Presence<SuppressionGrant>.Present>(evaluation.AcceptedGrant("CSAI0001", fingerprint, Date));
        Assert.IsType<Presence<SuppressionGrant>.Absent>(evaluation.AcceptedGrant("CSAI0001", fingerprint, Date.AddDays(1)));
        Assert.IsType<Presence<SuppressionGrant>.Absent>(evaluation.AcceptedGrant("csai0001", fingerprint, Date));
        Assert.IsType<Presence<SuppressionGrant>.Absent>(evaluation.AcceptedGrant("CSAI0001", fingerprint.ToUpperInvariant(), Date));
        Assert.IsType<Presence<SuppressionGrant>.Absent>(evaluation.AcceptedGrant("CSAI0001", Fingerprints.Sha256("changed source identity"), Date));
    }

    [Fact]
    public void Defaults_and_namespace_exceptions_remain_explicit_and_case_sensitive()
    {
        var policy = AssayPolicy.Observe with { Boundaries = new(["Domain.csproj"], ["Store.csproj"], ["App.Domain"], ["App.Domain.Persistence"]) };
        var evaluation = PolicyEvaluation.Normalize(policy, Known, false);
        Assert.Equal(new BoundaryDecision("core", true), evaluation.Classify("Domain.csproj", "App.Domain.Values", true, true));
        Assert.Equal(new BoundaryDecision("shell", false), evaluation.Classify("Domain.csproj", "App.Domain.Persistence", true, true));
        Assert.Equal(new BoundaryDecision("shell", false), evaluation.Classify("Store.csproj", "App.Domain", true, true));
        Assert.Equal(new BoundaryDecision("default", false), evaluation.Classify("Other.csproj", "App.Domain", true, true));
        Assert.Equal(new BoundaryDecision("default", false), evaluation.Classify("Domain.csproj", "app.domain", true, false));
        Assert.Equal(RuleDisposition.Block, PolicyEvaluation.Disposition("CSAP0001", RuleDisposition.Block, new("shell", false)));
        Assert.Equal(["Store.csproj"], evaluation.UnmatchedProjects(["Domain.csproj"]));
        var defaults = PolicyEvaluation.Normalize(AssayPolicy.Observe, Known, false);
        Assert.Equal(new BoundaryDecision("default", true), defaults.Classify("Any.csproj", "Any", true, false));
    }

    [Fact]
    public void Path_case_is_a_host_input_and_backslashes_are_normalized()
    {
        var policy = AssayPolicy.Observe with { Boundaries = new(["src\\Domain.csproj"], [], [], []) };
        Assert.True(PolicyEvaluation.Normalize(policy, Known, false).Classify("src/Domain.csproj", "", false, false).UsesCoreDisposition);
        Assert.False(PolicyEvaluation.Normalize(policy, Known, false).Classify("SRC/Domain.csproj", "", false, false).UsesCoreDisposition);
        Assert.True(PolicyEvaluation.Normalize(policy, Known, true).Classify("SRC/Domain.csproj", "", false, false).UsesCoreDisposition);
    }

    [Fact]
    public void External_policy_paths_use_normalized_literal_graph_identities()
    {
        var policy = AssayPolicy.Observe with { Boundaries = new(["../app/src/./Domain.csproj"], [], [], []) };
        var evaluation = PolicyEvaluation.Normalize(policy, Known, false);
        Assert.True(evaluation.Classify("../app/src/Domain.csproj", "", true, false).UsesCoreDisposition);
        Assert.Empty(evaluation.UnmatchedProjects(["../app/src/Domain.csproj"]));
        Assert.False(evaluation.RequiresNamespaceEvaluation("../app/src/Domain.csproj"));
    }

    [Fact]
    public void Unicode_and_escaped_namespace_identifiers_preserve_ordinal_identity()
    {
        var policy = AssayPolicy.Observe with { Boundaries = new([], [], ["应用.@class"], []) };
        var evaluation = PolicyEvaluation.Normalize(policy, Known, false);
        Assert.True(evaluation.Classify("Domain.csproj", "应用.class", true, true).UsesCoreDisposition);
        Assert.False(evaluation.Classify("Domain.csproj", "应用.Class", true, false).UsesCoreDisposition);
        Assert.Throws<InvalidDataException>(() => PolicyEvaluation.Normalize(policy with { Boundaries = policy.Boundaries with { ShellNamespaces = ["应用.class"] } }, Known, false));
    }
}
