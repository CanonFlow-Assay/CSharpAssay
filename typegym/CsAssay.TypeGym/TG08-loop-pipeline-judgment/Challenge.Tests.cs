namespace CsAssay.TypeGym;

public sealed class TG08Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG08-loop-pipeline-judgment");
}
