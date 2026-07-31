using System.Collections.Immutable;
using System.Diagnostics;
using CsAssay.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.Performance.Tests;

public sealed class AnalyzerPerformanceTests
{
    private const double RepresentativeDocumentP95BudgetMilliseconds = 50;

    [Fact]
    public async Task Representative_document_p95_stays_within_budget()
    {
        var compilation = CreateCompilation();

        for (var index = 0; index < 3; index++)
        {
            await AnalyzeAsync(compilation);
        }

        var samples = new double[20];
        for (var index = 0; index < samples.Length; index++)
        {
            var started = Stopwatch.GetTimestamp();
            await AnalyzeAsync(compilation);
            samples[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        Array.Sort(samples);
        var p95 = samples[(int)Math.Ceiling(samples.Length * 0.95) - 1];

        Assert.True(
            p95 < RepresentativeDocumentP95BudgetMilliseconds,
            "Analyzer p95 was " +
                p95.ToString(
                    "F3",
                    System.Globalization.CultureInfo.InvariantCulture) +
                " ms; budget is " +
                RepresentativeDocumentP95BudgetMilliseconds.ToString(
                    "F3",
                    System.Globalization.CultureInfo.InvariantCulture) +
                " ms.");
    }

    [Fact]
    public async Task Repeated_analysis_is_diagnostic_deterministic()
    {
        var compilation = CreateCompilation();

        var first = Project(await AnalyzeAsync(compilation));
        for (var index = 0; index < 10; index++)
        {
            Assert.Equal(first, Project(await AnalyzeAsync(compilation)));
        }
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        CSharpCompilation compilation)
    {
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            onAnalyzerException: (_, _, _) => { },
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);
        return await compilation
            .WithAnalyzers([new FunctionalPolicyAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation()
    {
        var source = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 40).Select(index =>
                "public sealed record Value" +
                index.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                "(string Name, System.Collections.Immutable.ImmutableArray<int> Items);"));
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.CSharp14),
            path: "Representative.cs");
        return CSharpCompilation.Create(
            "Representative",
            [tree],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static string[] Project(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics
            .Select(diagnostic => string.Join(
                "|",
                diagnostic.Id,
                diagnostic.Location.SourceSpan.Start.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                diagnostic.GetMessage(
                    System.Globalization.CultureInfo.InvariantCulture)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static ImmutableArray<MetadataReference> References { get; } =
        AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths
            ? paths
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToImmutableArray<MetadataReference>()
            : throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");
}
