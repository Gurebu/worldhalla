using Worldhalla.Plugin.Core;

namespace Worldhalla.Plugin;

public sealed class CommandLineOptions
{
    public string? worldgenGuid;
    public string? settings;
}

public static class CommandLine
{
    public const string PREFIX = "-wh";

    private static readonly Dictionary<string, Action<CommandLineOptions, string>> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["guid"] = (options, value) => SetOnce(ref options.worldgenGuid, value, "worldgen guids"),
        ["settings"] = (options, value) => SetOnce(ref options.settings, value, "worldgen settings"),
    };

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CommandLineOptions();

        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], PREFIX, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Count)
                throw new ServerStartupException($"'{PREFIX}' is missing a command, expected one of: {string.Join(", ", Commands.Keys)}");

            var command = args[++i];
            if (!Commands.TryGetValue(command, out var apply))
                throw new ServerStartupException($"Unknown command '{PREFIX} {command}', expected one of: {string.Join(", ", Commands.Keys)}");

            if (i + 1 >= args.Count)
                throw new ServerStartupException($"'{PREFIX} {command}' is missing a value");

            apply(options, args[++i]);
        }

        if (options.settings != null && options.worldgenGuid == null)
            throw new ServerStartupException($"'{PREFIX} settings' needs a worldgen to apply to, pass one with '{PREFIX} guid'");

        return options;
    }

    private static void SetOnce(ref string? field, string value, string what)
    {
        if (field != null && field != value)
            throw new ServerStartupException($"Conflicting {what} '{field}' and '{value}'");

        field = value;
    }
}
