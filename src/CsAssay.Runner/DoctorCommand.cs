using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CsAssay.Domain;
using CsAssay.Reporting;
using CsAssay.SdkAdapter;
using CsAssay.Workspaces;

namespace CsAssay.Runner;

public sealed record DoctorHostFacts(string ToolVersion, string Runtime, int RuntimeMajor,
    string SelectedSdk, int SdkExitCode, string SdkDetail, string MsBuildInstance, string RoslynVersion, bool UnionSymbols);

public interface IDoctorProbe
{
    Task<DoctorHostFacts> InspectAsync(string contextDirectory, CancellationToken cancellationToken);
}

public sealed class LocalDoctorProbe : IDoctorProbe
{
    public async Task<DoctorHostFacts> InspectAsync(string contextDirectory, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("dotnet")
        { WorkingDirectory = contextDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add("--version");
        // SDK identity only: no restore, workload advertising, first-run setup or telemetry.
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1";
        process.StartInfo.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false";
        process.StartInfo.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "false";
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); throw; }
        var selected = (await stdout.ConfigureAwait(false)).Trim(); var detail = (await stderr.ConfigureAwait(false)).Trim();
        var instance = "unknown (SDK selection did not complete)";
        if (process.ExitCode == 0 && selected.Length > 0)
        {
            var registered = WorkspaceLoader.RegisterMsBuild();
            instance = registered.Name + "; locator version " + registered.Version + "; " + registered.MSBuildPath;
        }
        return new(typeof(CommandApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            RuntimeInformation.FrameworkDescription, Environment.Version.Major, process.ExitCode == 0 ? selected : "", process.ExitCode,
            detail, instance, typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation).Assembly.GetName().Version?.ToString() ?? "unknown",
            UnionCapabilities.CompilerExposesUnionSymbols());
    }
}

public static class DoctorCommand
{
    public const string Conclusion = "Prerequisite check only; application verification has not run. Run verify to establish configured application evidence.";

    public static string SupportedSdk
    {
        get
        {
            using var stream = typeof(DoctorCommand).Assembly.GetManifestResourceStream("CsAssay.SupportedToolchain.json")
                ?? throw new InvalidOperationException("Bundled toolchain policy is missing.");
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.GetProperty("sdk").GetProperty("version").GetString()
                ?? throw new InvalidOperationException("Bundled SDK version is missing.");
        }
    }

    public static async Task<int> RunAsync(CommandLine command, IDoctorProbe probe, TextWriter output, TextWriter error,
        CancellationToken cancellationToken, string? currentDirectory = null)
    {
        output.WriteLine("CSharpAssay doctor");
        try
        {
            var hasInput = command.Input is Presence<string>.Present;
            var input = command.Input is Presence<string>.Present supplied ? Path.GetFullPath(supplied.Value) : Path.GetFullPath(currentDirectory ?? Directory.GetCurrentDirectory());
            var context = hasInput ? Path.GetDirectoryName(input)! : input;
            output.WriteLine("Requested context: " + Clean(input));
            if (hasInput && !File.Exists(input)) return Missing("CSASSAY-DOCTOR-INPUT-MISSING", "Input does not exist; SDK/project context has not been established.");
            if (hasInput && Path.GetExtension(input).ToLowerInvariant() is not (".csproj" or ".sln" or ".slnx"))
                return Missing("CSASSAY-DOCTOR-INPUT-UNSUPPORTED", "Use a C# project, .sln or .slnx input.");
            var facts = await probe.InspectAsync(context, cancellationToken).ConfigureAwait(false);
            output.WriteLine("Tool: " + Clean(facts.ToolVersion));
            output.WriteLine("Running runtime: " + Clean(facts.Runtime));
            output.WriteLine("SDK selected in context: " + (facts.SelectedSdk.Length == 0 ? "unknown/incomplete" : Clean(facts.SelectedSdk)));
            output.WriteLine("SDK resolution directory: " + Clean(context));
            output.WriteLine("Supported exact SDK: " + SupportedSdk);
            output.WriteLine("MSBuild selected process instance: " + Clean(facts.MsBuildInstance));
            output.WriteLine("Roslyn assembly version: " + Clean(facts.RoslynVersion));
            output.WriteLine("Native-union symbol APIs: " + (facts.UnionSymbols ? "present" : "absent") + " (capability observation; compilation/target/language qualification not established)");
            var incomplete = false;
            void Issue(string code, string message) { output.WriteLine(code + ": " + Clean(message)); incomplete = true; }
            if (facts.RuntimeMajor != 11) Issue("CSASSAY-DOCTOR-RUNTIME-UNSUPPORTED", "The tool supports the .NET 11 runtime only.");
            if (facts.SdkExitCode != 0 || facts.SelectedSdk.Length == 0) Issue("CSASSAY-DOCTOR-SDK-UNAVAILABLE", "Required SDK could not be selected. " + facts.SdkDetail);
            else if (facts.SelectedSdk != SupportedSdk) Issue("CSASSAY-DOCTOR-SDK-UNSUPPORTED", "Context selects an unsupported SDK; do not infer SDK identity from the running runtime.");
            if (facts.MsBuildInstance.Length == 0 || facts.MsBuildInstance.StartsWith("unknown", StringComparison.Ordinal)) Issue("CSASSAY-DOCTOR-MSBUILD-UNKNOWN", "MSBuild identity is incomplete.");
            if (facts.RoslynVersion.Length == 0) Issue("CSASSAY-DOCTOR-ROSLYN-UNKNOWN", "Roslyn identity is incomplete.");
            if (!facts.UnionSymbols) Issue("CSASSAY-DOCTOR-UNION-UNAVAILABLE", "Native-union symbol APIs are unavailable.");
            var pin = FindPin(context);
            output.WriteLine("SDK pin: " + (pin.Length == 0 ? "none found (incomplete)" : Clean(pin)));
            if (pin.Length == 0) Issue("CSASSAY-DOCTOR-PIN-MISSING", "An exact supported global.json pin has not been established.");
            else
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllBytes(pin), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    var sdk = document.RootElement.GetProperty("sdk");
                    var version = sdk.GetProperty("version").GetString();
                    var roll = sdk.TryGetProperty("rollForward", out var r) ? r.GetString() : "unspecified";
                    var preview = sdk.TryGetProperty("allowPrerelease", out var p) && p.ValueKind == JsonValueKind.True;
                    output.WriteLine("Declared pin: " + Clean(version ?? "unknown") + "; rollForward=" + Clean(roll ?? "unknown") + "; allowPrerelease=" + preview);
                    if (version != SupportedSdk || roll != "disable" || !preview) Issue("CSASSAY-DOCTOR-PIN-MISMATCH", "Pin does not match the supported exact preview toolchain policy.");
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
                { Issue("CSASSAY-DOCTOR-PIN-INVALID", "SDK pin is malformed: " + exception.Message); }
            }
            var selectedPolicy = PolicyLoader.DiscoverPath(input, command.PolicyPath);
            output.WriteLine("Policy selected: " + (selectedPolicy is Presence<string>.Present found ? Clean(found.Value) : "none; built-in observe"));
            var policy = PolicyLoader.Load(input, command.PolicyPath, requirePolicy: hasInput || command.PolicyPath is Presence<string>.Present);
            foreach (var failure in policy.Failures) Issue(failure.Code, failure.Message);
            if (policy.Failures.IsEmpty)
            {
                output.WriteLine(policy.UsedDefault ? "Policy: built-in observation; no release policy established." : "Policy: parses successfully; selector matches are not established.");
                var boundary = policy.Policy.Boundaries;
                Scope("core projects", boundary.CoreProjects); Scope("shell projects", boundary.ShellProjects);
                Scope("core namespaces", boundary.CoreNamespaces); Scope("shell namespaces", boundary.ShellNamespaces);
            }
            output.WriteLine("Project graph, target frameworks, language settings, restore state, rules and required tests: not evaluated. No restore/build/probe compilation was performed.");
            return incomplete ? 2 : 0;
            void Scope(string name, IEnumerable<string> items) => output.WriteLine("Declared " + name + ": " + Clean(string.Join(", ", items.Order(StringComparer.Ordinal).DefaultIfEmpty("(none)"))));
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync("CSASSAY-DOCTOR-EXECUTION: " + Clean(exception.Message), CancellationToken.None).ConfigureAwait(false);
            return 3;
        }
        finally { output.WriteLine(Conclusion); }
        int Missing(string code, string message) { output.WriteLine(code + ": " + message); return 2; }
    }

    private static string Clean(string text) => DiagnosticPresentation.Sanitize(text);
    private static string FindPin(string context)
    {
        for (var folder = new DirectoryInfo(context); folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, "global.json");
            if (File.Exists(path)) return path;
        }
        return "";
    }
}
