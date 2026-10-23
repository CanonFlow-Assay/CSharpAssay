using System.Text.Json;
using CsAssay.Domain;
using CsAssay.Reporting;

namespace CsAssay.Runner.Tests;

public sealed class AssessmentTests
{
    [Fact]
    public void Assessment_command_preserves_verify_options()
    {
        var command = CommandLine.Parse(["assess", "Sample.slnx", "--json", "assessment.json", "--profile", "native"]);
        Assert.Equal("assess", command.Command);
        Assert.Equal(new Presence<string>.Present("Sample.slnx"), command.Input);
    }

    [Fact]
    public void Projection_does_not_promote_provisional_authority()
    {
        var verdict = new PassVerdict(EvidenceBundle.Empty("sample", false));
        var snapshot = AssessmentSnapshot.FromVerdict(verdict);
        Assert.False(snapshot.Authoritative);
        Assert.Equal("csassay-assessment/0.1", snapshot.Schema);
    }

    [Fact]
    public void Envelope_retains_complete_receipt_and_is_deterministic()
    {
        var verdict = new InconclusiveVerdict([], EvidenceBundle.Empty("sample", false));
        var bytes = AssessmentWriter.Write(verdict);
        Assert.Equal(bytes, AssessmentWriter.Write(verdict));
        using var envelope = JsonDocument.Parse(bytes);
        using var original = JsonDocument.Parse(JsonEvidenceWriter.Write(verdict));
        Assert.True(JsonElement.DeepEquals(original.RootElement, envelope.RootElement.GetProperty("receipt")));
        Assert.Equal("Inconclusive", envelope.RootElement.GetProperty("assessment").GetProperty("verdict").GetString());
    }
}
