using System.Text.Json;
using CsAssay.Domain;
using CsAssay.Workspaces;

namespace CsAssay.Workspaces.Tests;

public sealed class MigrationInventoryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public async Task Inventory_links_every_recommendation_to_exact_api_evidence()
    {
        var fixture = Fixture();
        var sourceHashesBefore = SourceHashes(fixture);

        var report = await MigrationInventory.AnalyzeAsync(
            fixture,
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Failures);
        Assert.Equal("report-only", report.Mode);
        Assert.Collection(
            report.Sources,
            source => Assert.Equal("PublicApi.cs", source.Path));
        Assert.Contains(
            report.Exposures,
            exposure => exposure is
            {
                Representation: "OneOf",
                ApiRole: "return",
                MetadataIdentity: "OneOf.OneOf`2",
                AssemblyIdentity: "MigrationDependencies, Version=3.2.1.0"
            } && exposure.ApiAssemblyIdentity ==
                "MigrationSurface, Version=7.8.9.0" &&
                exposure.Api.Contains(
                "Decide",
                StringComparison.Ordinal));
        Assert.Contains(
            report.Exposures,
            exposure => exposure is
            {
                Representation: "OneOf",
                MetadataIdentity: "OneOf.OneOfBase`1"
            } && exposure.Api.Contains(
                "NamedOutcome",
                StringComparison.Ordinal));
        Assert.Contains(
            report.Exposures,
            exposure => exposure is
            {
                Representation: "ValueOf",
                ApiRole: "parameter:customerId",
                MetadataIdentity: "ValueOf.ValueOf`2"
            });
        Assert.Contains(
            report.Exposures,
            exposure => exposure.Api.Contains(
                    "NestedAsync",
                    StringComparison.Ordinal) &&
                exposure.Representation == "OneOf");
        Assert.Contains(
            report.Exposures,
            exposure => exposure.Api.Contains(
                    "NestedAsync",
                    StringComparison.Ordinal) &&
                exposure.Representation == "ValueOf");
        Assert.Contains(
            report.Exposures,
            exposure => exposure.Api.Contains(
                    "GenericSurface",
                    StringComparison.Ordinal) &&
                exposure.ApiRole == "constraint:T" &&
                exposure.Representation == "OneOf");
        Assert.Contains(
            report.Exposures,
            exposure => exposure.Api.Contains(
                    "this[",
                    StringComparison.Ordinal) &&
                exposure.ApiRole == "parameter:customerId" &&
                exposure.Representation == "ValueOf");
        Assert.DoesNotContain(
            report.Exposures,
            exposure => exposure.Api.Contains(
                "Hidden",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            report.Exposures,
            exposure => exposure.Api.EndsWith(
                ".get",
                StringComparison.Ordinal));

        Assert.All(report.Exposures, exposure =>
        {
            Assert.NotEqual(SourceSpan.None, exposure.Location);
            Assert.Equal(4, exposure.AdapterAssessments.Length);
            Assert.All(exposure.Recommendations, recommendation =>
            {
                Assert.Equal(exposure.Api, recommendation.AffectedApi);
                Assert.True(exposure.Evidence.SequenceEqual(
                    recommendation.Evidence));
                Assert.DoesNotContain(
                    "find-and-replace operation",
                    recommendation.Statement,
                    StringComparison.OrdinalIgnoreCase);
            });
            Assert.Contains(
                exposure.Recommendations,
                recommendation => recommendation.Statement.Contains(
                    "do not use find-and-replace",
                    StringComparison.Ordinal));
        });
        Assert.Equal(sourceHashesBefore, SourceHashes(fixture));
    }

    [Fact]
    public async Task Inventory_is_byte_deterministic_and_keeps_unqualified_adapters_disabled()
    {
        var first = await MigrationInventory.AnalyzeAsync(
            Fixture(),
            TestContext.Current.CancellationToken);
        var second = await MigrationInventory.AnalyzeAsync(
            Fixture(),
            TestContext.Current.CancellationToken);

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Contains(
            first.EcosystemAdapters,
            adapter => adapter is
            {
                Name: "OneOf",
                Status: "observation-only",
                Version: "unqualified"
            });
        Assert.Contains(
            first.EcosystemAdapters,
            adapter => adapter is
            {
                Name: "ValueOf",
                Status: "legacy-observation-only",
                Version: "unqualified"
            });
        Assert.All(
            first.EcosystemAdapters.Where(adapter => adapter.Name is
                "Vogen" or
                "dunet" or
                "Thinktecture.Runtime.Extensions"),
            adapter => Assert.Equal("not-enabled", adapter.Status));
    }

    [Fact]
    public async Task Missing_input_is_a_visible_migration_failure()
    {
        var report = await MigrationInventory.AnalyzeAsync(
            Path.Combine(FindRoot(), "missing-migration.csproj"),
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Exposures);
        Assert.Contains(
            report.Failures,
            failure => failure.Code == "CSASSAY-MIGRATION-WORKSPACE");
    }

    private static string Serialize(MigrationReport report) =>
        JsonSerializer.Serialize(report, JsonOptions);

    private static string Fixture() =>
        Path.Combine(
            FindRoot(),
            "specimens",
            "Projects",
            "MigrationSurface",
            "MigrationSurface.csproj");

    private static string SourceHashes(string fixture)
    {
        var directory = Path.GetDirectoryName(fixture) is string present
            ? present
            : throw new InvalidOperationException(
                "Migration fixture has no directory.");
        return string.Join(
            "\n",
            Directory.GetFiles(directory, "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path + "=" +
                    CsAssay.Domain.Fingerprints.Sha256(
                        File.ReadAllText(path))));
    }

    private static string FindRoot() =>
        FindRoot(new DirectoryInfo(AppContext.BaseDirectory));

    private static string FindRoot(DirectoryInfo directory)
    {
        if (File.Exists(Path.Combine(directory.FullName, "CSharpAssay.slnx")))
        {
            return directory.FullName;
        }

        return directory.Parent is DirectoryInfo parent
            ? FindRoot(parent)
            : throw new InvalidOperationException(
                "Could not locate CSharpAssay.slnx.");
    }
}
