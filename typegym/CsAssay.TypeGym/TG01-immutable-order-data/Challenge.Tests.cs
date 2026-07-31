namespace CsAssay.TypeGym;

public sealed class TG01Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG01-immutable-order-data");
}
