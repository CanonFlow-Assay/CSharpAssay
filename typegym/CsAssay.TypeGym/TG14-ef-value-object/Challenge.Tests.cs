namespace CsAssay.TypeGym;

public sealed class TG14Tests
{
    [Fact]
    public Task Compiles_analyzes_and_matches_golden_behavior() =>
        CorpusVerifier.VerifyAsync("TG14-ef-value-object");
}
