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
                    report = true;
                    break;
                case "--json":
                    json = Presence.Of(Next(args, ref index, argument));
                    break;
                case "--sarif":
                    sarif = Presence.Of(Next(args, ref index, argument));
                    break;
                case "--html":
                    html = Presence.Of(Next(args, ref index, argument));
                    break;
                case "--policy":
                    policy = Presence.Of(Next(args, ref index, argument));
                    break;
                case "--profile":
                    profile = Presence.Of(
                        ParseProfile(Next(args, ref index, argument)));
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

        return new CommandLine(
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
    }

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
