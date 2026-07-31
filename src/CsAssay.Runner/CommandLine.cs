using CsAssay.Domain;

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
    bool Report,
    bool Help)
{
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
                false,
                true);
        }

        var command = args[0].ToLowerInvariant();
        Presence<string> input = Presence.Missing<string>();
        Presence<string> ruleId = Presence.Missing<string>();
        Presence<string> json = Presence.Missing<string>();
        Presence<string> sarif = Presence.Missing<string>();
        Presence<string> html = Presence.Missing<string>();
        Presence<string> policy = Presence.Missing<string>();
        Presence<AssayProfile> profile = Presence.Missing<AssayProfile>();
        var report = false;
        var help = false;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "--report":
                    if (report)
                    {
                        throw new ArgumentException(
                            "Duplicate option: " + argument);
                    }

                    report = true;
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
                    else if (input is Presence<string>.Absent)
                    {
                        input = Presence.Of(argument);
                    }
                    else
                    {
                        throw new ArgumentException(
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
            report,
            help);
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

        var invalid = commandLine.Command switch
        {
            "doctor" when
                Has(commandLine.Input) ||
                Has(commandLine.RuleId) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.PolicyPath) ||
                Has(commandLine.Profile) ||
                commandLine.Report =>
                "doctor does not accept arguments.",
            "catalog" when
                Has(commandLine.Input) ||
                Has(commandLine.RuleId) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.PolicyPath) ||
                commandLine.Report =>
                "catalog accepts only --profile.",
            "explain" when
                Has(commandLine.Input) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.PolicyPath) ||
                Has(commandLine.Profile) ||
                commandLine.Report =>
                "explain accepts exactly one rule ID.",
            "check" or "verify" when
                Has(commandLine.RuleId) ||
                commandLine.Report =>
                commandLine.Command +
                    " accepts an input and verification options only.",
            "migrate" when
                Has(commandLine.RuleId) ||
                Has(commandLine.SarifPath) ||
                Has(commandLine.HtmlPath) ||
                Has(commandLine.PolicyPath) ||
                Has(commandLine.Profile) =>
                "migrate accepts --report and --json only.",
            "help" when
                Has(commandLine.Input) ||
                Has(commandLine.RuleId) ||
                HasAnyOutput(commandLine) ||
                Has(commandLine.PolicyPath) ||
                Has(commandLine.Profile) ||
                commandLine.Report =>
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
        Has(commandLine.HtmlPath);

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
            "auto" => AssayProfile.Auto,
            "compat" => AssayProfile.Compat,
            "native" => AssayProfile.Native,
            _ => throw new ArgumentException(
                "--profile must be auto, compat, or native.")
        };
}
