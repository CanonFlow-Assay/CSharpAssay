namespace CsAssay.TypeGym;

public sealed class TG02Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG02-validated-email");
}
