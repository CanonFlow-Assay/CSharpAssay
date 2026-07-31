using CsAssay.Domain;
using CsAssay.Reporting;
using CsAssay.Workspaces;

namespace CsAssay.Integration.Tests;

public sealed class VerificationIntegrationTests
{
    [Fact]
    public async Task Check_is_provisional_and_deterministic()
    {
        var root = FindRoot();
        var project = Path.Combine(
            root,
            "src",
            "CsAssay.Domain",
            "CsAssay.Domain.csproj");
        var request = new VerificationRequest(
            project,
            PolicyPath: Presence.Missing<string>(),
            IsAuthoritative: false,
            ProfileOverride: Presence.Of(AssayProfile.Compat));

        var first = await VerificationEngine.VerifyAsync(
            request,
            TestContext.Current.CancellationToken);
        var second = await VerificationEngine.VerifyAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(first.Verdict.Evidence.IsAuthoritative);
        Assert.Equal(
            JsonEvidenceWriter.Write(first.Verdict),
            JsonEvidenceWriter.Write(second.Verdict));
    }

    [Fact]
    public async Task Authoritative_self_assay_can_pass_with_admitted_rules()
    {
        var root = FindRoot();
        var solution = Path.Combine(root, "CSharpAssay.slnx");

        var result = await VerificationEngine.VerifyAsync(
            new VerificationRequest(
                solution,
                PolicyPath: Presence.Missing<string>(),
                IsAuthoritative: true,
                ProfileOverride: Presence.Of(AssayProfile.Compat)),
            TestContext.Current.CancellationToken);

        Assert.Equal(AssayVerdictKind.Pass, result.Verdict.Kind);
        Assert.DoesNotContain(
            result.Verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-NO-ADMITTED-RULES");
        Assert.DoesNotContain(
            result.Verdict.Evidence.Missing,
            item => item.Code == "CSASSAY-WORKSPACE-WARNING");
        Assert.NotEmpty(result.Verdict.Evidence.WorkspaceDiagnostics);
        Assert.All(
            result.Verdict.Evidence.WorkspaceDiagnostics,
            diagnostic => Assert.False(diagnostic.AffectsCompleteness));
        Assert.All(
            result.Verdict.Evidence.Sources,
            source =>
            {
                Assert.False(Path.IsPathRooted(source.Path));
                Assert.False(
                    source.Path.StartsWith("../", StringComparison.Ordinal));
            });
    }

    [Fact]
    public async Task Project_level_nullable_evidence_respects_shell_scope()
    {
        var root = FindRoot();
        var project = Path.Combine(
            root,
            "specimens",
            "Projects",
            "BoundaryScope",
            "BoundaryScope.csproj");

        var result = await VerificationEngine.VerifyAsync(
            new VerificationRequest(
                project,
                PolicyPath: Presence.Missing<string>(),
                IsAuthoritative: false,
                ProfileOverride: Presence.Of(AssayProfile.Compat)),
            TestContext.Current.CancellationToken);

        var nullableDisabled = Assert.Single(
            result.Verdict.Evidence.Findings,
            finding => finding.RuleId == "CSAN0001");
        Assert.Equal(RuleDisposition.Advise, nullableDisabled.Disposition);
        Assert.DoesNotContain(
            result.Verdict.Evidence.Findings,
            finding =>
                finding.RuleId == "CSAN0003" &&
                finding.Disposition == RuleDisposition.Block);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CSharpAssay.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate CSharpAssay.slnx.");
    }
}
