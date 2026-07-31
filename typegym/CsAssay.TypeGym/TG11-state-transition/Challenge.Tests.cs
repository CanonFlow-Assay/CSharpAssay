namespace CsAssay.TypeGym;

public sealed class TG11Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG11-state-transition");
}
