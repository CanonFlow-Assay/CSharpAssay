using System.Collections.Concurrent;
using System.Collections.Immutable;
using CsAssay.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CsAssay.SdkAdapter;

public sealed record AnalyzerFailure(
    string Analyzer,
    string ExceptionType,
    string Message,
    Presence<string> DiagnosticId);

public sealed record AnalyzerExecutionResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<AnalyzerFailure> Failures);

public static class AnalyzerExecutor
{
    public static async Task<AnalyzerExecutionResult> ExecuteAsync(
        Compilation compilation,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        AnalyzerOptions analyzerOptions,
        CancellationToken cancellationToken)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        var failures = new ConcurrentBag<AnalyzerFailure>();
        var options = new CompilationWithAnalyzersOptions(
            analyzerOptions,
            (exception, analyzer, diagnostic) =>
            {
                failures.Add(new AnalyzerFailure(
                    analyzer.GetType().FullName ?? analyzer.GetType().Name,
                    exception.GetType().FullName ?? exception.GetType().Name,
                    exception.Message,
                    diagnostic is Diagnostic reported
                        ? Presence.Of(reported.Id)
                        : Presence.Missing<string>()));
            },
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);

        var withAnalyzers = compilation.WithAnalyzers(analyzers, options);
        var diagnostics = await withAnalyzers
            .GetAnalyzerDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AnalyzerExecutionResult(
            diagnostics
                .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
                .ThenBy(
                    diagnostic => diagnostic.Location.SourceTree is SyntaxTree tree
                        ? tree.FilePath
                        : string.Empty,
                    StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .ToImmutableArray(),
            failures
                .OrderBy(failure => failure.Analyzer, StringComparer.Ordinal)
                .ThenBy(failure => failure.ExceptionType, StringComparer.Ordinal)
                .ThenBy(failure => failure.Message, StringComparer.Ordinal)
                .ToImmutableArray());
    }
}
