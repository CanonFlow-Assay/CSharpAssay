namespace CsAssay.TypeGym;

public sealed class TG03Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG03-customer-order-ids");
}
