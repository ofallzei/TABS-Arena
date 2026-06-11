using Discord;
using Discord.WebSocket;

namespace Tabs.Bot;

public static class DiscordMatchRenderer
{
    public static Embed BuildMatchEmbed(TabsMatch match, IReadOnlyDictionary<string, string>? factionEmojis = null)
    {
        if (match.MatchEndAnnounced)
            return BuildGameOverEmbed(match);

        match.RefreshTimerClock();
        match.RefreshMatchTimerClock();

        var embed = new EmbedBuilder()
            .WithTitle($"TABS Arena • {match.FormatLabel} • {match.ModeLabel}")
            .WithColor(match.PendingWinner == 1 ? Color.DarkRed : match.PendingWinner == 2 ? Color.Blue : new Color(102, 221, 235))
            .WithDescription(
                $"Round **{match.Round}** • Goal **{match.GoalPoints}** • Faction Mode **{(match.FactionModeEnabled ? "ON" : "OFF")}**\n" +
                BuildHostLine(match))
            .AddField("Match Control",
                $"Pending: **{match.PendingText}**\n" +
                $"Turn Order: **{match.TurnOrderText}**\n" +
                $"Turn Status: **{match.TurnStatusText}**\n" +
                $"Match Timer: {BuildMatchTimerText(match)}\n" +
                $"Tie Timer: {BuildTimerText(match)}\n" +
                $"Round Rewards: Winner **{match.GetWinnerReward()}g** • Loser **{match.GetLoserReward()}g** • Tie **{match.GetTieReward()}g**", false)
            .AddField("Milestones",
                $"Red: **{Math.Max(0, match.NextMilestoneForTeam(1) - match.RedPoints)} pts away**\n" +
                $"Blue: **{Math.Max(0, match.NextMilestoneForTeam(2) - match.BluePoints)} pts away**\n" +
                $"Next Reward: **{match.NextRewardText}**", true)
            .AddField("Possible Rewards Left", BuildRewardCountText(match), true);

        foreach (var player in match.Players)
            embed.AddField(PlayerTitle(match, player), BuildPlayerText(match, player, factionEmojis), match.Format == TabsMatchFormat.TwoVTwo);

        embed.AddField("Action Log", BuildLogText(match), false);
        embed.WithFooter("Use player action buttons for shop controls. Custom values open Discord popups.");
        return embed.Build();
    }

    public static MessageComponent BuildMainComponents(TabsMatch match)
    {
        if (match.MatchEndAnnounced)
            return new ComponentBuilder().Build();

        bool waitingForFirst = match.NeedsFirstTurnChoice;
        bool tieTimerDisabled = waitingForFirst || !match.CanUseTieTimer;
        var builder = new ComponentBuilder()
            .WithButton(match.Format == TabsMatchFormat.OneVOne ? "\U0001F534 Wins" : "Red Wins", "tabs:win:1", ButtonStyle.Danger, disabled: waitingForFirst, row: 0)
            .WithButton("Tie", "tabs:win:3", ButtonStyle.Secondary, disabled: waitingForFirst, row: 0)
            .WithButton(match.Format == TabsMatchFormat.OneVOne ? "\U0001F535 Wins" : "Blue Wins", "tabs:win:2", ButtonStyle.Primary, disabled: waitingForFirst, row: 0)
            .WithButton("Next Round", "tabs:next", ButtonStyle.Success, disabled: match.PendingWinner == 0 || waitingForFirst, row: 0);

        if (!match.FirstTurnChosen && match.Round == 1)
        {
            builder
                .WithButton(match.Format == TabsMatchFormat.OneVOne ? "\U0001F534 First (+50g)" : "Red First (+40g)", "tabs:first:1", ButtonStyle.Danger, row: 1)
                .WithButton(match.Format == TabsMatchFormat.OneVOne ? "\U0001F535 First (+50g)" : "Blue First (+40g)", "tabs:first:2", ButtonStyle.Primary, row: 1);
        }

        builder
            .WithButton(match.TimerRunning ? "Stop Timer" : match.GetTimerRemainingSeconds() == 120 ? "Start Tie Timer" : "Resume Timer", "tabs:timer:toggle", ButtonStyle.Secondary, disabled: tieTimerDisabled, row: 1)
            .WithButton("Restart Timer", "tabs:timer:restart", ButtonStyle.Secondary, row: 1);

        for (int player = 1; player <= match.PlayerCount; player++)
            builder.WithButton(PlayerActionLabel(match, player), $"tabs:panel:{player}", match.TeamOfPlayer(player) == 1 ? ButtonStyle.Danger : ButtonStyle.Primary, disabled: waitingForFirst, row: 2);

        builder
            .WithButton("Undo", "tabs:undo", ButtonStyle.Secondary, disabled: match.UndoStack.Count == 0, row: 3)
            .WithButton("Saves", "tabs:saves", ButtonStyle.Success, row: 3)
            .WithButton("Close Match", "tabs:closematch", ButtonStyle.Danger, row: 3)
            .WithButton("Forfeit", "tabs:forfeit", ButtonStyle.Danger, row: 3);

        return builder.Build();
    }

    private static Embed BuildGameOverEmbed(TabsMatch match)
    {
        int winner = match.WinningTeam;
        string winnerName = winner == 0 ? "Game Over" : $"{match.TeamName(winner)} Wins";
        string resultReason = match.ForfeitedTeam is 1 or 2
            ? $"\n**{match.TeamName(match.ForfeitedTeam)} forfeited.**"
            : "";
        var embed = new EmbedBuilder()
            .WithTitle($"TABS Arena - Game Over")
            .WithColor(winner == 1 ? Color.DarkRed : winner == 2 ? Color.Blue : new Color(102, 221, 235))
            .WithDescription($"**{winnerName}!**{resultReason}\nFinal Score: **Red {match.RedPoints} - Blue {match.BluePoints}**\nThis game will be deleted in **3 minutes**.");

        if (match.Format == TabsMatchFormat.OneVOne)
        {
            embed
                .AddField($"Red - {match.RequirePlayer(1).Name}", $"{match.RedPoints} point{Plural(match.RedPoints)}", true)
                .AddField($"Blue - {match.RequirePlayer(2).Name}", $"{match.BluePoints} point{Plural(match.BluePoints)}", true);
        }
        else
        {
            embed
                .AddField("Red Team",
                    $"{match.RequirePlayer(1).Name}\n{match.RequirePlayer(2).Name}\nScore: **{match.RedPoints}**", true)
                .AddField("Blue Team",
                    $"{match.RequirePlayer(3).Name}\n{match.RequirePlayer(4).Name}\nScore: **{match.BluePoints}**", true);
        }

        embed.AddField("Action Log", BuildLogText(match), false);
        return embed.Build();
    }

    private static string Plural(int count) => count == 1 ? "" : "s";

    public static Embed BuildPlayerEmbed(TabsMatch match, int playerId, IReadOnlyDictionary<string, string>? factionEmojis = null)
    {
        var player = match.RequirePlayer(playerId);
        return new EmbedBuilder()
            .WithTitle($"{PlayerTitle(match, player)} Actions")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription(BuildPlayerText(match, player, factionEmojis))
            .AddField("Permanent upgrades",
                $"Income: **{(match.IsIncomeAvailable ? IncomeCostText(match, player) : "disabled")}**\n" +
                $"Perm Move: **{DiscountedText(match.GetPermMoveBaseCost(), player.NextPermMoveDiscountPct)}**\n" +
                $"Random Faction: **{(match.FactionModeEnabled ? DiscountedText(match.GetFactionCost(player), player.NextFactionDiscountPct) : "disabled")}**\n" +
                $"Chosen Faction: **{(match.FactionModeEnabled ? DiscountedText(match.GetChosenFactionCost(player), player.NextChosenFactionDiscountPct) : "disabled")}**", false)
            .AddField("Utility shop",
                $"Single Move: **{(match.Mode == TabsMatchMode.FT13 ? 20 : 25)}g**\n" +
                $"Sellback: **{match.GetDisplayedSellbackPct(playerId)}%**", false)
            .Build();
    }

    public static MessageComponent BuildPlayerComponents(TabsMatch match, int playerId, bool controlsDisabled = false, bool endTurnDisabled = false, bool replayDisabled = false)
    {
        var player = match.RequirePlayer(playerId);
        var builder = new ComponentBuilder()
            .WithButton($"Buy Income ({(match.IsIncomeAvailable ? IncomeCostText(match, player) : "off")})", $"tabs:act:{playerId}:income", ButtonStyle.Primary, disabled: controlsDisabled || !match.IsIncomeAvailable, row: 0)
            .WithButton($"Buy Perm Move ({DiscountedText(match.GetPermMoveBaseCost(), player.NextPermMoveDiscountPct)})", $"tabs:act:{playerId}:perm", ButtonStyle.Primary, disabled: controlsDisabled, row: 0)
            .WithButton($"Buy Faction ({(match.FactionModeEnabled ? DiscountedText(match.GetFactionCost(player), player.NextFactionDiscountPct) : "off")})", $"tabs:act:{playerId}:faction", ButtonStyle.Primary, disabled: controlsDisabled || !match.FactionModeEnabled, row: 1)
            .WithButton($"Chosen Faction ({(match.FactionModeEnabled ? DiscountedText(match.GetChosenFactionCost(player), player.NextChosenFactionDiscountPct) : "off")})", $"tabs:act:{playerId}:chosen", ButtonStyle.Primary, disabled: controlsDisabled || !match.FactionModeEnabled, row: 1);

        builder
            .WithButton($"Single Move ({(match.Mode == TabsMatchMode.FT13 ? 20 : 25)}g)", $"tabs:act:{playerId}:move", ButtonStyle.Primary, disabled: controlsDisabled, row: 2)
            .WithButton("Replay (10g)", $"tabs:act:{playerId}:replay", ButtonStyle.Primary, disabled: replayDisabled, row: 2)
            .WithButton("Manage Army", $"tabs:army:{playerId}", ButtonStyle.Primary, disabled: controlsDisabled, row: 3);

        if (match.Format == TabsMatchFormat.TwoVTwo)
            builder.WithButton("BFT", $"tabs:act:{playerId}:bft", ButtonStyle.Secondary, disabled: controlsDisabled, row: 3);

        if (player.FreeFactionChoicePending)
            builder.WithButton("Claim Free Faction", $"tabs:act:{playerId}:freefaction", ButtonStyle.Success, disabled: controlsDisabled, row: 3);

        builder.WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, disabled: endTurnDisabled, row: 4);

        return builder.Build();
    }

    public static MessageComponent BuildModeComponents(TabsMatch match)
    {
        bool locked = match.ModesLocked;
        return new ComponentBuilder()
            .WithButton("FT13", "tabs:mode:ft13", match.Mode == TabsMatchMode.FT13 ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: locked, row: 0)
            .WithButton("FT20", "tabs:mode:ft20", match.Mode == TabsMatchMode.FT20 ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: locked, row: 0)
            .WithButton("FT30", "tabs:mode:ft30", match.Mode == TabsMatchMode.FT30 ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: locked, row: 0)
            .WithButton(match.FactionModeEnabled ? "Faction ON" : "Faction OFF", "tabs:mode:faction", match.FactionModeEnabled ? ButtonStyle.Success : ButtonStyle.Secondary, disabled: locked, row: 1)
            .Build();
    }

    public static Embed BuildStartEmbed(SocketUser host)
    {
        return new EmbedBuilder()
            .WithTitle("TABS Arena Start")
            .WithColor(new Color(102, 221, 235))
            .WithDescription($"Host: {host.Mention}")
            .AddField("Choose A Start Action", "Create a hosted match invite, load one of your saves, or delete one of your saves.", false)
            .Build();
    }

    public static MessageComponent BuildStartComponents()
    {
        return new ComponentBuilder()
            .WithButton("Create New Match", "tabs:start:create", ButtonStyle.Success, row: 0)
            .WithButton("Load Game", "tabs:start:load", ButtonStyle.Primary, row: 0)
            .WithButton("Delete Save", "tabs:start:delete", ButtonStyle.Danger, row: 0)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 0)
            .Build();
    }

    public static Embed BuildFormatPromptEmbed(MatchSetupSession setup)
    {
        return new EmbedBuilder()
            .WithTitle("Choose Match Format")
            .WithColor(new Color(102, 221, 235))
            .WithDescription($"Host: <@{setup.HostUserId}>\nChoose whether this match is 1v1 or 2v2.")
            .Build();
    }

    public static MessageComponent BuildFormatPromptComponents()
    {
        return new ComponentBuilder()
            .WithButton("1v1", "tabs:setupformat:1v1", ButtonStyle.Danger, row: 0)
            .WithButton("2v2", "tabs:setupformat:2v2", ButtonStyle.Primary, row: 0)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 0)
            .Build();
    }

    public static Embed BuildOpponentChatPromptEmbed(MatchSetupSession setup)
    {
        string title = setup.Format == TabsMatchFormat.OneVOne ? "Mention Opponent" : "Mention 2v2 Players";
        string instructions = setup.Format == TabsMatchFormat.OneVOne
            ? "Mention the opponent in this channel using Discord chat, like `@Player`."
            : "Mention exactly 3 players in this order: red teammate, blue player 1, blue player 2. Example: `@Teammate @Blue1 @Blue2`.";

        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(new Color(102, 221, 235))
            .WithDescription($"Host: <@{setup.HostUserId}>\n{instructions}")
            .Build();
    }

    public static MessageComponent BuildCancelOnlyComponents()
    {
        return new ComponentBuilder()
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 0)
            .Build();
    }

    public static Embed BuildSavePickerEmbed(string title, string text)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(new Color(102, 221, 235))
            .WithDescription(text)
            .Build();
    }

    public static MessageComponent BuildSavePickerComponents(IReadOnlyList<string> saves, string customId)
    {
        var select = new SelectMenuBuilder()
            .WithCustomId(customId)
            .WithPlaceholder("Choose save");

        foreach (string save in saves.Take(25))
            select.AddOption(save, save);

        return new ComponentBuilder()
            .WithSelectMenu(select, row: 0)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 1)
            .Build();
    }

    public static Embed BuildSaveHubEmbed(TabsMatch match)
    {
        string loaded = string.IsNullOrWhiteSpace(match.LoadedSaveName) ? "Unsaved match" : $"Loaded save: `{match.LoadedSaveName}`";
        return new EmbedBuilder()
            .WithTitle("Saves")
            .WithColor(new Color(102, 221, 235))
            .WithDescription($"{loaded}\nChoose what you want to do with this hosted match.")
            .Build();
    }

    public static MessageComponent BuildSaveHubComponents()
    {
        return new ComponentBuilder()
            .WithButton("New Game", "tabs:saves:newgame", ButtonStyle.Secondary, row: 0)
            .WithButton("Load Save", "tabs:saves:load", ButtonStyle.Primary, row: 0)
            .WithButton("Delete Save", "tabs:saves:delete", ButtonStyle.Danger, row: 0)
            .WithButton("Save Game", "tabs:saves:save", ButtonStyle.Success, row: 0)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 1)
            .Build();
    }

    public static Embed BuildConfirmEmbed(string title, string text)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithColor(Color.DarkRed)
            .WithDescription(text)
            .Build();
    }

    public static MessageComponent BuildConfirmComponents(string confirmCustomId, string confirmLabel)
    {
        return new ComponentBuilder()
            .WithButton(confirmLabel, confirmCustomId, ButtonStyle.Danger, row: 0)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 0)
            .Build();
    }

    public static Embed BuildSetupEmbed(MatchSetupSession setup)
    {
        return new EmbedBuilder()
            .WithTitle("Create Hosted Match")
            .WithColor(new Color(102, 221, 235))
            .WithDescription(
                $"Host: <@{setup.HostUserId}>\n" +
                BuildSetupPlayersText(setup) + "\n\n" +
                "Answer the three setup questions, set players, then send the invite.")
            .AddField("1. Match Format", setup.Format == TabsMatchFormat.OneVOne ? "1v1" : "2v2", true)
            .AddField("2. FT Mode", setup.Mode.ToString().ToUpperInvariant(), true)
            .AddField("3. Faction Mode", setup.FactionMode ? "ON" : "OFF", true)
            .Build();
    }

    public static MessageComponent BuildSetupComponents(MatchSetupSession setup)
    {
        var format = new SelectMenuBuilder()
            .WithCustomId("tabs:setup:format")
            .WithPlaceholder("Question 1: 1v1 or 2v2")
            .AddOption("1v1", "1v1", isDefault: setup.Format == TabsMatchFormat.OneVOne)
            .AddOption("2v2", "2v2", isDefault: setup.Format == TabsMatchFormat.TwoVTwo);

        var mode = new SelectMenuBuilder()
            .WithCustomId("tabs:setup:mode")
            .WithPlaceholder("Question 2: FT13, FT20, or FT30")
            .AddOption("FT13", "ft13", isDefault: setup.Mode == TabsMatchMode.FT13)
            .AddOption("FT20", "ft20", isDefault: setup.Mode == TabsMatchMode.FT20)
            .AddOption("FT30", "ft30", isDefault: setup.Mode == TabsMatchMode.FT30);

        var faction = new SelectMenuBuilder()
            .WithCustomId("tabs:setup:faction")
            .WithPlaceholder("Question 3: Faction Mode")
            .AddOption("Faction Mode ON", "on", isDefault: setup.FactionMode)
            .AddOption("Faction Mode OFF", "off", isDefault: !setup.FactionMode);

        string playerButtonLabel = setup.HasRequiredPlayers
            ? setup.Format == TabsMatchFormat.OneVOne ? "Change Opponent" : "Change Players"
            : setup.Format == TabsMatchFormat.OneVOne ? "Set Opponent" : "Set Players";

        return new ComponentBuilder()
            .WithSelectMenu(format, row: 0)
            .WithSelectMenu(mode, row: 1)
            .WithSelectMenu(faction, row: 2)
            .WithButton("Send Invite", "tabs:setup:invite", ButtonStyle.Success, row: 3)
            .WithButton(playerButtonLabel, "tabs:setup:players", ButtonStyle.Secondary, row: 3)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 3)
            .Build();
    }

    public static Embed BuildInviteEmbed(PendingInvite invite)
    {
        return new EmbedBuilder()
            .WithTitle("TABS Arena Match Invite")
            .WithColor(new Color(102, 221, 235))
            .WithDescription(
                BuildInvitePlayersText(invite) + "\n\n" +
                $"Format: **{(invite.Format == TabsMatchFormat.OneVOne ? "1v1" : "2v2")}**\n" +
                $"Mode: **{invite.Mode.ToString().ToUpperInvariant()}**\n" +
                $"Faction Mode: **{(invite.FactionMode ? "ON" : "OFF")}**")
            .Build();
    }

    public static MessageComponent BuildInviteComponents(ulong hostUserId)
    {
        return new ComponentBuilder()
            .WithButton("Accept Match", $"tabs:invite:accept:{hostUserId}", ButtonStyle.Success)
            .WithButton("Decline", $"tabs:invite:decline:{hostUserId}", ButtonStyle.Danger)
            .WithButton("Cancel Invite", $"tabs:invite:cancel:{hostUserId}", ButtonStyle.Secondary)
            .Build();
    }

    public static Embed BuildTeamArrangementEmbed(PendingInvite invite)
    {
        invite.EnsureTeamAssignments();
        string red = BuildTeamLine(invite, 1);
        string blue = BuildTeamLine(invite, 2);
        return new EmbedBuilder()
            .WithTitle("Arrange 2v2 Teams")
            .WithColor(new Color(102, 221, 235))
            .WithDescription(
                "All players accepted. Swap teams if needed, then start the match.\n\n" +
                $"**Red Team ({invite.TeamAssignments.Count(kvp => kvp.Value == 1)}/2)**\n{red}\n\n" +
                $"**Blue Team ({invite.TeamAssignments.Count(kvp => kvp.Value == 2)}/2)**\n{blue}")
            .AddField("Required", "The match needs exactly **2 players on each team** to start.", false)
            .Build();
    }

    public static MessageComponent BuildTeamArrangementComponents(PendingInvite invite)
    {
        invite.EnsureTeamAssignments();
        var builder = new ComponentBuilder();
        int index = 0;
        foreach (ulong userId in invite.AllParticipantUserIds)
        {
            string team = invite.TeamOf(userId) == 1 ? "Red" : "Blue";
            string label = $"Move {ShortName(invite.DisplayNameFor(userId))} to {(team == "Red" ? "Blue" : "Red")}";
            builder.WithButton(label, $"tabs:arrange:toggle:{invite.HostUserId}:{userId}", ButtonStyle.Secondary, row: index / 2);
            index++;
        }

        builder.WithButton("Start Match", $"tabs:arrange:start:{invite.HostUserId}", ButtonStyle.Success, row: 2);
        builder.WithButton("Cancel Invite", $"tabs:invite:cancel:{invite.HostUserId}", ButtonStyle.Danger, row: 2);
        return builder.Build();
    }

    public static MessageComponent BuildFactionSelect(TabsMatch match, int playerId, bool free)
    {
        var player = match.RequirePlayer(playerId);
        int cost = GetChosenFactionPreviewCost(match, player, free);
        var select = new SelectMenuBuilder()
            .WithCustomId($"tabs:choose:{playerId}:{(free ? "free" : "paid")}")
            .WithPlaceholder(free ? "Choose a free faction" : "Choose a faction to buy");

        foreach (string faction in match.AvailableFactions(player).Take(25))
            select.AddOption(faction, faction, free ? "Free faction choice" : $"{cost}g");

        return new ComponentBuilder()
            .WithSelectMenu(select, row: 0)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 1)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 2)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 2)
            .Build();
    }

    public static Embed BuildFactionChooseEmbed(TabsMatch match, int playerId, bool free, string? faction = null)
    {
        var player = match.RequirePlayer(playerId);
        int cost = GetChosenFactionPreviewCost(match, player, free);
        string text = faction == null
            ? $"Current gold: **{player.Gold}g**\n{(free ? "Choose the free faction, then confirm it." : "Choose a faction, then press Buy to confirm.")}"
            : $"Faction: **{faction}**\nPrice: **{cost}g**\nCurrent gold: **{player.Gold}g**\nPredicted gold: **{player.Gold - cost}g**";

        return new EmbedBuilder()
            .WithTitle(free ? $"{PlayerTitle(match, player)} Free Faction" : $"{PlayerTitle(match, player)} Chosen Faction")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription(text)
            .Build();
    }

    public static MessageComponent BuildFactionConfirmComponents(TabsMatch match, int playerId, bool free, string faction)
    {
        var player = match.RequirePlayer(playerId);
        int cost = GetChosenFactionPreviewCost(match, player, free);
        string mode = free ? "free" : "paid";
        string label = free ? "Claim" : $"Buy ({cost}g)";
        bool disabled = !free && player.Gold < cost;

        return new ComponentBuilder()
            .WithButton(label, $"tabs:chooseconfirm:{playerId}:{mode}:{faction}", free ? ButtonStyle.Success : ButtonStyle.Primary, disabled: disabled, row: 0)
            .WithButton("Cancel", "tabs:cancel", ButtonStyle.Secondary, row: 0)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 2)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 2)
            .Build();
    }

    public static Embed BuildArmyMenuEmbed(TabsMatch match, int playerId)
    {
        var player = match.RequirePlayer(playerId);

        return new EmbedBuilder()
            .WithTitle($"{PlayerTitle(match, player)} Army")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription($"Gold: **{player.Gold}g**\nChoose whether this player is buying or selling units.")
            .Build();
    }

    public static MessageComponent BuildArmyMenuComponents(int playerId)
    {
        return new ComponentBuilder()
            .WithButton("Buy Units", $"tabs:army:buyunits:{playerId}", ButtonStyle.Secondary, row: 0)
            .WithButton("Sell Units", $"tabs:army:sell:{playerId}", ButtonStyle.Success, row: 0)
            .WithButton("Close", $"tabs:army:close:{playerId}", ButtonStyle.Secondary, row: 0)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 2)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 2)
            .Build();
    }

    public static Embed BuildArmyFactionEmbed(TabsMatch match, int playerId)
    {
        var player = match.RequirePlayer(playerId);
        string accessText = match.FactionModeEnabled
            ? "Choose one of this player's owned factions."
            : "Faction Mode is OFF, so every faction is available.";

        return new EmbedBuilder()
            .WithTitle($"{PlayerTitle(match, player)} Buy Units")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription($"{accessText}\nGold: **{player.Gold}g**")
            .Build();
    }

    public static MessageComponent BuildArmyFactionComponents(TabsMatch match, int playerId, IReadOnlyDictionary<string, string>? factionEmojis)
    {
        var player = match.RequirePlayer(playerId);
        var factionNames = match.FactionModeEnabled
            ? player.Factions.OrderBy(name => name).ToList()
            : TabsMatch.AllFactions.OrderBy(name => name).ToList();

        if (factionNames.Count == 0)
        {
            return new ComponentBuilder()
                .WithButton("Back", $"tabs:army:{playerId}", ButtonStyle.Secondary, row: 0)
                .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 2)
                .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 2)
                .Build();
        }

        var select = new SelectMenuBuilder()
            .WithCustomId($"tabs:army:faction:{playerId}")
            .WithPlaceholder("Choose faction");

        foreach (string faction in factionNames.Take(25))
        {
            int unitCount = ArmyCatalog.UnitsForFaction(faction).Count;
            select.AddOption(faction, faction, unitCount == 0 ? "No units added yet" : $"{unitCount} units added", GetFactionEmote(faction, factionEmojis));
        }

        var builder = new ComponentBuilder()
            .WithSelectMenu(select, row: 0)
            .WithButton("Back", $"tabs:army:{playerId}", ButtonStyle.Secondary, row: 1)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 3)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 3);
        return builder.Build();
    }

    public static Embed BuildArmyUnitEmbed(TabsMatch match, int playerId, string faction, int page = 0)
    {
        var player = match.RequirePlayer(playerId);
        var units = ArmyCatalog.UnitsForFaction(faction);
        int totalPages = Math.Max(1, (int)Math.Ceiling(units.Count / (double)ArmyCatalog.UnitsPerPage));
        int displayPage = Math.Clamp(page, 0, totalPages - 1) + 1;
        string text = units.Count == 0
            ? "No units have been added for this faction yet."
            : $"Choose a unit to buy from **{faction}**.\nGold: **{player.Gold}g**\nPage **{displayPage}/{totalPages}**";

        return new EmbedBuilder()
            .WithTitle($"{PlayerTitle(match, player)} Army")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription(text)
            .Build();
    }

    public static MessageComponent BuildArmyUnitComponents(TabsMatch match, int playerId, string faction, int page = 0)
    {
        var units = ArmyCatalog.UnitsForFaction(faction);
        int totalPages = Math.Max(1, (int)Math.Ceiling(units.Count / (double)ArmyCatalog.UnitsPerPage));
        int currentPage = Math.Clamp(page, 0, totalPages - 1);
        var builder = new ComponentBuilder();
        if (units.Count > 0)
        {
            var select = new SelectMenuBuilder()
                .WithCustomId($"tabs:army:unit:{playerId}")
                .WithPlaceholder("Choose unit");

            foreach (var unit in units.Skip(currentPage * ArmyCatalog.UnitsPerPage).Take(ArmyCatalog.UnitsPerPage))
                select.AddOption(unit.Name, unit.Slug, $"{unit.Gold} gold");

            builder.WithSelectMenu(select, row: 0);
        }

        if (totalPages > 1)
        {
            builder
                .WithButton("Previous", $"tabs:army:factionpage:{playerId}:{currentPage - 1}:{faction}", ButtonStyle.Secondary, disabled: currentPage == 0, row: 1)
                .WithButton("Next", $"tabs:army:factionpage:{playerId}:{currentPage + 1}:{faction}", ButtonStyle.Secondary, disabled: currentPage >= totalPages - 1, row: 1);
        }

        int navRow = totalPages > 1 ? 2 : 1;
        int actionRow = Math.Min(4, navRow + 2);
        builder
            .WithButton("Back", $"tabs:army:{playerId}", ButtonStyle.Secondary, row: navRow)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: actionRow)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: actionRow);
        return builder.Build();
    }

    public static Embed BuildArmyBuyEmbed(TabsMatch match, int playerId, string unitSlug, int quantity)
    {
        var player = match.RequirePlayer(playerId);
        var unit = ArmyCatalog.FindUnit(unitSlug);
        if (unit == null)
            return BuildArmyErrorEmbed("That unit has not been added yet.");

        int total = match.PreviewArmyBuyGold(unitSlug, quantity);
        int predicted = player.Gold - total;
        return new EmbedBuilder()
            .WithTitle($"{PlayerTitle(match, player)} Buy Unit")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription(
                $"Unit: **{unit.Name}**\n" +
                $"Faction: **{unit.Faction}**\n" +
                $"Unit gold: **{unit.Gold}g**\n" +
                $"Quantity: **{quantity}**\n" +
                $"Total gold: **{total}g**\n" +
                $"Current gold: **{player.Gold}g**\n" +
                $"Predicted gold: **{predicted}g**")
            .Build();
    }

    public static MessageComponent BuildArmyBuyComponents(int playerId, string unitSlug, int quantity)
    {
        var unit = ArmyCatalog.FindUnit(unitSlug);
        string backId = unit == null ? $"tabs:army:{playerId}" : $"tabs:army:factionback:{playerId}:{unit.Faction}";
        string label = quantity == 1 ? "Buy 1" : $"Buy {quantity}";
        return new ComponentBuilder()
            .WithButton(label, $"tabs:army:buy:{playerId}:{unitSlug}:{quantity}", ButtonStyle.Primary, row: 0)
            .WithButton("Buy Custom", $"tabs:army:buycustom:{playerId}:{unitSlug}", ButtonStyle.Secondary, row: 0)
            .WithButton("Back", backId, ButtonStyle.Secondary, row: 1)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 3)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 3)
            .Build();
    }

    public static Embed BuildArmySellSelectEmbed(TabsMatch match, int playerId)
    {
        var player = match.RequirePlayer(playerId);
        return new EmbedBuilder()
            .WithTitle($"{PlayerTitle(match, player)} Sell Units")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription($"Choose one of this player's owned units.\nGold: **{player.Gold}g**\nSell value: **{match.GetDisplayedSellbackPct(playerId)}%**")
            .Build();
    }

    public static MessageComponent BuildArmySellSelectComponents(TabsMatch match, int playerId)
    {
        var select = new SelectMenuBuilder()
            .WithCustomId($"tabs:army:sellunit:{playerId}")
            .WithPlaceholder("Choose owned unit");

        foreach (var unit in match.OwnedArmyUnits(playerId).Take(25))
            select.AddOption($"{match.OwnedArmyCount(playerId, unit.Slug)}x {unit.Name}", unit.Slug, $"{unit.Gold} gold each");

        return new ComponentBuilder()
            .WithSelectMenu(select, row: 0)
            .WithButton("Back", $"tabs:army:{playerId}", ButtonStyle.Secondary, row: 1)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 3)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 3)
            .Build();
    }

    public static Embed BuildArmySellEmbed(TabsMatch match, int playerId, string unitSlug, int quantity)
    {
        var player = match.RequirePlayer(playerId);
        var unit = ArmyCatalog.FindUnit(unitSlug);
        if (unit == null)
            return BuildArmyErrorEmbed("That unit has not been added yet.");

        int owned = match.OwnedArmyCount(playerId, unitSlug);
        int gain = match.PreviewArmySellGold(playerId, unitSlug, quantity);
        int predicted = player.Gold + gain;
        return new EmbedBuilder()
            .WithTitle($"{PlayerTitle(match, player)} Sell Unit")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription(
                $"Sell value: **{match.GetDisplayedSellbackPct(playerId)}%**\n" +
                $"Unit: **{unit.Name}**\n" +
                $"Owned: **{owned}**\n" +
                $"Quantity: **{quantity}**\n" +
                $"Gold gained: **{gain}g**\n" +
                $"Current gold: **{player.Gold}g**\n" +
                $"Predicted gold: **{predicted}g**")
            .Build();
    }

    public static MessageComponent BuildArmySellComponents(TabsMatch match, int playerId, string unitSlug, int quantity)
    {
        int owned = match.OwnedArmyCount(playerId, unitSlug);
        string label = quantity == 1 ? "Sell 1" : $"Sell {quantity}";
        var builder = new ComponentBuilder()
            .WithButton(label, $"tabs:army:sellconfirm:{playerId}:{unitSlug}:{quantity}", ButtonStyle.Success, row: 0);

        if (owned > 1)
            builder.WithButton("Custom Sell", $"tabs:army:sellcustom:{playerId}:{unitSlug}", ButtonStyle.Secondary, row: 0);

        builder
            .WithButton("Back", $"tabs:army:sell:{playerId}", ButtonStyle.Secondary, row: 1)
            .WithButton("Main Menu", $"tabs:playermenu:{playerId}", ButtonStyle.Secondary, row: 3)
            .WithButton("End Turn", $"tabs:endturn:{playerId}", ButtonStyle.Danger, row: 3);
        return builder.Build();
    }

    public static Embed BuildArmyBoughtEmbed(TabsMatch match, int playerId, string unitSlug, int quantity)
    {
        var player = match.RequirePlayer(playerId);
        var unit = ArmyCatalog.FindUnit(unitSlug);
        string unitName = unit?.Name ?? "unit";

        return new EmbedBuilder()
            .WithTitle("Bought Units")
            .WithColor(match.TeamOfPlayer(playerId) == 1 ? Color.DarkRed : Color.Blue)
            .WithDescription($"Bought **{quantity}: {unitName} x{quantity} units.**\n{player.Name} now has **{player.Gold}g**.")
            .Build();
    }

    private static Embed BuildArmyErrorEmbed(string message)
    {
        return new EmbedBuilder()
            .WithTitle("Army")
            .WithColor(Color.DarkRed)
            .WithDescription(message)
            .Build();
    }

    private static string PlayerTitle(TabsMatch match, PlayerState player)
    {
        if (match.Format == TabsMatchFormat.OneVOne)
            return $"{(match.TeamOfPlayer(player.Id) == 1 ? "\U0001F534" : "\U0001F535")} {player.Name}";

        string flag = match.TeamOfPlayer(player.Id) == 1 ? "\U0001F6A9" : "\U0001F535";
        return $"{flag} P{player.Id}: {player.Name}";
    }

    private static string PlayerActionLabel(TabsMatch match, int player)
    {
        if (match.Format == TabsMatchFormat.OneVOne)
            return match.TeamOfPlayer(player) == 1 ? "\U0001F534 Actions" : "\U0001F535 Actions";

        return $"P{player} Actions";
    }

    private static string BuildHostLine(TabsMatch match)
    {
        if (match.HostUserId == 0)
            return "Host: not set";

        if (match.Format == TabsMatchFormat.OneVOne)
        {
            string guest = match.Player2UserId == 0
                ? "No invite"
                : match.InviteAccepted ? $"<@{match.Player2UserId}> accepted" : $"<@{match.Player2UserId}> pending";
            return $"Host: <@{match.HostUserId}> • Opponent: {guest}";
        }

        return $"Host: <@{match.HostUserId}> • Red 2: {MentionOrUnset(match.Player2UserId)} • Blue 1: {MentionOrUnset(match.Player3UserId)} • Blue 2: {MentionOrUnset(match.Player4UserId)}";
    }

    private static string BuildPlayerText(TabsMatch match, PlayerState player, IReadOnlyDictionary<string, string>? factionEmojis)
    {
        var factions = BuildFactionText(player, factionEmojis);
        var coupons = BuildCoupons(match, player);
        string factionLine = match.FactionModeEnabled ? $"Factions: {factions}\n" : "";
        string turnLine = match.IsPlayerTurnEnded(player.Id)
            ? "Turn: ended\n"
            : match.IsPlayerTurnActive(player.Id)
                ? "Turn: active\n"
                : "";
        return
            $"Gold **{player.Gold}** - Points **{match.TeamPoints(match.TeamOfPlayer(player.Id))}**\n" +
            $"Perm MV **{player.PermMoveUpgrades}** - Income **+{player.Income}** - Interest now **+{match.CalcInterest(player.Gold)}**\n" +
            factionLine +
            turnLine +
            $"Coupons: {coupons}";
    }

    private static string BuildSetupPlayersText(MatchSetupSession setup)
    {
        if (setup.Format == TabsMatchFormat.OneVOne)
            return $"Opponent: {(setup.Player2UserId == 0 ? "not set" : $"<@{setup.Player2UserId}>")}";

        return
            $"Red 1: <@{setup.HostUserId}>\n" +
            $"Red 2: {MentionOrUnset(setup.Player2UserId)}\n" +
            $"Blue 1: {MentionOrUnset(setup.Player3UserId)}\n" +
            $"Blue 2: {MentionOrUnset(setup.Player4UserId)}";
    }

    private static string BuildInvitePlayersText(PendingInvite invite)
    {
        string Status(ulong userId) => invite.AcceptedUserIds.Contains(userId) ? "accepted" : "pending";
        if (invite.Format == TabsMatchFormat.OneVOne)
            return $"<@{invite.HostUserId}> invited <@{invite.Player2UserId}> to a match.\nOpponent: <@{invite.Player2UserId}> **{Status(invite.Player2UserId)}**";

        return
            $"<@{invite.HostUserId}> invited 3 players to a 2v2 match.\n" +
            $"Red 1: <@{invite.HostUserId}> **host**\n" +
            $"Red 2: <@{invite.Player2UserId}> **{Status(invite.Player2UserId)}**\n" +
            $"Blue 1: <@{invite.Player3UserId}> **{Status(invite.Player3UserId)}**\n" +
            $"Blue 2: <@{invite.Player4UserId}> **{Status(invite.Player4UserId)}**";
    }

    private static string BuildTeamLine(PendingInvite invite, int team)
    {
        var players = invite.TeamAssignments
            .Where(kvp => kvp.Value == team)
            .Select(kvp => $"<@{kvp.Key}> ({invite.DisplayNameFor(kvp.Key)})")
            .ToList();
        return players.Count == 0 ? "none" : string.Join("\n", players);
    }

    private static string ShortName(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        return name.Length <= 18 ? name : name[..18];
    }

    private static string MentionOrUnset(ulong userId) => userId == 0 ? "not set" : $"<@{userId}>";

    public static string NormalizeEmojiKey(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    private static IEmote? GetFactionEmote(string faction, IReadOnlyDictionary<string, string>? factionEmojis)
    {
        if (factionEmojis == null)
            return null;

        string key = NormalizeEmojiKey(faction);
        if (!factionEmojis.TryGetValue(key, out string? raw))
            return null;

        return Emote.TryParse(raw, out var emote) ? emote : null;
    }

    private static string BuildFactionText(PlayerState player, IReadOnlyDictionary<string, string>? factionEmojis)
    {
        if (player.Factions.Count == 0)
            return "none";

        return string.Join(" ", player.Factions.Select(faction =>
        {
            string key = NormalizeEmojiKey(faction);
            return factionEmojis != null && factionEmojis.TryGetValue(key, out string? emoji)
                ? emoji
                : faction;
        }));
    }

    private static string BuildCoupons(TabsMatch match, PlayerState player)
    {
        var coupons = new List<string>();
        if (player.NextIncomeDiscountPct > 0) coupons.Add($"{player.NextIncomeDiscountPct}% income");
        if (player.NextFactionDiscountPct > 0) coupons.Add($"{player.NextFactionDiscountPct}% faction");
        if (player.NextChosenFactionDiscountPct > 0) coupons.Add($"{player.NextChosenFactionDiscountPct}% chosen faction");
        if (player.NextPermMoveDiscountPct > 0) coupons.Add($"{player.NextPermMoveDiscountPct}% perm move");
        if (player.NextSellBonusPct > 0) coupons.Add($"+{player.NextSellBonusPct}% next sell");
        if (player.HasFullRefund) coupons.Add("full refund");
        if (player.FreeFactionChoicePending) coupons.Add("free faction choice");
        if (match.Format == TabsMatchFormat.TwoVTwo)
            coupons.Add($"BFT +{(match.TeamOfPlayer(player.Id) == 1 ? match.RedBftSurchargePct : match.BlueBftSurchargePct)}%");
        return coupons.Count == 0 ? "none" : string.Join(", ", coupons);
    }

    private static string BuildRewardCountText(TabsMatch match)
    {
        var counts = match.RewardCounts();
        if (counts.Count == 0)
            return "None left";

        return string.Join("\n", counts.Select(kvp => $"**{kvp.Value}x** {kvp.Key}"));
    }

    private static string BuildLogText(TabsMatch match)
    {
        if (match.ActionLog.Count == 0)
            return "No actions yet.";

        return string.Join("\n", match.ActionLog.Take(8).Select(line => $"• {line}"));
    }

    private static string BuildTimerText(TabsMatch match)
    {
        if (match.TimerRunning && match.TimerEndsAtUtc != null)
        {
            long unix = match.TimerEndsAtUtc.Value.ToUnixTimeSeconds();
            return $"**running** • ends <t:{unix}:R> at <t:{unix}:T>";
        }

        return match.GetTimerRemainingSeconds() == 0
            ? "**0:00** done"
            : $"**{match.TimerText}** paused";
    }

    private static string BuildMatchTimerText(TabsMatch match)
    {
        if (!match.FirstTurnChosen)
            return "**4:00** waiting for first turn";
        if (!match.TurnsComplete)
            return "**4:00** waiting for turns";
        if (match.MatchTimerRunning && match.MatchTimerEndsAtUtc != null)
        {
            long unix = match.MatchTimerEndsAtUtc.Value.ToUnixTimeSeconds();
            return $"**running** - ends <t:{unix}:R> at <t:{unix}:T>";
        }

        return match.GetMatchTimerRemainingSeconds() == 0
            ? "**0:00** done"
            : $"**{match.MatchTimerText}** waiting";
    }

    private static string DiscountedText(int baseCost, int discountPct)
    {
        int cost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));
        return CostWithDiscountText(cost, discountPct);
    }

    private static string IncomeCostText(TabsMatch match, PlayerState player)
    {
        int discountPct = player.IncomeDecayPct + player.NextIncomeDiscountPct;
        return CostWithDiscountText(match.GetDisplayedIncomeCost(player), discountPct);
    }

    private static string CostWithDiscountText(int cost, int discountPct)
    {
        return discountPct > 0 ? $"{cost}g, {discountPct}% off" : $"{cost}g";
    }

    private static int GetChosenFactionPreviewCost(TabsMatch match, PlayerState player, bool free)
    {
        if (free)
            return 0;

        return (int)Math.Ceiling(Math.Max(1m, match.GetChosenFactionCost(player) * (1m - player.NextChosenFactionDiscountPct / 100m)));
    }
}
