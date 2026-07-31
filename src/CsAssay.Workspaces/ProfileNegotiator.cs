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
        bool allowPreview)
    {
        var previewLanguage = unit.Compilation.SyntaxTrees
            .Select(tree => tree.Options)
            .OfType<CSharpParseOptions>()
            .Any(options => options.LanguageVersion == LanguageVersion.Preview);
        var net11 = unit.TargetFramework.StartsWith(
            "net11.0",
            StringComparison.OrdinalIgnoreCase);
        var unionSupport = UnionCapabilities.CompilerExposesUnionSymbols();
        var nativeAvailable = net11 && previewLanguage && unionSupport && allowPreview;

        if (requested == AssayProfile.Native && !nativeAvailable)
        {
            return new ProfileNegotiation(
                EffectiveProfile.Compat,
                "native-unavailable",
                Presence.Of(new MissingEvidence(
                    "CSASSAY-NATIVE-UNAVAILABLE",
                    "Native profile requires net11.0, preview language, union symbols, and preview policy opt-in.",
                    unit.Name,
                    unit.TargetFramework)));
        }

        if (requested == AssayProfile.Native ||
            requested == AssayProfile.Auto && nativeAvailable)
        {
            return new ProfileNegotiation(
                EffectiveProfile.NativePreview,
                "native-preview",
                Presence.Missing<MissingEvidence>());
        }

        return new ProfileNegotiation(
            EffectiveProfile.Compat,
            "compat",
            Presence.Missing<MissingEvidence>());
    }
}
