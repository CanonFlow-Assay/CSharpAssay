using CsAssay.Domain;

namespace CsAssay.Workspaces.Tests;

public sealed class PolicyLoaderTests
{
    [Fact]
    public void Parses_strict_policy()
    {
        var policy = PolicyLoader.Parse(
            """
            {
              "profile": "compat",
              "release": {
                "allowPreviewToolchain": false,
                "requiredTargetFrameworks": ["net10.0"]
              },
              "boundaries": {
                "coreProjects": ["src/Acme.Domain/Acme.Domain.csproj"],
                "shellProjects": ["src/Acme.Web/Acme.Web.csproj"],
                "coreNamespaces": ["Acme.Domain"],
                "shellNamespaces": ["Acme.Web"]
              },
              "representations": {
                "resultTypes": ["Acme.Result`2"],
                "optionTypes": [],
                "closedTypes": ["Acme.Outcome"]
              },
              "domainPrimitives": {
                "Acme.CustomerId": ["customerId"]
              },
              "suppressions": []
            }
            """);

        Assert.Equal(AssayProfile.Compat, policy.Profile);
        Assert.Equal(["net10.0"], policy.Release.RequiredTargetFrameworks);
        Assert.Equal(
            ["src/Acme.Domain/Acme.Domain.csproj"],
            policy.Boundaries.CoreProjects);
        Assert.Equal(
            ["src/Acme.Web/Acme.Web.csproj"],
            policy.Boundaries.ShellProjects);
        Assert.Equal(["Acme.Outcome"], policy.Representations.ClosedTypes);
    }

    [Fact]
    public void Rejects_unknown_keys()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            PolicyLoader.Parse(
                """
                {
                  "profile": "compat",
                  "silentFallback": true
                }
                """));

        Assert.Contains("$.silentFallback", exception.Message);
    }

    [Fact]
    public void Rejects_invalid_metadata_names()
    {
        Assert.Throws<InvalidDataException>(() =>
            PolicyLoader.Parse(
                """
                {
                  "representations": {
                    "resultTypes": ["not a metadata name"],
                    "optionTypes": [],
                    "closedTypes": []
                  }
                }
                """));
    }
}
