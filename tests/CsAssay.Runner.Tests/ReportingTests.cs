using System.Collections.Immutable;
using CsAssay.Domain;
using CsAssay.Reporting;

namespace CsAssay.Runner.Tests;

public sealed class ReportingTests
{
    [Fact]
    public void Json_and_sarif_are_byte_deterministic()
    {
        var evidence = EvidenceBundle.Empty("Sample.csproj", true);
        var verdict = new InconclusiveVerdict(
            ImmutableArray.Create(new MissingEvidence(
                "TEST",
                "missing",
                "Sample",
                "net10.0")),
            evidence with
            {
                Missing =
                [
                    new MissingEvidence(
                        "TEST",
                        "missing",
                        "Sample",
                        "net10.0")
                ]
            });

        Assert.Equal(
            JsonEvidenceWriter.Write(verdict),
            JsonEvidenceWriter.Write(verdict));
        Assert.Equal(
            SarifWriter.Write(verdict),
            SarifWriter.Write(verdict));
    }
}
