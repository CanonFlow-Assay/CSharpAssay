using CsAssay.Domain;
using Microsoft.CodeAnalysis;

namespace CsAssay.SdkAdapter;

public static class DiagnosticProjection
{
    public static SourceSpan ToSourceSpan(Location location)
    {
        if (location == Location.None || !location.IsInSource)
        {
            return SourceSpan.None;
        }

        var span = location.GetLineSpan();
        return new SourceSpan(
            Fingerprints.NormalizePath(span.Path),
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    public static FindingSeverity ToFindingSeverity(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Hidden => FindingSeverity.Hidden,
            DiagnosticSeverity.Info => FindingSeverity.Info,
            DiagnosticSeverity.Warning => FindingSeverity.Warning,
            DiagnosticSeverity.Error => FindingSeverity.Error,
            _ => FindingSeverity.Warning
        };
}
