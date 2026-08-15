namespace UloManager.Gui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Light, dark, or whatever Windows is doing (the default). The colour mode has to be set
        // before any window exists, which is why a change of preference asks for a restart.
        var settings = UloManager.Core.UloSettings.Load();
        Theme.Preference = Theme.Parse(settings.Theme);

#pragma warning disable WFO5001
        try
        {
            Application.SetColorMode(Theme.Preference switch
            {
                Theme.Mode.Light => SystemColorMode.Classic,
                Theme.Mode.Dark => SystemColorMode.Dark,
                _ => SystemColorMode.System,
            });
        }
        catch (Exception)
        {
            // Older Windows builds without the dark mode support - stay light.
        }
#pragma warning restore WFO5001

        Application.Run(new MainForm(LaunchOptions.Parse(args)));
    }
}

/// <summary>
/// Optional launch parameters so the window can be started pre-filled, e.g. from a shortcut:
/// <c>UloManager.exe --host 192.168.0.10 --user admin@example.com --password secret --connect</c>.
/// The environment variables ULO_HOST, ULO_USER and ULO_PASSWORD are used as fallback.
/// </summary>
public sealed class LaunchOptions
{
    public string? Host { get; private set; }

    public string? User { get; private set; }

    public string? Password { get; private set; }

    public bool AutoConnect { get; private set; }

    /// <summary>Tab to open on start: dashboard, live, activity, recordings, setup or api.</summary>
    public string? Tab { get; private set; }

    /// <summary>Start the live video immediately after connecting.</summary>
    public bool StartLive { get; private set; }

    public static LaunchOptions Parse(string[] args)
    {
        var options = new LaunchOptions
        {
            Host = Environment.GetEnvironmentVariable("ULO_HOST"),
            User = Environment.GetEnvironmentVariable("ULO_USER"),
            Password = Environment.GetEnvironmentVariable("ULO_PASSWORD"),
        };

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i].TrimStart('-').ToLowerInvariant();
            var value = i + 1 < args.Length && !args[i + 1].StartsWith('-') ? args[i + 1] : null;

            switch (name)
            {
                case "host" when value is not null:
                    options.Host = value;
                    i++;
                    break;
                case "user" when value is not null:
                    options.User = value;
                    i++;
                    break;
                case "password" when value is not null:
                    options.Password = value;
                    i++;
                    break;
                case "connect":
                    options.AutoConnect = true;
                    break;
                case "tab" when value is not null:
                    options.Tab = value;
                    i++;
                    break;
                case "live":
                    options.StartLive = true;
                    options.Tab ??= "live";
                    break;
            }
        }

        return options;
    }
}
