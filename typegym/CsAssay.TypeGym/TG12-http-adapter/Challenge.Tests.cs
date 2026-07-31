namespace CsAssay.TypeGym;

public sealed class TG12Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG12-http-adapter");
}
