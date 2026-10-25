using CsAssay.Domain;
using CsAssay.Reporting;

namespace CsAssay.Runner;

public sealed record CommandLine(
    string Command,
    Presence<string> Input,
    Presence<string> RuleId,
    Presence<string> JsonPath,
    Presence<string> SarifPath,
    Presence<string> HtmlPath,
    Presence<string> PolicyPath,
    Presence<AssayProfile> Profile,
    bool Help,
    ConsoleFormat Format = ConsoleFormat.Msbuild,
    bool ShowSuppressed = false,
    bool FormatSpecified = false)
{
    public Presence<string> MarkdownPath { get; init; } = Presence.Missing<string>();

    public static CommandLine Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CommandLine(
                "help",
                Presence.Missing<string>(),
                Presence.Missing<string>(),
                Presence.Missing<string>(),
                Presence.Missing<string>(),
                Presence.Missing<string>(),
                Presence.Missing<string>(),
                Presence.Missing<AssayProfile>(),
                true);
        }

        var command = args[0] switch
        {
            "-h" or "--help" => "help",
            _ => args[0].ToLowerInvariant()
        };
        Presence<string> input = Presence.Missing<string>();
        Presence<string> ruleId = Presence.Missing<string>();
        Presence<string> json = Presence.Missing<string>();
        Presence<string> sarif = Presence.Missing<string>();
        Presence<string> html = Presence.Missing<string>();
        Presence<string> markdown = Presence.Missing<string>();
        Presence<string> policy = Presence.Missing<string>();
        Presence<AssayProfile> profile = Presence.Missing<AssayProfile>();
        var help = false;
        Presence<string> format = Presence.Missing<string>();
        var showSuppressed = false;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "--format":
                    format = Once(format, Next(args, ref index, argument), argument);
                    break;
                case "--show-suppressed":
                    if (showSuppressed) { throw new ArgumentException("Duplicate option: " + argument); }
                    showSuppressed = true;
                    break;
                case "--json":
                    json = Once(
                        json,
                        Next(args, ref index, argument),
                        argument);
                    break;
                case "--sarif":
                    sarif = Once(
                        sarif,
                        Next(args, ref index, argument),
                        argument);
                    break;
                case "--html":
                    html = Once(
                        html,
                        Next(args, ref index, argument),
                        argument);
                    break;
                case "--markdown":
                    markdown = Once(markdown, Next(args, ref index, argument), argument);
                    break;
                case "--policy":
                    policy = Once(
                        policy,
                        Next(args, ref index, argument),
                        argument);
                    break;
                case "--profile":
                    profile = Once(
                        profile,
                        ParseProfile(Next(args, ref index, argument)),
                        argument);
                    break;
                default:
                    if (argument.StartsWith('-'))
                    {
                        throw new ArgumentException("Unknown option: " + argument);
                    }

                    if (string.Equals(command, "explain", StringComparison.Ordinal) &&
                        ruleId is Presence<string>.Absent)
                    {
                        ruleId = Presence.Of(argument);
                    }
                    else
                    {
                        input = input is Presence<string>.Absent
                            ? Presence.Of(argument)
                            : throw new ArgumentException(
                                "Unexpected positional argument: " + argument);
                    }

                    break;
            }
        }

        var result = new CommandLine(
            command,
            input,
            ruleId,
            json,
            sarif,
            html,
            policy,
            profile,
            help,
            format switch
            {
                Presence<string>.Present { Value: "msbuild" } => ConsoleFormat.Msbuild,
                Presence<string>.Present { Value: "text" } => ConsoleFormat.Text,
                Presence<string>.Absent => ConsoleFormat.Msbuild,
                _ => throw new ArgumentException("--format must be msbuild or text.")
            },
            showSuppressed,
            format is Presence<string>.Present) { MarkdownPath = markdown };
        ValidateCommandShape(result);
        return result;
    }

    private static Presence<T> Once<T>(
        Presence<T> current,
        T value,
        string option)
        where T : notnull =>
        current is Presence<T>.Present
            ? throw new ArgumentException("Duplicate option: " + option)
            : Presence.Of(value);

    private static void ValidateCommandShape(CommandLine commandLine)
    {
        if (commandLine.Help)
        {
            return;
        }

        if ((commandLine.FormatSpecified || commandLine.ShowSuppressed) && commandLine.Command is not ("check" or "verify" or "assess"))
        {
            throw new ArgumentException("--format and --show-suppressed apply to check, verify and assess only.");
        }
        var invalid = commandLine.Command switch
        {
            "doctor" when
                Has(commandLine.RuleId) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.Profile) =>
                "doctor accepts an optional project/solution and --policy only.",
            "catalog" when
                Has(commandLine.Input) ||
                Has(commandLine.RuleId) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.PolicyPath) =>
                "catalog accepts only --profile.",
            "explain" when
                Has(commandLine.Input) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.PolicyPath) ||
                Has(commandLine.Profile) =>
                "explain accepts exactly one rule ID.",
            "check" or "verify" or "assess" when
                Has(commandLine.RuleId) =>
                commandLine.Command +
                    " accepts an input and verification options only.",
            "help" when
                Has(commandLine.Input) ||
                Has(commandLine.RuleId) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.PolicyPath) ||
                Has(commandLine.Profile) =>
                "help does not accept arguments.",
            _ => string.Empty
        };

        if (invalid.Length > 0)
        {
            throw new ArgumentException(invalid);
        }
    }

    private static bool HasAnyOutput(CommandLine commandLine) =>
        Has(commandLine.JsonPath) ||
        Has(commandLine.SarifPath) ||
        Has(commandLine.HtmlPath) || Has(commandLine.MarkdownPath);

    private static bool Has<T>(Presence<T> value)
        where T : notnull =>
        value is Presence<T>.Present;

    private static string Next(string[] args, ref int index, string option)
    {
        index++;
        if (index >= args.Length)
        {
            throw new ArgumentException(option + " requires a value.");
        }

        return args[index];
    }

    private static AssayProfile ParseProfile(string value) =>
        value.ToLowerInvariant() switch
        {
            "native" => AssayProfile.Native,
            _ => throw new ArgumentException(
                "--profile must be native; .NET 11 is required.")
        };
}
