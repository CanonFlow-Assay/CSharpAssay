namespace CsAssay.TypeGym;

public sealed class TG05Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG05-railway-pipeline");
}
