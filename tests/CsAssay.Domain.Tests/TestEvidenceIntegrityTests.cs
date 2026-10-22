using System.Collections.Immutable;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Domain.Tests;

public sealed class TestEvidenceIntegrityTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 0, 0, 1)]
    [InlineData(2, 1, 0, 0)]
    [InlineData(1, 1, 0, 0, 1)]
    public void IncompleteRequiredTestEvidenceCannotAuthorizePass(int total, int passed, int failed, int skipped, int exitCode = 0)
    {
        var evidence = EvidenceBundle.Empty("fixture.csproj", true) with
        {
            Projects = [new("Fixture", "fixture.csproj", "net11.0", EffectiveProfile.Native, "qualified", "preview", "Enable", true, ImmutableArray<string>.Empty, ImmutableArray<CompilerEvidence>.Empty)],
            Tests = [new("tests/Fixture.csproj", "Release", true, TestRunOutcome.Passed, exitCode, total, passed, failed, skipped)]
        };
        var verdict = VerdictFactory.Create(evidence, RuleCatalogue.All);
        Assert.IsType<InconclusiveVerdict>(verdict);
        Assert.False(verdict.Evidence.IsAuthoritative);
    }
}
