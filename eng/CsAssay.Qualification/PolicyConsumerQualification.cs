using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CsAssay.Qualification;

internal static class PolicyConsumerQualification
{
    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string message) { if (!condition) { throw new InvalidDataException(message); } }

    public static async Task<int> Run(string root, string output, string tool)
    {
        Require(!Directory.Exists(output), "Use a fresh packaged-policy evidence directory.");
        Directory.CreateDirectory(output);
        var fixture = Path.Combine(output, "AnalyzerConsumer");
        Directory.CreateDirectory(fixture);
        var source = Path.Combine(root, "eng/qualification/packaging/AnalyzerConsumer");
        foreach (var name in new[] { "AnalyzerConsumer.csproj", "Good.cs", "Violation.cs", "UnrelatedWarning.cs", ".csassay.json", "shell.csassay.json" })
        {
            File.Copy(Path.Combine(source, name), Path.Combine(fixture, name));
        }
        var project = Path.Combine(fixture, "AnalyzerConsumer.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("<EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
            "<EnableDefaultCompileItems>false</EnableDefaultCompileItems><QualificationViolation>true</QualificationViolation>", StringComparison.Ordinal));
        var report = new JsonObject { ["schema"] = "csassay-packaged-policy/0.1", ["executionStatus"] = "running", ["sourceCommit"] = (await Execute(["git", "rev-parse", "HEAD"], root)).Text.Trim(), ["cases"] = new JsonArray(), ["scope"] = "Fresh package/tool on this host. Synthetic policy owner is not authenticated human approval; IDE and Windows execution are not qualified." };
        var provenancePath = Path.Combine(root, "artifacts/packages/provenance.json");
        report["packageSourceCommit"] = JsonNode.Parse(File.ReadAllText(provenancePath))!["commit"]!.GetValue<string>();
        report["packageProvenanceSha256"] = Sha(provenancePath);
        void Save() => File.WriteAllText(Path.Combine(output, "qualification.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        async Task<Execution> Case(string name, string[] command, int expected, string contains)
        {
            var result = await Execute(command, root);
            var log = Path.Combine(output, name + ".log");
            File.WriteAllText(log, result.Text);
            var passed = result.ExitCode == expected && (contains.Length == 0 || result.Text.Contains(contains, StringComparison.Ordinal));
            report["cases"]!.AsArray().Add(new JsonObject { ["name"] = name, ["passed"] = passed, ["exitCode"] = result.ExitCode, ["log"] = Path.GetFileName(log), ["logSha256"] = Sha(log) });
            Save();
            Require(passed, "Packaged policy case failed: " + name + ". See retained log.");
            return result;
        }
        string[] Build(string policy) => ["dotnet", "build", project, "--no-restore", "-t:Rebuild", "-c", "Release", "-m:1", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:QualificationViolation=true", "-p:CsAssayPolicyPath=" + policy, "-p:TreatWarningsAsErrors=true"];
        Save();
        try
        {
            await Case("restore", ["dotnet", "restore", project, "--configfile", Path.Combine(root, "eng/qualification/packaging/NuGet.Config")], 0, "");
            var core = Path.Combine(fixture, ".csassay.json");
            var shell = Path.Combine(fixture, "shell.csassay.json");
            await Case("core-build-blocks", Build(core), 1, "error CSAN0004");
            await Case("case-insensitive-enforcement", Build(core).Select(argument => argument == "-p:TreatWarningsAsErrors=true" ? "-p:TreatWarningsAsErrors=false" : argument).Append("-p:CsAssayEnforceOnBuild=True").ToArray(), 1, "error CSAN0004");
            await Case("malformed-enforcement-rejected", Build(core).Append("-p:CsAssayEnforceOnBuild=yes").ToArray(), 1, "CSASSAY-BUILD-GATE-CONFIG");
            await Case("shell-build-advises-under-warnings-as-errors", Build(shell), 0, "warning CSAN0004");
            var originalProject = File.ReadAllText(project);
            var document = System.Xml.Linq.XDocument.Load(project);
            document.Descendants("PropertyGroup").First().Add(new System.Xml.Linq.XElement("CsAssayPolicyPath", shell));
            document.Save(project);
            await Case("project-property-policy-input", Build(shell).Where(argument => !argument.StartsWith("-p:CsAssayPolicyPath=", StringComparison.Ordinal)).ToArray(), 0, "warning CSAN0004");
            File.WriteAllText(project, originalProject);
            var coreReceipt = Path.Combine(output, "core.json");
            var shellReceipt = Path.Combine(output, "shell.json");
            await Case("core-tool-blocks", [tool, "check", project, "--policy", core, "--json", coreReceipt], 1, "error CSAN0004");
            await Case("shell-tool-advises", [tool, "check", project, "--policy", shell, "--json", shellReceipt], 0, "warning CSAN0004");
            var active = JsonNode.Parse(File.ReadAllText(coreReceipt))!["evidence"]!["findings"]!.AsArray().Where(row => row!["ruleId"]!.GetValue<string>() == "CSAN0004").ToArray();
            var shellFindings = JsonNode.Parse(File.ReadAllText(shellReceipt))!["evidence"]!["findings"]!.AsArray().Where(row => row!["ruleId"]!.GetValue<string>() == "CSAN0004").ToArray();
            var fingerprints = active.Select(row => row!["fingerprint"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
            Require(fingerprints.Length > 0 && fingerprints.SequenceEqual(shellFindings.Select(row => row!["fingerprint"]!.GetValue<string>()).Order(StringComparer.Ordinal)), "Boundary classification changed finding identities.");
            var date = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
            var grantPath = Path.Combine(fixture, "grant.csassay.json");
            void Grant(bool mismatch, DateOnly expiry)
            {
                var policy = JsonNode.Parse(File.ReadAllText(core))!.AsObject();
                policy["suppressions"] = JsonSerializer.SerializeToNode(fingerprints.Select(identity => new
                {
                    ruleId = "CSAN0004", owner = "package-fixture-maintainer", reason = "Reviewed nullable persistence transport boundary; no blanket waiver",
                    expires = expiry.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    fingerprint = mismatch ? (Convert.ToInt32(identity[..1], 16) ^ 1).ToString("x", System.Globalization.CultureInfo.InvariantCulture) + identity[1..] : identity
                }).ToArray());
                File.WriteAllText(grantPath, policy.ToJsonString());
            }
            Grant(false, date.AddDays(30));
            await Case("real-grant-build", Build(grantPath), 0, "Build succeeded.");
            var acceptedReceipt = Path.Combine(output, "accepted.json");
            await Case("real-grant-tool", [tool, "check", project, "--policy", grantPath, "--json", acceptedReceipt, "--show-suppressed"], 0, "accepted suppression");
            Require(JsonNode.Parse(File.ReadAllText(acceptedReceipt))!["evidence"]!["suppressions"]!.AsArray().Any(item => item!["authorized"]!.GetValue<bool>()), "Accepted grant evidence missing.");
            File.Copy(grantPath, Path.Combine(output, "accepted-policy.json"));
            Grant(false, date.AddDays(-1));
            await Case("expired-grant-build-blocks", Build(grantPath), 1, "error CSAN0004");
            Grant(true, date.AddDays(30));
            await Case("mismatched-grant-build-blocks", Build(grantPath), 1, "error CSAN0004");
            var invalid = Path.Combine(fixture, "invalid.csassay.json");
            File.WriteAllText(invalid, "{");
            await Case("invalid-policy-build-blocks", Build(invalid), 1, "error CSAP0001");
            await Case("missing-policy-build-blocks", Build(Path.Combine(fixture, "absent.csassay.json")), 1, "error CSAP0001");
            var editorconfig = Path.Combine(fixture, ".editorconfig");
            File.WriteAllText(editorconfig, "root = true\n[*.cs]\ndotnet_diagnostic.CSAN0004.severity = none\ndotnet_diagnostic.CSAP0001.severity = none\n");
            await Case("editorconfig-cannot-hide-integrity", Build(core), 1, "error CSAP0001");
            report["executionStatus"] = "completed";
            report["evaluationDate"] = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            report["realFindingFingerprints"] = JsonSerializer.SerializeToNode(fingerprints);
            Save();
            Console.WriteLine("Fresh packaged policy controls passed; receipt SHA-256 " + Sha(Path.Combine(output, "qualification.json")));
            return 0;
        }
        catch (Exception error) { report["executionStatus"] = "failed"; report["failure"] = error.Message; Save(); throw; }
    }

    private sealed record Execution(int ExitCode, string Text);
    private static async Task<Execution> Execute(string[] command, string root)
    {
        using var process = new Process { StartInfo = new(command[0]) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in command.Skip(1)) { process.StartInfo.ArgumentList.Add(argument); }
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw new InvalidOperationException("Packaged policy command timed out."); }
        return new(process.ExitCode, await stdout + await stderr);
    }
}
