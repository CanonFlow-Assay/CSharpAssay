using System.Text.Json;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Domain.Tests;

public sealed class AdmissionProofTests
{
    [Fact]
    public void Phase_two_manifest_closes_every_admitted_obligation()
    {
        var root = FindRoot();
        using var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "eng", "admission", "phase2.json")));
        var document = manifest.RootElement;
        var admitted = document
            .GetProperty("admittedRules")
            .EnumerateArray()
            .Select(RequiredString)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var catalogue = RuleCatalogue.All
            .Where(rule => rule.Status == RuleStatus.Admitted)
            .Select(rule => rule.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            admitted.Length >= document
                .GetProperty("minimumAdmittedRules")
                .GetInt32());
        Assert.Equal(catalogue, admitted);

        var obligations = document
            .GetProperty("obligations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(admitted.Length, obligations.Length);
        foreach (var obligation in obligations)
        {
            Assert.Contains(
                RequiredString(obligation.GetProperty("ruleId")),
                admitted);
            Assert.True(obligation.GetProperty("positive").GetBoolean());
            Assert.True(obligation.GetProperty("negative").GetBoolean());
            Assert.True(obligation.GetProperty("suppression").GetBoolean());
            Assert.True(obligation.GetProperty("fault").GetBoolean());
            Assert.True(obligation.GetProperty("matrix").GetBoolean());
            Assert.True(obligation.GetProperty("realWorld").GetBoolean());
        }

        Assert.True(File.Exists(Path.Combine(
            root,
            RequiredString(document
                .GetProperty("proof")
                .GetProperty("realWorldAdjudication")))));
        Assert.True(File.Exists(Path.Combine(
            root,
            RequiredString(document
                .GetProperty("proof")
                .GetProperty("boundaryQualificationFixture")))));
    }

    [Fact]
    public void Real_world_adjudication_is_complete_and_does_not_invent_precision()
    {
        var root = FindRoot();
        using var admission = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "eng", "admission", "phase2.json")));
        using var adjudication = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(
                root,
                "specimens",
                "RealWorld",
                "adjudication.json")));
        var admitted = admission.RootElement
            .GetProperty("admittedRules")
            .EnumerateArray()
            .Select(RequiredString)
            .ToHashSet(StringComparer.Ordinal);
        var adjudicatedRules = adjudication.RootElement
            .GetProperty("rules")
            .EnumerateArray()
            .Where(rule => admitted.Contains(
                RequiredString(rule.GetProperty("ruleId"))))
            .ToArray();

        Assert.Equal(admitted.Count, adjudicatedRules.Length);
        foreach (var rule in adjudicatedRules)
        {
            Assert.Equal(
                "qualified",
                RequiredString(rule.GetProperty("status")));
            var findingCount = rule.GetProperty("findingCount").GetInt32();
            if (findingCount == 0)
            {
                Assert.Equal(
                    "run-clean",
                    RequiredString(rule.GetProperty("verdict")));
                Assert.Equal(
                    "inconclusive",
                    RequiredString(rule.GetProperty("precision")));
                continue;
            }

            var groupCount = rule
                .GetProperty("groups")
                .EnumerateArray()
                .Sum(group => group.GetProperty("count").GetInt32());
            Assert.Equal(findingCount, groupCount);
        }

        var summary = adjudication.RootElement.GetProperty("summary");
        Assert.Equal(0, summary.GetProperty("falsePositives").GetInt32());
        Assert.Equal(0, summary.GetProperty("toolFailures").GetInt32());
        Assert.True(
            summary.GetProperty("precisionDenominator").GetInt32() > 0);
        Assert.Equal(1.0, summary.GetProperty("precision").GetDouble());
        Assert.Equal(
            adjudicatedRules.Sum(rule =>
                rule.GetProperty("findingCount").GetInt32()),
            summary.GetProperty("admittedRuleFindings").GetInt32());
    }

    private static string RequiredString(JsonElement element) =>
        element.GetString() is string value
            ? value
            : throw new InvalidDataException("Expected a JSON string.");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CSharpAssay.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate CSharpAssay.slnx.");
    }
}
