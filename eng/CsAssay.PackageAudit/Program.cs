using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace CsAssay.PackageAudit;

public static class Program
{
    private const string AnalyzerPackagePrefix = "CsAssay.Analyzers.";
    private const string ToolPackagePrefix = "CsAssay.Tool.";
    private const string CorePropertiesPath =
        "package/services/metadata/core-properties/core-properties.psmdcp";

    public static int Main(string[] args)
    {
        if (args.Length < 6)
        {
            Console.Error.WriteLine(
                "Usage: CsAssay.PackageAudit <output-directory> <commit> <sdk> <signed|unsigned> <normalize|preserve> <packages...>");
            return 64;
        }

        try
        {
            var outputDirectory = Path.GetFullPath(args[0]);
            var commit = RequireValue(args[1], "commit");
            var sdk = RequireValue(args[2], "sdk");
            var requireSigned = args[3] switch
            {
                "signed" => true,
                "unsigned" => false,
                _ => throw new InvalidDataException(
                    "Signing mode must be signed or unsigned.")
            };
            var normalize = args[4] switch
            {
                "normalize" => true,
                "preserve" => false,
                _ => throw new InvalidDataException(
                    "Archive mode must be normalize or preserve.")
            };
            if (normalize && requireSigned)
            {
                throw new InvalidDataException(
                    "Signed packages cannot be normalized after signing.");
            }

            var artifacts = args
                .Skip(5)
                .Select(Path.GetFullPath)
                .OrderBy(path => new FileInfo(path).Name, StringComparer.Ordinal)
                .Select(path => NormalizeWhenRequested(path, normalize))
                .Select(path => Audit(path, commit, requireSigned))
                .ToArray();
            if (artifacts.Length != 2)
            {
                throw new InvalidDataException(
                    "Exactly the analyzer and tool packages must be audited.");
            }

            Directory.CreateDirectory(outputDirectory);
            WriteChecksums(outputDirectory, artifacts);
            WriteProvenance(
                outputDirectory,
                commit,
                sdk,
                requireSigned,
                artifacts);
            foreach (var artifact in artifacts)
            {
                Console.WriteLine(
                    artifact.Name + " " + artifact.Sha256 + " " +
                    artifact.EntryCount + " entries");
            }

            return 0;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
                InvalidDataException or
                InvalidOperationException or
                IOException or
                XmlException or
                UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Package audit failed: " + exception.Message);
            return 1;
        }
    }

    private static Artifact Audit(
        string packagePath,
        string commit,
        bool requireSigned)
    {
        if (!File.Exists(packagePath))
        {
            throw new InvalidDataException(
                "Package does not exist: " + packagePath);
        }

        var name = new FileInfo(packagePath).Name;
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();
        foreach (var entry in entries)
        {
            RejectUnsafeEntry(name, entry);
        }

        var packageKind = name.StartsWith(
            AnalyzerPackagePrefix,
            StringComparison.Ordinal)
            ? PackageKind.Analyzer
            : name.StartsWith(ToolPackagePrefix, StringComparison.Ordinal)
                ? PackageKind.Tool
                : throw new InvalidDataException(
                    "Unexpected package identity: " + name);
        var expectedName = packageKind == PackageKind.Analyzer
            ? "CsAssay.Analyzers.0.1.1.nupkg"
            : "CsAssay.Tool.0.1.1.nupkg";
        if (!string.Equals(name, expectedName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Package filename must be " + expectedName + ".");
        }

        if (packageKind == PackageKind.Analyzer)
        {
            RequireEntries(
                name,
                entries,
                "README.md",
                "analyzers/dotnet/cs/CsAssay.Analyzers.dll",
                "analyzers/dotnet/cs/CsAssay.Catalogue.dll",
                "analyzers/dotnet/cs/CsAssay.Domain.dll",
                "analyzers/dotnet/cs/CsAssay.SdkAdapter.dll",
                "buildTransitive/CsAssay.Analyzers.props",
                "buildTransitive/CsAssay.Analyzers.targets");
        }
        else
        {
            RequireEntries(
                name,
                entries,
                "README.md",
                "tools/net10.0/any/DotnetToolSettings.xml",
                "tools/net10.0/any/cs-assay.dll",
                "tools/net10.0/any/CsAssay.Analyzers.dll",
                "tools/net10.0/any/CsAssay.Workspaces.dll");
        }

        VerifyPackageMetadata(archive, packageKind, commit);

        var signed = entries.Contains(
            ".signature.p7s",
            StringComparer.OrdinalIgnoreCase);
        if (signed != requireSigned)
        {
            throw new InvalidDataException(
                name + " signing state does not match release policy.");
        }

        using var stream = File.OpenRead(packagePath);
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        return new Artifact(name, hash, entries.Length, signed);
    }

    private static void VerifyPackageMetadata(
        ZipArchive archive,
        PackageKind packageKind,
        string commit)
    {
        var nuspecEntries = archive.Entries
            .Where(entry => entry.FullName.EndsWith(
                ".nuspec",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (nuspecEntries.Length != 1)
        {
            throw new InvalidDataException(
                "Package must contain exactly one NuGet manifest.");
        }

        using var stream = nuspecEntries[0].Open();
        var document = XDocument.Load(stream, LoadOptions.None);
        var root = document.Root is XElement presentRoot
            ? presentRoot
            : throw new InvalidDataException(
                "NuGet manifest has no root element.");
        var metadata = root.Elements().SingleOrDefault(
            element => element.Name.LocalName == "metadata") is XElement presentMetadata
            ? presentMetadata
            : throw new InvalidDataException(
                "NuGet manifest has no metadata element.");
        var expectedId = packageKind == PackageKind.Analyzer
            ? "CsAssay.Analyzers"
            : "CsAssay.Tool";
        RequireMetadataValue(metadata, "id", expectedId);
        RequireMetadataValue(metadata, "version", "0.1.1");
        RequireMetadataValue(metadata, "readme", "README.md");

        var license = RequireMetadataElement(metadata, "license");
        var licenseType = license.Attribute("type") is XAttribute presentType
            ? presentType.Value
            : throw new InvalidDataException(
                expectedId + " license metadata has no type attribute.");
        if (!string.Equals(
                licenseType,
                "expression",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                license.Value,
                "Apache-2.0",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                expectedId + " must declare the Apache-2.0 license expression.");
        }

        var repository = RequireMetadataElement(metadata, "repository");
        RequireAttributeValue(repository, "type", "git", expectedId);
        RequireAttributeValue(
            repository,
            "url",
            "https://github.com/CanonFlow-Assay/CSharpAssay",
            expectedId);
        RequireAttributeValue(repository, "commit", commit, expectedId);
    }

    private static XElement RequireMetadataElement(
        XElement metadata,
        string localName) =>
        metadata.Elements().SingleOrDefault(
            element => element.Name.LocalName == localName)
        ?? throw new InvalidDataException(
            "NuGet manifest is missing metadata: " + localName);

    private static void RequireMetadataValue(
        XElement metadata,
        string localName,
        string expected)
    {
        var element = RequireMetadataElement(metadata, localName);
        if (!string.Equals(element.Value, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "NuGet metadata " + localName + " must be " + expected + ".");
        }
    }

    private static void RequireAttributeValue(
        XElement element,
        string localName,
        string expected,
        string package)
    {
        var actual = element.Attributes().SingleOrDefault(
            attribute => attribute.Name.LocalName == localName);
        if (actual is not XAttribute present ||
            !string.Equals(present.Value, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                package + " repository " + localName +
                " must be " + expected + ".");
        }
    }

    private static string NormalizeWhenRequested(
        string packagePath,
        bool normalize)
    {
        if (normalize)
        {
            NormalizePackage(packagePath);
        }

        return packagePath;
    }

    private static void NormalizePackage(string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            throw new InvalidDataException(
                "Package does not exist: " + packagePath);
        }

        ArchivedEntry[] entries;
        using (var source = ZipFile.OpenRead(packagePath))
        {
            var rawEntries = source.Entries
                .Select(ReadEntry)
                .ToArray();
            if (rawEntries.Count(entry => entry.Name.EndsWith(
                    ".psmdcp",
                    StringComparison.OrdinalIgnoreCase)) != 1)
            {
                throw new InvalidDataException(
                    "Package must contain exactly one core-properties entry: " +
                        packagePath);
            }

            entries = rawEntries
                .Select(CanonicalizeOpcEntry)
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray();
        }

        if (entries.Select(entry => entry.Name)
            .Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            throw new InvalidDataException(
                "Package contains duplicate ZIP entry names: " + packagePath);
        }

        var temporaryPath = packagePath + ".canonical";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        try
        {
            using (var stream = File.Create(temporaryPath))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Create,
                       leaveOpen: false))
            {
                foreach (var entry in entries)
                {
                    var created = archive.CreateEntry(
                        entry.Name,
                        CompressionLevel.Optimal);
                    created.LastWriteTime = new DateTimeOffset(
                        1980,
                        1,
                        1,
                        0,
                        0,
                        0,
                        TimeSpan.Zero);
                    created.ExternalAttributes = 0;
                    using var destination = created.Open();
                    destination.Write(entry.Content);
                }
            }

            File.Move(temporaryPath, packagePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static ArchivedEntry ReadEntry(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        using var content = new MemoryStream();
        source.CopyTo(content);
        return new ArchivedEntry(
            entry.FullName.Replace('\\', '/'),
            content.ToArray());
    }

    private static ArchivedEntry CanonicalizeOpcEntry(ArchivedEntry entry)
    {
        if (entry.Name.EndsWith(
                ".psmdcp",
                StringComparison.OrdinalIgnoreCase))
        {
            return new ArchivedEntry(CorePropertiesPath, entry.Content);
        }

        return string.Equals(
            entry.Name,
            "_rels/.rels",
            StringComparison.OrdinalIgnoreCase)
            ? new ArchivedEntry(
                entry.Name,
                CanonicalizeRelationships(entry.Content))
            : entry;
    }

    private static byte[] CanonicalizeRelationships(byte[] content)
    {
        using var input = new MemoryStream(content, writable: false);
        var document = XDocument.Load(input, LoadOptions.None);
        var root = document.Root is XElement present
            ? present
            : throw new InvalidDataException(
                "Package relationships document has no root element.");
        var relationships = root.Elements()
            .OrderBy(RelationshipType, StringComparer.Ordinal)
            .ThenBy(RelationshipTarget, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < relationships.Length; index++)
        {
            var relationship = relationships[index];
            relationship.SetAttributeValue(
                "Id",
                "R" + (index + 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            if (RelationshipType(relationship).EndsWith(
                    "/metadata/core-properties",
                    StringComparison.Ordinal))
            {
                relationship.SetAttributeValue(
                    "Target",
                    "/" + CorePropertiesPath);
            }
        }

        root.ReplaceNodes(relationships);
        using var output = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace
        };
        using (var writer = XmlWriter.Create(output, settings))
        {
            document.Save(writer);
        }

        return output.ToArray();
    }

    private static string RelationshipType(XElement relationship) =>
        relationship.Attribute("Type") is XAttribute value
            ? value.Value
            : throw new InvalidDataException(
                "Package relationship has no Type attribute.");

    private static string RelationshipTarget(XElement relationship) =>
        relationship.Attribute("Target") is XAttribute value
            ? value.Value
            : throw new InvalidDataException(
                "Package relationship has no Target attribute.");

    private static void RejectUnsafeEntry(string package, string entry)
    {
        var normalized = "/" + entry.ToLowerInvariant();
        var hasTraversalSegment = entry
            .Split('/')
            .Any(segment => segment is "." or "..");
        if (entry.Length > 0 && entry[0] == '/' ||
            entry.Contains(':') ||
            hasTraversalSegment ||
            normalized.Contains("/inspire/", StringComparison.Ordinal) ||
            normalized.Contains("functional-csharp-code", StringComparison.Ordinal) ||
            normalized.EndsWith(".cs", StringComparison.Ordinal) ||
            normalized.Contains("/.git/", StringComparison.Ordinal) ||
            normalized.EndsWith("/.env", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                package + " contains prohibited entry: " + entry);
        }
    }

    private static void RequireEntries(
        string package,
        string[] entries,
        params string[] required)
    {
        foreach (var expected in required)
        {
            if (!entries.Contains(expected, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    package + " is missing required entry: " + expected);
            }
        }
    }

    private static void WriteChecksums(
        string outputDirectory,
        Artifact[] artifacts)
    {
        var content = string.Join(
            "\n",
            artifacts.Select(artifact =>
                artifact.Sha256 + "  " + artifact.Name)) + "\n";
        File.WriteAllText(
            Path.Combine(outputDirectory, "checksums.sha256"),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteProvenance(
        string outputDirectory,
        string commit,
        string sdk,
        bool signed,
        Artifact[] artifacts)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("commit", commit);
            writer.WriteString("sdk", sdk);
            writer.WriteString("configuration", "Release");
            writer.WriteBoolean("nugetSigned", signed);
            writer.WritePropertyName("artifacts");
            writer.WriteStartArray();
            foreach (var artifact in artifacts)
            {
                writer.WriteStartObject();
                writer.WriteString("name", artifact.Name);
                writer.WriteString("sha256", artifact.Sha256);
                writer.WriteNumber("entries", artifact.EntryCount);
                writer.WriteBoolean("nugetSigned", artifact.Signed);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        File.WriteAllBytes(
            Path.Combine(outputDirectory, "provenance.json"),
            Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(stream.ToArray()) + "\n"));
    }

    private static string RequireValue(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException(name + " cannot be empty.")
            : value;

    private enum PackageKind
    {
        Analyzer,
        Tool
    }

    private sealed record Artifact(
        string Name,
        string Sha256,
        int EntryCount,
        bool Signed);

    private sealed class ArchivedEntry
    {
        public ArchivedEntry(string name, byte[] content)
        {
            Name = name;
            Content = content;
        }

        public string Name { get; }

        public byte[] Content { get; }
    }
}
