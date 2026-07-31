using System.Text.Json;
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
                "requiredTargetFrameworks": ["net10.0"],
                "requiredRules": ["CSAN0001"],
                "tests": [
                  {
                    "input": "tests/Acme.Tests/Acme.Tests.csproj",
                    "configuration": "Release",
                    "noBuild": true,
                    "minimumExpectedTests": 12
                  }
                ]
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
        Assert.Equal(["CSAN0001"], policy.Release.RequiredRules);
        var test = Assert.Single(policy.Release.Tests);
        Assert.Equal(
            "tests/Acme.Tests/Acme.Tests.csproj",
            test.Input);
        Assert.True(test.NoBuild);
        Assert.Equal(12, test.MinimumExpectedTests);
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

    [Theory]
    [InlineData("../Acme.Tests/Acme.Tests.csproj")]
    [InlineData("/Acme.Tests/Acme.Tests.csproj")]
    [InlineData("Acme.Tests/run.sh")]
    public void Rejects_unsafe_or_non_dotnet_test_inputs(string input)
    {
        Assert.Throws<InvalidDataException>(() =>
            PolicyLoader.Parse(
                $$"""
                {
                  "release": {
                    "tests": [
                      {
                        "input": {{JsonSerializer.Serialize(input)}}
                      }
                    ]
                  }
                }
                """));
    }

    [Fact]
    public void Rejects_noncanonical_rule_ids()
    {
        Assert.Throws<InvalidDataException>(() =>
            PolicyLoader.Parse(
                """
                {
                  "release": {
                    "requiredRules": ["csan0001"]
                  }
                }
                """));
    }
}
