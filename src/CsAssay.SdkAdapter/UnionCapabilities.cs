using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsAssay.SdkAdapter;

public static class UnionCapabilities
{
    // RC1's union API is experimental; the SDK and Roslyn packages are pinned.
#pragma warning disable RSEXPERIMENTAL006
    public static bool IsNativeUnion(ITypeSymbol type) => type.IsUnion;
#pragma warning restore RSEXPERIMENTAL006

    public static bool CompilerExposesUnionSymbols() =>
        !CSharpSyntaxTree.ParseText(
                "public union CompilerProbe(int, string);",
                new CSharpParseOptions(LanguageVersion.Preview))
            .GetDiagnostics()
            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
