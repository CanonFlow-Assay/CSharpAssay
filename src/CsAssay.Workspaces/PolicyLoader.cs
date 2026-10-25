using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Workspaces;

public sealed record PolicyLoadResult(
    AssayPolicy Policy,
    Presence<string> Path,
    ImmutableArray<EvaluationFailure> Failures,
    bool UsedDefault)
{
    public string RawSha256 { get; init; } = string.Empty;
    public string TextSha256 { get; init; } = string.Empty;
    public Presence<PolicyEvaluation> Evaluation { get; init; } = Presence.Missing<PolicyEvaluation>();
}

public static class PolicyLoader
{
    public static PolicyLoadResult Load(
        string inputPath,
        Presence<string> explicitPolicyPath,
        bool requirePolicy)
    {
        try
        {
            var policyPath = DiscoverPath(inputPath, explicitPolicyPath);

            if (policyPath is not Presence<string>.Present found)
            {
                var failures = requirePolicy
                    ? ImmutableArray.Create(new EvaluationFailure(
                        "CSASSAY-CONFIG-MISSING",
                        "Authoritative verification requires .csassay.json.",
                        "policy",
                        Presence.Missing<string>()))
                    : ImmutableArray<EvaluationFailure>.Empty;
                return new PolicyLoadResult(
                    AssayPolicy.Observe,
                    Presence.Missing<string>(),
                    failures,
                    UsedDefault: true);
            }

            if (!File.Exists(found.Value))
            {
                return Failure(
                    "CSASSAY-CONFIG-NOT-FOUND",
                    "Policy file does not exist: " + found.Value);
            }

            var bytes = File.ReadAllBytes(found.Value);
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            var policy = Parse(text);
            return new PolicyLoadResult(
                policy,
                policyPath,
                ImmutableArray<EvaluationFailure>.Empty,
                UsedDefault: false)
            {
                RawSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                TextSha256 = Fingerprints.Sha256(text),
                Evaluation = Presence.Of(PolicyEvaluation.Normalize(policy, RuleCatalogue.All.Select(rule => rule.Id).ToImmutableArray(), OperatingSystem.IsWindows()))
            };
        }
        catch (Exception exception) when (
            exception is JsonException or
            InvalidDataException or
            IOException or
            UnauthorizedAccessException)
        {
            return Failure("CSASSAY-CONFIG-INVALID", exception.Message);
        }
    }

    public static AssayPolicy Parse(string json) => PolicyParser.Parse(json, RuleCatalogue.All.Select(rule => rule.Id).ToImmutableArray());

    public static Presence<string> DiscoverPath(string inputPath, Presence<string> explicitPolicyPath) => explicitPolicyPath switch
    {
        Presence<string>.Present present => Presence.Of(Path.GetFullPath(present.Value)),
        _ => FindPolicy(inputPath)
    };

    private static Presence<string> FindPolicy(string inputPath)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        Presence<DirectoryInfo> directory = Directory.Exists(fullInputPath)
            ? Presence.Of(new DirectoryInfo(fullInputPath))
            : new FileInfo(fullInputPath).Directory is DirectoryInfo parent
                ? Presence.Of(parent)
                : Presence.Missing<DirectoryInfo>();

        while (directory is Presence<DirectoryInfo>.Present current)
        {
            var candidate = Path.Combine(current.Value.FullName, ".csassay.json");
            if (File.Exists(candidate))
            {
                return Presence.Of(candidate);
            }

            directory = current.Value.Parent is DirectoryInfo next
                ? Presence.Of(next)
                : Presence.Missing<DirectoryInfo>();
        }

        return Presence.Missing<string>();
    }

    private static PolicyLoadResult Failure(string code, string message) =>
        new(
            AssayPolicy.Observe,
            Presence.Missing<string>(),
            ImmutableArray.Create(new EvaluationFailure(
                code,
                message,
                "policy",
                Presence.Missing<string>())),
            UsedDefault: true);

}
