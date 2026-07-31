namespace CsAssay.TypeGym;

public sealed class TG09Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG09-async-boundary");
}
