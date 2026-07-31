namespace CsAssay.TypeGym;

public sealed class TG06Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG06-accumulate-validation");
}
