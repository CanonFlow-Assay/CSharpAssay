using System.Collections.Immutable;
using CsAssay.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.Analyzers.Tests;

internal sealed record AnalyzerSource(string Path, string Text);

internal static class AnalyzerTestHost
{
    public static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        AnalyzeAsync(source, new Dictionary<string, string>());

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        IReadOnlyDictionary<string, string> options,
        NullableContextOptions nullableContext =
            NullableContextOptions.Enable,
        bool concurrentAnalysis = true) =>
        await AnalyzeAsync(
            [new AnalyzerSource("Test.cs", source)],
            options,
            nullableContext,
            concurrentAnalysis);

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        ImmutableArray<AnalyzerSource> sources,
        IReadOnlyDictionary<string, string> options,
        NullableContextOptions nullableContext =
            NullableContextOptions.Enable,
        bool concurrentAnalysis = true)
    {
        var syntaxTrees = sources.Select(source =>
            CSharpSyntaxTree.ParseText(
                source.Text,
                new CSharpParseOptions(LanguageVersion.CSharp14),
                path: source.Path));
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            syntaxTrees,
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullableContext));
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestOptionsProvider(options));
        var withAnalyzers = compilation.WithAnalyzers(
            [new FunctionalPolicyAnalyzer()],
            new CompilationWithAnalyzersOptions(
                analyzerOptions,
                onAnalyzerException: (_, _, _) => { },
                concurrentAnalysis: concurrentAnalysis,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true));

        return (await withAnalyzers.GetAnalyzerDiagnosticsAsync())
            .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> References { get; } =
        AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths
            ? paths
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToImmutableArray<MetadataReference>()
            : throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");

    private sealed class TestOptionsProvider(
        IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty =
            new TestOptions(new Dictionary<string, string>());
        private readonly AnalyzerConfigOptions global = new TestOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class TestOptions(
        IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var found) &&
                found is string required)
            {
                value = required;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
