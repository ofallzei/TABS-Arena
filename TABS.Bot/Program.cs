using Tabs.Bot;

using var singleInstance = new Mutex(true, "TABS_Arena_Discord_Bot_SingleInstance", out bool ownsSingleInstance);
if (!ownsSingleInstance)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("TABS Arena Discord Bot is already running. Close the existing bot before starting another one.");
    Console.ResetColor();
    return;
}

var settings = BotSettings.Load();
if (string.IsNullOrWhiteSpace(settings.Token))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("TABS Arena Discord Bot is installed, but no bot token was found.");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Create TABS.Bot/botsettings.local.json from botsettings.example.json,");
    Console.WriteLine("or set the TABS_DISCORD_TOKEN environment variable.");
    Console.WriteLine();
    Console.WriteLine("This local token file is ignored by Git.");
    return;
}

var bot = new TabsArenaBot(settings);
await bot.RunAsync();
