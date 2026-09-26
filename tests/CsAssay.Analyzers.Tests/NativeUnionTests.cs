using CsAssay.Catalogue;
using CsAssay.SdkAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsAssay.Analyzers.Tests;

public sealed class NativeUnionTests
{
    private const string Cases = """
        public sealed record Success(string Text);
        public sealed record Failure(string Message);
        public union Outcome(Success, Failure);
        """;

    [Fact]
    public void Compiler_and_runtime_provide_real_union_support()
    {
        var compilation = Compile(Cases);
        Assert.DoesNotContain(compilation.GetDiagnostics(TestContext.Current.CancellationToken), d => d.Severity == DiagnosticSeverity.Error);
        var type = compilation.GetTypeByMetadataName("Outcome") ??
            throw new InvalidOperationException("Native union symbol is missing.");

        Assert.True(UnionCapabilities.CompilerExposesUnionSymbols());
        Assert.True(UnionCapabilities.IsNativeUnion(type));
        Assert.Contains(type.AllInterfaces, i => i.Name == "IUnion");
    }

    [Fact]
    public async Task Discard_is_reported_but_explicit_cases_are_accepted()
    {
        var source = Cases + """

            public static class Examples
            {
                public static string Hidden(Outcome value) => value switch
                {
                    Success success => success.Text,
                    _ => "hidden"
                };
                public static string Exhaustive(Outcome value) => value switch
                {
                    Success success => success.Text,
                    Failure failure => failure.Message
                };
            }
            """;
        Assert.DoesNotContain(Compile(source).GetDiagnostics(TestContext.Current.CancellationToken), d => d.Severity == DiagnosticSeverity.Error);
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync(source);

        Assert.Single(diagnostics, d => d.Id == RuleIds.NativeUnionDiscard);
    }

    [Fact]
    public void Compiler_reports_an_omitted_case()
    {
        var compilation = Compile(Cases + """

            public static class Examples
            {
                public static string Missing(Outcome value) => value switch
                {
                    Success success => success.Text
                };
            }
            """);
        Assert.Contains(compilation.GetDiagnostics(TestContext.Current.CancellationToken), d => d.Id == "CS8509");
    }

    private static CSharpCompilation Compile(string source)
    {
        var paths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ??
            throw new InvalidOperationException("Runtime references are unavailable.");
        return CSharpCompilation.Create(
            "UnionProbe",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            paths.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
