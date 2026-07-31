namespace CsAssay.TypeGym;

public sealed class TG10Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG10-resource-boundary");
}
