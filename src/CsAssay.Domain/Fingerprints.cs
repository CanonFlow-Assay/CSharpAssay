using System.Security.Cryptography;
using System.Text;

namespace CsAssay.Domain;

public static class Fingerprints
{
    public static string Finding(
        string ruleId,
        string project,
        string targetFramework,
        SourceSpan location,
        string message)
    {
        return Sha256(string.Join(
            "\n",
            ruleId,
            project,
            targetFramework,
            NormalizePath(location.Path),
            location.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            location.StartColumn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            message));
    }

    public static string Sha256(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static string NormalizePath(string path) =>
        path.Replace('\\', '/');
}
