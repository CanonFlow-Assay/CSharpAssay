using System.Collections.Immutable;

namespace CsAssay.Reporting;

public sealed record RequestedReport(string Path, byte[] Content);

/// <summary>Stage every output, then atomically replace each file. This is not a multi-file transaction.</summary>
public static class AtomicReportWriter
{
    public static async Task WriteAsync(IEnumerable<RequestedReport> reports, CancellationToken cancellationToken)
    {
        var requested = reports.Select(report => report with { Path = System.IO.Path.GetFullPath(report.Path) }).ToImmutableArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (requested.Select(report => report.Path).Distinct(comparer).Count() != requested.Length)
        {
            throw new ArgumentException("Requested report paths must be distinct.");
        }
        var staged = new List<(string Temporary, string Destination)>();
        try
        {
            foreach (var report in requested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = System.IO.Path.GetDirectoryName(report.Path)!;
                Directory.CreateDirectory(directory);
                if (Directory.Exists(report.Path)) { throw new IOException("Requested report path is a directory: " + report.Path); }
                var temporary = System.IO.Path.Combine(directory, ".csassay-" + Guid.NewGuid().ToString("N") + ".tmp");
                staged.Add((temporary, report.Path));
                await using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                await stream.WriteAsync(report.Content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            foreach (var (temporary, destination) in staged)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, destination, overwrite: true);
            }
        }
        finally
        {
            foreach (var (temporary, _) in staged)
            {
                try { File.Delete(temporary); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { /* Keep the original output failure. */ }
            }
        }
    }
}
