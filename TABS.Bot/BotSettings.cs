using System.Text.Json;

namespace Tabs.Bot;

public sealed class BotSettings
{
    public string Token { get; set; } = "";
    public ulong GuildId { get; set; }

    public static BotSettings Load()
    {
        var settings = new BotSettings();
        var token = Environment.GetEnvironmentVariable("TABS_DISCORD_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            settings.Token = token.Trim();

        foreach (string path in CandidateConfigPaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var loaded = JsonSerializer.Deserialize<BotSettings>(File.ReadAllText(path), TabsJson.Options);
                if (loaded == null)
                    continue;

                if (string.IsNullOrWhiteSpace(settings.Token))
                    settings.Token = loaded.Token;
                settings.GuildId = loaded.GuildId;
                break;
            }
            catch
            {
                // Keep startup friendly. The console message below explains a missing token.
            }
        }

        return settings;
    }

    private static IEnumerable<string> CandidateConfigPaths()
    {
        string file = "botsettings.local.json";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                foreach (string path in new[]
                {
                    Path.Combine(dir.FullName, file),
                    Path.Combine(dir.FullName, "TABS.Bot", file)
                })
                {
                    if (seen.Add(path))
                        yield return path;
                }

                dir = dir.Parent;
            }
        }
    }
}
