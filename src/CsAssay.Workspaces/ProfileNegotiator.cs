using CsAssay.Domain;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis.CSharp;

namespace CsAssay.Workspaces;

public sealed record ProfileNegotiation(
    EffectiveProfile Profile,
    string EvidenceName,
    Presence<MissingEvidence> Missing);

public static class ProfileNegotiator
{
    public static ProfileNegotiation Negotiate(
        AssayProfile requested,
        WorkspaceCompilation unit,
        bool allowPreview = true)
    {
        var nativeLanguage = unit.Compilation.SyntaxTrees
            .Select(tree => tree.Options)
            .OfType<CSharpParseOptions>()
            .All(options => options.LanguageVersion == LanguageVersion.Preview);
        var net11 = unit.TargetFramework.Equals("net11.0", StringComparison.OrdinalIgnoreCase) ||
            unit.TargetFramework.StartsWith("net11.0-", StringComparison.OrdinalIgnoreCase);
        var runtimeSupport = unit.Compilation.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.UnionAttribute") is not null &&
            unit.Compilation.GetTypeByMetadataName(
                "System.Runtime.CompilerServices.IUnion") is not null;

        if (requested != AssayProfile.Native || !net11 || !nativeLanguage ||
            !allowPreview || !runtimeSupport || !UnionCapabilities.CompilerExposesUnionSymbols())
        {
            return new ProfileNegotiation(
                EffectiveProfile.Native,
                "native-unavailable",
                Presence.Of(new MissingEvidence(
                    "CSASSAY-NATIVE-UNAVAILABLE",
                    "CSharpAssay requires .NET 11, C# preview, and native compiler/runtime union support. Earlier targets and library compatibility profiles are unsupported.",
                    unit.Name,
                    unit.TargetFramework)));
        }

        return new ProfileNegotiation(
            EffectiveProfile.Native,
            "native",
            Presence.Missing<MissingEvidence>());
    }
}
