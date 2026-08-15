namespace UloManager.Cli;

/// <summary>Minimal argument parser: global options, a command and positional arguments.</summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positional = new();

    private CommandLine()
    {
    }

    public string Command { get; private set; } = "";

    public IReadOnlyList<string> Positional => _positional;

    public static CommandLine Parse(string[] args)
    {
        var result = new CommandLine();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var name = arg[2..];
                var eq = name.IndexOf('=');

                if (eq >= 0)
                {
                    result._options[name[..eq]] = name[(eq + 1)..];
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    result._options[name] = args[++i];
                }
                else
                {
                    result._options[name] = "true";
                }
            }
            else if (result.Command.Length == 0)
            {
                result.Command = arg.ToLowerInvariant();
            }
            else
            {
                result._positional.Add(arg);
            }
        }

        return result;
    }

    public string? GetOption(string name, string? fallback = null)
        => _options.TryGetValue(name, out var value) ? value : fallback;

    public bool HasFlag(string name)
        => _options.TryGetValue(name, out var value) &&
           (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Length == 0);

    public int GetInt(string name, int fallback)
        => int.TryParse(GetOption(name), out var value) ? value : fallback;

    public string GetPositional(int index, string? fallback = null)
        => index < _positional.Count
            ? _positional[index]
            : fallback ?? throw new ArgumentException($"Missing argument #{index + 1}.");
}
