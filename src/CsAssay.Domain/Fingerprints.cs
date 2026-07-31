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
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        using var algorithm = SHA256.Create();
        var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var valueByte in hash)
        {
            builder.Append(valueByte.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static string NormalizePath(string path) =>
        path.Replace('\\', '/');
}
