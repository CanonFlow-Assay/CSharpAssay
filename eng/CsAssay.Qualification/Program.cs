using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsAssay.Qualification;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly string Root = FindRoot();

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var command = args.FirstOrDefault() ?? "qualify";
            if (command == "copy-qualified")
            {
                if (args.Length != 3 || args[1] != "--destination") { throw new ArgumentException("copy-qualified --destination local-feed"); }
                return await CopyQualified(Path.GetFullPath(args[2]));
            }
            var index = Array.IndexOf(args, "--output");
            var output = Path.GetFullPath(index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Root, "artifacts/local-qualification"));
            if (!output.StartsWith(Path.Combine(Root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException("Keep qualification output under ignored artifacts/.");
            }

            Directory.CreateDirectory(command == "package-profile" ? Path.GetDirectoryName(output)! : output);
            return command switch
            {
                "qualify" => await Qualify(output),
                "package-profile" => await PackageProfile(output),
                _ => throw new ArgumentException("Usage: qualify|package-profile [--output path]")
            };
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException or ArgumentException or InvalidOperationException or JsonException)
        {
            Console.Error.WriteLine(failure.Message);
            return 2;
        }
    }

    private static string FindRoot()
    {
        for (var path = new DirectoryInfo(Directory.GetCurrentDirectory()); path is not null; path = path.Parent)
        {
            if (File.Exists(Path.Combine(path.FullName, "CSharpAssay.slnx")))
            {
                return path.FullName;
            }
        }

        throw new InvalidOperationException("Run from the CSharpAssay checkout.");
    }

    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Save(string path, JsonObject value) => File.WriteAllText(path, value.ToJsonString(Json) + "\n");

    private static async Task<int> CopyQualified(string destination)
    {
        var qualification = Path.Combine(Root, "artifacts/csharp-tooling-qualified");
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(qualification, "qualification.json")))!;
        var head = await Git("rev-parse", "HEAD");
        if ((await Git("status", "--porcelain")).Length != 0 || report["sourceCommit"]!.GetValue<string>() != head || report["executionStatus"]!.GetValue<string>() != "completed" || !report["releaseQualified"]!.GetValue<bool>() || !report["sourceUnchanged"]!.GetValue<bool>())
        {
            throw new InvalidDataException("Copy requires the exact clean qualified engine commit.");
        }
        var gates = report["gates"]!.AsArray();
        if (gates.Count != 7) { throw new InvalidDataException("Engine qualification gate set is incomplete."); }
        foreach (var gate in gates)
        {
            var log = Path.GetFullPath(Path.Combine(qualification, gate!["log"]!.GetValue<string>()));
            if (!log.StartsWith(qualification + Path.DirectorySeparatorChar, StringComparison.Ordinal) || gate["state"]!.GetValue<string>() != "Passed" || Sha(log) != gate["logSha256"]!.GetValue<string>())
            {
                throw new InvalidDataException("Engine gate or log integrity failed.");
            }
        }
        var packages = Path.Combine(Root, "artifacts/packages");
        var checksums = File.ReadAllLines(Path.Combine(packages, "checksums.sha256")).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToDictionary(a => a[1], a => a[0], StringComparer.Ordinal);
        Directory.CreateDirectory(destination);
        foreach (var name in new[] { "CsAssay.Analyzers.0.2.0-rc.1.nupkg", "CsAssay.Tool.0.2.0-rc.1.nupkg" })
        {
            if (!checksums.TryGetValue(name, out var hash) || Sha(Path.Combine(packages, name)) != hash) { throw new InvalidDataException("Qualified package checksum differs: " + name); }
            File.Copy(Path.Combine(packages, name), Path.Combine(destination, name), overwrite: true);
        }
        Console.WriteLine("Copied exact qualified package archives without rebuilding the engine.");
        return 0;
    }

    private static async Task<(int Code, string Output, bool TimedOut)> Run(string[] args, string? log = null, bool performance = false)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(args[0]) { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
        foreach (var arg in args.Skip(1))
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (performance)
        {
            process.StartInfo.Environment["CSASSAY_PERFORMANCE_REPORT"] = Path.Combine(Path.GetDirectoryName(log!)!, "performance.json");
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        var timedOut = false;
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { timedOut = true; process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        var text = await stdout + await stderr;
        if (log is not null)
        {
            File.WriteAllText(log, text);
        }

        return (process.ExitCode, text, timedOut);
    }

    private static async Task<string> Git(params string[] args)
    {
        var (Code, Output, _) = await Run(["git", .. args]);
        if (Code != 0)
        {
            throw new InvalidOperationException("Local Git inspection failed.");
        }

        return Output.Trim();
    }

    private static async Task<int> Qualify(string output)
    {
        if ((await Git("status", "--porcelain")).Length != 0)
        {
            throw new InvalidOperationException("Qualification requires the exact clean source commit.");
        }

        var source = await Git("rev-parse", "HEAD");
        var gates = new JsonArray();
        var report = new JsonObject { ["schema"] = "csassay-local-qualification/0.1", ["executionStatus"] = "running", ["sourceCommit"] = source, ["gates"] = gates, ["releaseQualified"] = false, ["publication"] = "none", ["scope"] = "Every required local release gate must pass; failures and logs are retained." };
        var path = Path.Combine(output, "qualification.json");
        Save(path, report);
        (string Name, string[] Command)[] commands = [
            ("restore", ["./eng/restore.sh"]),
            ("build", ["dotnet", "build", "CSharpAssay.slnx", "--no-restore", "-c", "Release"]),
            ("tests", ["dotnet", "test", "CSharpAssay.slnx", "--no-build", "--no-restore", "-c", "Release", "--max-parallel-test-modules", "1"]),
            ("self-assay", ["dotnet", "src/CsAssay.Runner/bin/Release/net11.0/cs-assay.dll", "verify", "CSharpAssay.slnx", "--profile", "native", "--json", Path.Combine(output, "self-assay.json"), "--sarif", Path.Combine(output, "self-assay.sarif")]),
            ("reproducible-pack", ["./eng/pack-release.sh"]),
            ("package-profile-boundaries", ["dotnet", "eng/CsAssay.Qualification/bin/Release/net11.0/CsAssay.Qualification.dll", "package-profile", "--output", Path.Combine(output, "package-profile-boundaries.json")]),
            ("fresh-install", ["./eng/qualify-packages.sh"])];
        foreach (var (name, command) in commands)
        {
            var failedPrerequisite = name != "restore" && gates.Any(g => g!["name"]!.GetValue<string>() is "restore" or "build" && g["state"]!.GetValue<string>() != "Passed");
            failedPrerequisite |= name is "fresh-install" or "package-profile-boundaries" && !gates.Any(g => g!["name"]!.GetValue<string>() == "reproducible-pack" && g["state"]!.GetValue<string>() == "Passed");
            if (failedPrerequisite) { gates.Add(new JsonObject { ["name"] = name, ["state"] = "NotRun", ["reason"] = "Required preceding gate failed." }); Save(path, report); continue; }
            Console.WriteLine("Executing local qualification: " + name);
            var log = Path.Combine(output, name + ".log");
            var watch = Stopwatch.StartNew();
            var (Code, Output, TimedOut) = await Run(command, log, performance: true);
            gates.Add(new JsonObject { ["name"] = name, ["state"] = TimedOut ? "TimedOut" : Code == 0 ? "Passed" : "Failed", ["exitCode"] = Code, ["seconds"] = Math.Round(watch.Elapsed.TotalSeconds, 3), ["command"] = JsonSerializer.SerializeToNode(command), ["log"] = Path.GetFileName(log), ["logSha256"] = Sha(log) });
            Save(path, report);
        }
        var unchanged = await Git("rev-parse", "HEAD") == source && (await Git("status", "--porcelain")).Length == 0;
        var failed = gates.Where(g => g!["state"]!.GetValue<string>() != "Passed").Select(g => g!["name"]!.GetValue<string>()).ToArray();
        report["executionStatus"] = unchanged ? "completed" : "invalid";
        report["sourceUnchanged"] = unchanged;
        report["releaseQualified"] = unchanged && failed.Length == 0;
        report["failedGates"] = JsonSerializer.SerializeToNode(failed);
        Save(path, report);
        return unchanged && failed.Length == 0 ? 0 : 1;
    }

    private static async Task<int> PackageProfile(string output)
    {
        var commit = await Git("rev-parse", "HEAD");
        var sdk = (await Run(["dotnet", "--version"])).Output.Trim();
        var packages = Path.Combine(Root, "artifacts/packages");
        const string toolName = "CsAssay.Tool.0.2.0-rc.1.nupkg";
        var results = new JsonArray();
        var scratch = Directory.CreateTempSubdirectory("csassay-package-profile-");
        try
        {
            foreach (var mode in new[] { "enabled", "missing" })
            {
                var folder = Directory.CreateDirectory(Path.Combine(scratch.FullName, mode)).FullName;
                var altered = Path.Combine(folder, toolName);
                using (var original = ZipFile.OpenRead(Path.Combine(packages, toolName)))
                using (var candidate = ZipFile.Open(altered, ZipArchiveMode.Create))
                {
                    foreach (var entry in original.Entries)
                    {
                        using var source = entry.Open();
                        using var target = candidate.CreateEntry(entry.FullName).Open();
                        if (entry.FullName == "tools/net11.0/any/cs-assay.runtimeconfig.json")
                        {
                            var config = JsonNode.Parse(source)!;
                            var props = config["runtimeOptions"]!["configProperties"]!.AsObject();
                            if (mode == "enabled")
                            {
                                props["System.Runtime.TieredCompilation"] = true;
                            }
                            else
                            {
                                props.Remove("System.Runtime.TieredCompilation");
                            }

                            await target.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(config));
                        }
                        else
                        {
                            await source.CopyToAsync(target);
                        }
                    }
                }

                var (Code, Output, TimedOut) = await Run(["dotnet", "eng/CsAssay.PackageAudit/bin/Release/net11.0/CsAssay.PackageAudit.dll", Path.Combine(folder, "audit"), commit, sdk, "unsigned", "preserve", Path.Combine(packages, "CsAssay.Analyzers.0.2.0-rc.1.nupkg"), altered]);
                if (Code == 0 || !Output.Contains("qualified optimized JIT profile", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Package rejection failed for: " + mode);
                }

                results.Add(new JsonObject { ["case"] = mode, ["rejected"] = true, ["exitCode"] = Code });
            }
        }
        finally { scratch.Delete(recursive: true); }
        Save(output, new JsonObject { ["schema"] = "csassay-package-profile-boundaries/0.1", ["executionStatus"] = "completed", ["results"] = results });
        Console.WriteLine("Package profile boundaries: two altered packages rejected for the expected reason.");
        return 0;
    }
}
