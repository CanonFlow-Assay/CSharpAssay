using System.Text.Json;
using System.Text.Json.Serialization;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class AssessmentWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    public static byte[] Write(AssayVerdict verdict)
    {
        using var receipt = JsonDocument.Parse(JsonEvidenceWriter.Write(verdict));
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            assessment = AssessmentSnapshot.FromVerdict(verdict),
            receipt = receipt.RootElement
        }, Options);
    }

    public static async Task WriteAsync(string path, AssayVerdict verdict, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory());
        await File.WriteAllBytesAsync(path, Write(verdict), cancellationToken).ConfigureAwait(false);
    }
}
