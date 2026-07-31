namespace CsAssay.TypeGym;

public sealed class TG04Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG04-payment-outcome");
}
