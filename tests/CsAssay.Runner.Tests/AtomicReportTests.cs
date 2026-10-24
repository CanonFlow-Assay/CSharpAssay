using CsAssay.Reporting;

namespace CsAssay.Runner.Tests;

public sealed class AtomicReportTests
{
    [Fact]
    public async Task Staging_failure_preserves_existing_valid_bytes_and_cleans_temporary_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "csassay-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var prior = Path.Combine(root, "valid.json");
            await File.WriteAllTextAsync(prior, "{\"prior\":true}", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<IOException>(() => AtomicReportWriter.WriteAsync(
                [new(prior, [1, 2, 3]), new(root, [4, 5, 6])], TestContext.Current.CancellationToken));
            Assert.Equal("{\"prior\":true}", await File.ReadAllTextAsync(prior, TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFiles(root, ".csassay-*.tmp"));
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AtomicReportWriter.WriteAsync([new(prior, [9])], cancellation.Token));
            Assert.Equal("{\"prior\":true}", await File.ReadAllTextAsync(prior, TestContext.Current.CancellationToken));
            await AtomicReportWriter.WriteAsync([new(prior, [7, 8])], TestContext.Current.CancellationToken);
            Assert.Equal([7, 8], await File.ReadAllBytesAsync(prior, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
