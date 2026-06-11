# TABS Arena Discord Bot

This project runs a local Discord bot for TABS Arena while your PC is on.

## Local Setup

1. Open the Discord Developer Portal and create an application.
2. Add a bot user to that application.
3. Enable the bot's Message Content Intent so `/tabs-start` can read the host's opponent mention.
4. Copy the bot token.
5. Copy `botsettings.example.json` to `botsettings.local.json`.
6. Paste the token into `botsettings.local.json`.
7. Optional: paste your Discord server ID into `GuildId` for instant slash-command updates.
8. Invite the bot to your server with the `bot` and `applications.commands` scopes.
9. Run:

```powershell
dotnet run --project TABS.Bot\TABS.Bot.csproj
```

`botsettings.local.json` is ignored by Git. Do not commit your real token.

## Main Commands

- `/tabs-start` opens the hosted start menu for players with the `Host` role.
- `Create New Match` asks the host to mention the opponent in chat, then choose format, FT mode, and Faction Mode before sending an invite.
- `/tabs-status` reposts the active match controls.
- `/tabs-guide` shows the quick guide in Discord.

The invited player must accept before a hosted match is created.
The main match message handles round results, first turn, tie timer, undo, saving, modes, and new game.
The `Save` button opens a Discord popup for the save name.
Load and delete save options live inside `/tabs-start`, and saves are owned by the hosting player.
Player names come from Discord display names.
Use each `P# Actions` button for shop controls. Custom spend, sell unit, and BFT use Discord popups for typed values.
