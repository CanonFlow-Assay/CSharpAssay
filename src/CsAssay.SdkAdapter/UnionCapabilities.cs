using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsAssay.SdkAdapter;

public static class UnionCapabilities
{
    // Keep the compiler probe aligned with the stable native language lane.
#pragma warning disable RSEXPERIMENTAL006
    public static bool IsNativeUnion(ITypeSymbol type) => type.IsUnion;
#pragma warning restore RSEXPERIMENTAL006

    public static bool CompilerExposesUnionSymbols() =>
        !CSharpSyntaxTree.ParseText(
                "public union CompilerProbe(int, string);",
                new CSharpParseOptions(LanguageVersion.CSharp15))
            .GetDiagnostics()
            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
