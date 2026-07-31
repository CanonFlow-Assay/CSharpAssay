namespace CsAssay.TypeGym;

public sealed class TG15Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG15-migration");
}
