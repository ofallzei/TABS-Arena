using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace Tabs.Bot;

public sealed class TabsArenaBot
{
    private const string MatchmakingChannelName = "matchmaking";
    private const string DefaultJoinRoleName = "Non-host";
    private static readonly TimeSpan FinishedMatchDeleteDelay = TimeSpan.FromMinutes(3);

    private readonly BotSettings _settings;
    private readonly MatchStore _store = new();
    private readonly DiscordSocketClient _client;
    private readonly Dictionary<(ulong ChannelId, ulong HostUserId), MatchSetupSession> _setupSessions = new();
    private readonly Dictionary<(ulong ChannelId, ulong HostUserId), PendingInvite> _pendingInvites = new();
    private readonly Dictionary<(ulong ChannelId, ulong HostUserId), string> _pendingSaveDeletes = new();
    private readonly HashSet<(ulong ChannelId, ulong HostUserId)> _awaitingOpponentMentions = new();
    private readonly Dictionary<(ulong ChannelId, ulong HostUserId), SocketMessageComponent> _setupPromptInteractions = new();
    private readonly HashSet<(ulong ChannelId, ulong HostUserId)> _matchCreationsInProgress = new();
    private readonly Dictionary<(ulong ChannelId, ulong UserId, int PlayerId), OpenPlayerPanel> _openPlayerPanels = new();
    private readonly Dictionary<(ulong ChannelId, int PlayerId), IUserMessage> _turnPanelMessages = new();
    private readonly Dictionary<ulong, IUserMessage> _tieTimerAnnouncements = new();
    private readonly Dictionary<(ulong ChannelId, int Team), MilestoneWarning> _milestoneWarnings = new();
    private readonly Dictionary<ulong, DateTimeOffset> _nextOverviewMoveByChannel = new();
    private readonly Dictionary<ulong, DateTimeOffset> _nextCleanupPermissionWarningByChannel = new();
    private DateTimeOffset _nextMatchmakingMentionCleanupUtc = DateTimeOffset.MinValue;
    private IReadOnlyDictionary<string, string> _factionEmojiByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public TabsArenaBot(BotSettings settings)
    {
        _settings = settings;
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = false
        });
    }

    public async Task RunAsync()
    {
        await _store.LoadActiveAsync();

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.SlashCommandExecuted += SlashCommandExecutedAsync;
        _client.ButtonExecuted += ButtonExecutedAsync;
        _client.SelectMenuExecuted += SelectMenuExecutedAsync;
        _client.ModalSubmitted += ModalSubmittedAsync;
        _client.MessageReceived += MessageReceivedAsync;
        _client.UserJoined += UserJoinedAsync;

        await _client.LoginAsync(TokenType.Bot, _settings.Token);
        await _client.StartAsync();

        _ = Task.Run(TimerLoopAsync);
        await Task.Delay(Timeout.Infinite);
    }

    private Task LogAsync(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }

    private async Task UserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot)
            return;

        try
        {
            var role = user.Guild.Roles.FirstOrDefault(role =>
                string.Equals(role.Name, DefaultJoinRoleName, StringComparison.OrdinalIgnoreCase));

            if (role == null)
            {
                Console.WriteLine($"Auto-role skipped for {user.Username}: role `{DefaultJoinRoleName}` was not found.");
                return;
            }

            if (user.Roles.Any(existingRole => existingRole.Id == role.Id))
                return;

            await user.AddRoleAsync(role);
            Console.WriteLine($"Gave `{DefaultJoinRoleName}` role to {user.Username}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-role failed for {user.Username}: {ex.Message}");
        }
    }

    private async Task ReadyAsync()
    {
        await RegisterCommandsAsync();
        RefreshFactionEmojiMap();
        await CleanupStaleMatchmakingMentionsAsync();
        await CleanupLegacyPublicTurnPanelsAsync();
        await CleanupStaleMilestoneWarningsAsync();
        foreach (var match in _store.ActiveMatches)
        {
            await RefreshTurnPanelsAsync(match);
            await RefreshMilestoneWarningsAsync(match);
        }
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"TABS Arena bot online as {_client.CurrentUser.Username}.");
        Console.WriteLine($"Matched {_factionEmojiByName.Count}/{TabsMatch.AllFactions.Length} faction emojis.");
        Console.ResetColor();
    }

    private async Task RegisterCommandsAsync()
    {
        var commands = BuildCommands();
        try
        {
            if (_settings.GuildId != 0)
            {
                var guild = _client.GetGuild(_settings.GuildId);
                if (guild == null)
                {
                    Console.WriteLine($"GuildId {_settings.GuildId} was not found. Registering global commands instead.");
                    await _client.Rest.BulkOverwriteGlobalCommands(commands);
                }
                else
                {
                    await guild.BulkOverwriteApplicationCommandAsync(commands);
                    Console.WriteLine($"Registered commands in guild {guild.Name}.");
                }
            }
            else
            {
                await _client.Rest.BulkOverwriteGlobalCommands(commands);
                Console.WriteLine("Registered global commands. Global command visibility can take a little while.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Command registration failed: {ex.Message}");
        }
    }

    private void RefreshFactionEmojiMap()
    {
        var emotes = _client.Guilds.SelectMany(guild => guild.Emotes).ToList();
        var matched = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string faction in TabsMatch.AllFactions)
        {
            string factionKey = DiscordMatchRenderer.NormalizeEmojiKey(faction);
            var emote = emotes.FirstOrDefault(candidate => DiscordMatchRenderer.NormalizeEmojiKey(candidate.Name) == factionKey)
                ?? emotes.FirstOrDefault(candidate =>
                {
                    string emojiKey = DiscordMatchRenderer.NormalizeEmojiKey(candidate.Name);
                    return emojiKey.EndsWith(factionKey, StringComparison.Ordinal) ||
                           emojiKey.Contains(factionKey, StringComparison.Ordinal);
                });

            if (emote != null)
                matched[factionKey] = emote.ToString();
        }

        _factionEmojiByName = matched;
    }

    private static ApplicationCommandProperties[] BuildCommands()
    {
        var startCommand = new SlashCommandBuilder()
            .WithName("tabs-start")
            .WithDescription("Open the hosted TABS Arena start menu.");

        return new[]
        {
            startCommand.Build(),
            new SlashCommandBuilder().WithName("tabs-status").WithDescription("Repost or refresh the active match.").Build(),
            new SlashCommandBuilder().WithName("tabs-guide").WithDescription("Show a quick bot guide.").Build()
        };
    }

    private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
    {
        try
        {
            switch (command.Data.Name)
            {
                case "tabs-start":
                    await HandleStartCommandAsync(command);
                    break;
                case "tabs-status":
                    await HandleStatusCommandAsync(command);
                    break;
                case "tabs-guide":
                    await command.RespondAsync(BuildGuide(), ephemeral: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            await SafeRespondAsync(command, $"Something went wrong: {ex.Message}", true);
        }
    }

    private async Task HandleStartCommandAsync(SocketSlashCommand command)
    {
        if (!UserHasHostRole(command.User))
        {
            await command.RespondAsync("Only players with the `Host` role can create or manage hosted matches.", ephemeral: true);
            return;
        }

        var setup = new MatchSetupSession
        {
            ChannelId = command.Channel.Id,
            HostUserId = command.User.Id,
            HostDisplayName = DisplayName(command.User)
        };

        _setupSessions[(command.Channel.Id, command.User.Id)] = setup;
        await command.RespondAsync(
            embed: DiscordMatchRenderer.BuildStartEmbed(command.User),
            components: DiscordMatchRenderer.BuildStartComponents(),
            ephemeral: true);
    }

    private async Task HandleStatusCommandAsync(SocketSlashCommand command)
    {
        var match = _store.GetActive(command.Channel.Id);
        if (match == null)
        {
            await command.RespondAsync("No active TABS match in this channel. Use `/tabs-start`.", ephemeral: true);
            return;
        }

        await command.RespondAsync(embed: BuildMatchEmbed(match), components: DiscordMatchRenderer.BuildMainComponents(match));
        var message = await command.GetOriginalResponseAsync();
        match.MessageId = message.Id;
        await _store.SetActiveAsync(match);
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot || message is not SocketUserMessage userMessage)
            return;

        var key = (message.Channel.Id, message.Author.Id);
        if (!_awaitingOpponentMentions.Contains(key))
        {
            await MaybeMoveMatchOverviewAfterChatAsync(message.Channel.Id);
            return;
        }

        if (message.Author is not SocketUser author)
            return;

        if (!UserHasHostRole(author))
        {
            _awaitingOpponentMentions.Remove(key);
            await message.Channel.SendMessageAsync($"{message.Author.Mention} only players with the `Host` role can create hosted matches.");
            return;
        }

        if (!_setupSessions.TryGetValue(key, out var setup))
        {
            setup = new MatchSetupSession
            {
                ChannelId = message.Channel.Id,
                HostUserId = message.Author.Id,
                HostDisplayName = DisplayName(message.Author)
            };
        }

        if (!TryResolveSetupPlayers(author, userMessage.MentionedUsers, setup.Format, out var players, out string error))
        {
            await message.Channel.SendMessageAsync($"{message.Author.Mention} {error}");
            return;
        }

        setup.Player2UserId = players[0].UserId;
        setup.Player2DisplayName = players[0].DisplayName;
        setup.InvitedUserId = setup.Player2UserId;
        setup.InvitedDisplayName = setup.Player2DisplayName;
        if (setup.Format == TabsMatchFormat.TwoVTwo)
        {
            setup.Player3UserId = players[1].UserId;
            setup.Player3DisplayName = players[1].DisplayName;
            setup.Player4UserId = players[2].UserId;
            setup.Player4DisplayName = players[2].DisplayName;
        }
        else
        {
            setup.Player3UserId = 0;
            setup.Player3DisplayName = "";
            setup.Player4UserId = 0;
            setup.Player4DisplayName = "";
        }

        _setupSessions[key] = setup;
        _awaitingOpponentMentions.Remove(key);
        await DeleteStoredSetupPromptAsync(key);
        _ = DeleteMessageAfterDelayAsync(message, TimeSpan.FromSeconds(1));

        string setText = setup.Format == TabsMatchFormat.OneVOne
            ? $"opponent set to <@{setup.Player2UserId}>."
            : $"players set: red teammate <@{setup.Player2UserId}>, blue players <@{setup.Player3UserId}> and <@{setup.Player4UserId}>.";

        await message.Channel.SendMessageAsync(
            text: $"{message.Author.Mention} {setText}",
            embed: DiscordMatchRenderer.BuildSetupEmbed(setup),
            components: DiscordMatchRenderer.BuildSetupComponents(setup));
    }

    private async Task HandleSavesCommandAsync(SocketSlashCommand command)
    {
        var saves = _store.ListSaves(command.User.Id);
        string text = saves.Count == 0 ? "You do not have any hosted saves yet." : string.Join("\n", saves.Select(s => $"• `{s}`"));
        await command.RespondAsync(text, ephemeral: true);
    }

    private async Task ButtonExecutedAsync(SocketMessageComponent component)
    {
        string id = component.Data.CustomId;
        if (!id.StartsWith("tabs:", StringComparison.Ordinal))
            return;

        var parts = id.Split(':');
        if (parts.Length >= 2 && parts[1] == "cancel")
        {
            await CancelPromptAsync(component);
            return;
        }

        if (parts.Length >= 2 && parts[1] == "start")
        {
            await HandleStartButtonAsync(component, parts.ElementAtOrDefault(2) ?? "");
            return;
        }

        if (parts.Length >= 3 && parts[1] == "confirm" && parts[2] == "delete")
        {
            await HandleDeleteSaveConfirmAsync(component);
            return;
        }

        if (parts.Length >= 3 && parts[1] == "setup" && parts[2] == "invite")
        {
            await HandleSendInviteAsync(component);
            return;
        }

        if (parts.Length >= 3 && parts[1] == "setupformat")
        {
            await HandleFormatPromptButtonAsync(component, parts[2]);
            return;
        }

        if (parts.Length >= 3 && parts[1] == "setup" && parts[2] == "players")
        {
            await HandleSetPlayersPromptAsync(component);
            return;
        }

        if (parts.Length >= 4 && parts[1] == "invite")
        {
            await HandleInviteButtonAsync(component, parts[2], ulong.Parse(parts[3]));
            return;
        }

        if (parts.Length >= 4 && parts[1] == "arrange")
        {
            await HandleTeamArrangementButtonAsync(component, parts);
            return;
        }

        var match = ResolveMatch(component);
        if (match == null)
        {
            await RespondTemporaryAsync(component, "No active TABS match in this channel. Use `/tabs-start`.");
            return;
        }

        if (!CanUseMatch(component.User.Id, match))
        {
            await RespondTemporaryAsync(component, "Only the host and assigned match players can use this match.");
            return;
        }

        if (match.MatchEndAnnounced)
        {
            await RespondTemporaryAsync(component, "This game is over. The match will be deleted shortly.");
            return;
        }

        try
        {
            if (parts.Length >= 5 && parts[1] == "chooseconfirm")
            {
                await HandleChosenFactionConfirmAsync(component, match, parts);
                return;
            }

            if (parts.Length >= 2 && parts[1] == "saves")
            {
                await HandleSaveHubButtonAsync(component, match, parts.ElementAtOrDefault(2) ?? "");
                return;
            }

            if (parts.Length >= 2 && parts[1] == "save")
            {
                if (!IsHost(component.User.Id, match))
                {
                    await RespondTemporaryAsync(component, "Only the host can save this match.");
                    return;
                }

                await OpenSaveModalAsync(component, match);
                return;
            }

            if (parts.Length >= 2 && parts[1] == "forfeit")
            {
                var player = match.Players.FirstOrDefault(candidate => candidate.DiscordUserId == component.User.Id);
                if (player == null)
                {
                    await RespondTemporaryAsync(component, "Only an assigned player can forfeit their own side.");
                    return;
                }

                await PromptForfeitConfirmAsync(component, match, player);
                return;
            }

            if (parts.Length >= 2 && parts[1] == "closematch")
            {
                if (!IsHost(component.User.Id, match))
                {
                    await RespondTemporaryAsync(component, "Only the host can close this match.");
                    return;
                }

                await PromptCloseMatchConfirmAsync(component);
                return;
            }

            if (parts.Length >= 3 && parts[1] == "turnopen")
            {
                int player = int.Parse(parts[2]);
                var turnPlayer = match.RequirePlayer(player);
                bool isActiveTurn = match.FirstTurnChosen &&
                                    !match.TurnsComplete &&
                                    match.ActiveTurnTeam == match.TeamOfPlayer(player) &&
                                    !match.EndedTurnPlayers.Contains(player);
                bool canOpenTurnControls = turnPlayer.DiscordUserId == component.User.Id ||
                                           IsHost(component.User.Id, match);
                if (!canOpenTurnControls || !isActiveTurn)
                {
                    await RespondTemporaryAsync(component, "These turn controls are only available to the active player or the host.");
                    return;
                }

                await component.RespondAsync(
                    embed: BuildPlayerEmbed(match, player),
                    components: BuildPlayerComponentsForUser(match, player, component.User.Id),
                    ephemeral: true);
                TrackPlayerPanel(component, player, isActionsView: true);
                return;
            }

            if (parts.Length >= 3 && parts[1] == "panel")
            {
                int player = int.Parse(parts[2]);
                if (!CanUsePlayerControls(component.User.Id, match, player))
                {
                    await RespondTemporaryAsync(component, "You can only use your own player actions.");
                    return;
                }

                await component.RespondAsync(embed: BuildPlayerEmbed(match, player), components: BuildPlayerComponentsForUser(match, player, component.User.Id), ephemeral: true);
                TrackPlayerPanel(component, player, isActionsView: true);
                return;
            }

            if (parts.Length >= 3 && parts[1] == "playermenu")
            {
                int player = int.Parse(parts[2]);
                if (!CanUsePlayerControls(component.User.Id, match, player))
                {
                    await RespondTemporaryAsync(component, "You can only use your own player actions.");
                    return;
                }

                await component.UpdateAsync(properties =>
                {
                    properties.Embed = BuildPlayerEmbed(match, player);
                    properties.Components = BuildPlayerComponentsForUser(match, player, component.User.Id);
                });
                TrackPlayerPanel(component, player, isActionsView: true);
                return;
            }

            if (parts.Length >= 3 && parts[1] == "army")
            {
                await HandleArmyButtonAsync(component, match, parts);
                return;
            }

            if (parts.Length >= 2 && parts[1] == "modes")
            {
                if (!IsHost(component.User.Id, match))
                {
                    await RespondTemporaryAsync(component, "Only the host can use mode controls.");
                    return;
                }

                await component.RespondAsync("Mode controls reset the match and lock after round 1 advances.", components: DiscordMatchRenderer.BuildModeComponents(match), ephemeral: true);
                return;
            }

            if (parts.Length >= 4 && parts[1] == "act")
            {
                int player = int.Parse(parts[2]);
                string action = parts[3];
                if (!CanUsePlayerControls(component.User.Id, match, player))
                {
                    await RespondTemporaryAsync(component, "You can only use your own player actions.");
                    return;
                }

                bool isReplay = action == "replay";
                if (!isReplay && !CanUseTurnAction(component.User.Id, match, player, out string turnError))
                {
                    await RespondTemporaryAsync(component, turnError);
                    return;
                }

                if (isReplay && !CanUseReplayAction(match, out string replayError))
                {
                    await RespondTemporaryAsync(component, replayError);
                    return;
                }

                await HandlePlayerActionButtonAsync(component, match, player, action);
                return;
            }

            if (parts.Length >= 3 && parts[1] == "endturn")
            {
                int player = int.Parse(parts[2]);
                if (!CanUsePlayerControls(component.User.Id, match, player))
                {
                    await RespondTemporaryAsync(component, "You can only end turns for your own player actions.");
                    return;
                }

                await HandleEndTurnAsync(component, match, player);
                return;
            }

            if (parts.Length >= 3 && parts[1] == "timer" && !IsHost(component.User.Id, match))
            {
                string timerAction = parts[2];
                bool canStart = timerAction == "toggle" &&
                                match.CanUseTieTimer &&
                                !match.TimerRunning &&
                                match.GetTimerRemainingSeconds() == 120;
                if (!canStart)
                {
                    await RespondTemporaryAsync(component, "Only the host can stop, resume, or restart the tie timer.");
                    return;
                }

                await component.DeferAsync(ephemeral: true);
                MutationResult timerResult = match.StartTimer();
                await _store.SetActiveAsync(match);
                await UpdateMatchMessageAsync(match);
                if (timerResult.Success)
                    await AnnounceTieTimerStartedAsync(match);
                await FollowupTemporaryAsync(component, timerResult.Message);
                return;
            }

            if (parts.Length >= 5 && parts[1] == "confirm" && parts[2] == "forfeit")
            {
                await HandleForfeitConfirmAsync(component, match, parts);
                return;
            }

            if (!IsHost(component.User.Id, match))
            {
                await RespondTemporaryAsync(component, "Only the host can use that match control.");
                return;
            }

            if (parts.Length >= 3 && parts[1] == "confirm" && parts[2] == "newgame")
            {
                await HandleNewGameConfirmAsync(component, match);
                return;
            }

            if (parts.Length >= 3 && parts[1] == "confirm" && parts[2] == "closematch")
            {
                await HandleCloseMatchConfirmAsync(component, match);
                return;
            }

            if (parts.Length >= 2 && parts[1] == "newgame")
            {
                await PromptNewGameConfirmAsync(component, updateExisting: false);
                return;
            }

            if (match.NeedsFirstTurnChoice && parts.ElementAtOrDefault(1) != "first")
            {
                await RespondTemporaryAsync(component, "The host must choose who goes first before match controls are available.");
                return;
            }

            bool announceTieTimerStart =
                parts[1] == "timer" &&
                parts.ElementAtOrDefault(2) == "toggle" &&
                match.CanUseTieTimer &&
                !match.TimerRunning &&
                match.GetTimerRemainingSeconds() == 120;

            await component.DeferAsync(ephemeral: true);
            MutationResult result = parts[1] switch
            {
                "win" => match.MarkWinner(int.Parse(parts[2])),
                "next" => match.NextRound(),
                "first" => match.ChooseFirst(int.Parse(parts[2])),
                "undo" => match.Undo(),
                "timer" => HandleTimer(match, parts.ElementAtOrDefault(2) ?? ""),
                "mode" => HandleMode(match, parts.ElementAtOrDefault(2) ?? ""),
                _ => MutationResult.Fail("Unknown action.")
            };
            ScheduleGameDeletionIfMatchFinished(match);

            await _store.SetActiveAsync(match);
            await UpdateMatchMessageAsync(match);
            if (result.Success && announceTieTimerStart)
                await AnnounceTieTimerStartedAsync(match);
            if (result.Success && parts[1] == "next")
            {
                await DeleteTieTimerAnnouncementAsync(match.ChannelId);
            }
            if (result.Success && parts[1] is "next" or "undo" or "mode")
                await RefreshMilestoneWarningsAsync(match);
            if (match.MatchEndAnnounced)
            {
                await DeleteOpenPanelsForChannelAsync(match.ChannelId);
                await DeleteTurnPanelsForChannelAsync(match.ChannelId);
                await DeleteTieTimerAnnouncementAsync(match.ChannelId);
                await DeleteMilestoneWarningsAsync(match.ChannelId);
            }
            else
            {
                await RefreshOpenPlayerPanelsAsync(match);
                if (parts[1] is "first" or "next" or "undo" or "mode")
                    await RefreshTurnPanelsAsync(match);
            }
            await FollowupTemporaryAsync(component, result.Message);
        }
        catch (Exception ex)
        {
            if (!component.HasResponded)
                await RespondTemporaryAsync(component, $"Something went wrong: {ex.Message}");
            else
                await FollowupTemporaryAsync(component, $"Something went wrong: {ex.Message}");
        }
    }

    private async Task HandleStartButtonAsync(SocketMessageComponent component, string action)
    {
        if (!UserHasHostRole(component.User))
        {
            await RespondTemporaryAsync(component, "Only players with the `Host` role can create or manage hosted matches.");
            return;
        }

        var key = (component.Channel.Id, component.User.Id);
        if (!_setupSessions.TryGetValue(key, out var setup))
        {
            setup = new MatchSetupSession
            {
                ChannelId = component.Channel.Id,
                HostUserId = component.User.Id,
                HostDisplayName = DisplayName(component.User)
            };
            _setupSessions[key] = setup;
        }

        switch (action)
        {
            case "create":
                setup = new MatchSetupSession
                {
                    ChannelId = component.Channel.Id,
                    MatchmakingChannelId = IsMatchmakingChannel(component.Channel) ? component.Channel.Id : 0,
                    UsesPrivateMatchChannel = IsMatchmakingChannel(component.Channel),
                    HostUserId = component.User.Id,
                    HostDisplayName = DisplayName(component.User)
                };
                _setupSessions[key] = setup;
                _awaitingOpponentMentions.Remove(key);
                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildFormatPromptEmbed(setup);
                    properties.Components = DiscordMatchRenderer.BuildFormatPromptComponents();
                });
                return;

            case "load":
                var saves = _store.ListSaves(component.User.Id);
                if (saves.Count == 0)
                {
                    await RespondTemporaryAsync(component, "You do not have any hosted saves yet.");
                    return;
                }

                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildSavePickerEmbed("Load Game", "Choose one of your hosted saves to load into this channel.");
                    properties.Components = DiscordMatchRenderer.BuildSavePickerComponents(saves, "tabs:startload");
                });
                return;

            case "delete":
                var deleteSaves = _store.ListSaves(component.User.Id);
                if (deleteSaves.Count == 0)
                {
                    await RespondTemporaryAsync(component, "You do not have any hosted saves to delete.");
                    return;
                }

                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildSavePickerEmbed("Delete Save", "Choose one of your hosted saves to delete.");
                    properties.Components = DiscordMatchRenderer.BuildSavePickerComponents(deleteSaves, "tabs:startdelete");
                });
                return;

        }

        await RespondTemporaryAsync(component, "Unknown start action.");
    }

    private async Task HandleFormatPromptButtonAsync(SocketMessageComponent component, string value)
    {
        if (!UserHasHostRole(component.User))
        {
            await RespondTemporaryAsync(component, "Only players with the `Host` role can create hosted matches.");
            return;
        }

        var key = (component.Channel.Id, component.User.Id);
        if (!_setupSessions.TryGetValue(key, out var setup))
        {
            setup = new MatchSetupSession
            {
                ChannelId = component.Channel.Id,
                HostUserId = component.User.Id,
                HostDisplayName = DisplayName(component.User)
            };
        }

        setup.Format = value == "2v2" ? TabsMatchFormat.TwoVTwo : TabsMatchFormat.OneVOne;
        setup.Player2UserId = 0;
        setup.Player2DisplayName = "";
        setup.Player3UserId = 0;
        setup.Player3DisplayName = "";
        setup.Player4UserId = 0;
        setup.Player4DisplayName = "";
        setup.InvitedUserId = 0;
        setup.InvitedDisplayName = "";
        _setupSessions[key] = setup;
        _awaitingOpponentMentions.Add(key);

        await component.UpdateAsync(properties =>
        {
            properties.Embed = DiscordMatchRenderer.BuildOpponentChatPromptEmbed(setup);
            properties.Components = DiscordMatchRenderer.BuildCancelOnlyComponents();
        });
        _setupPromptInteractions[key] = component;
    }

    private async Task HandleSetPlayersPromptAsync(SocketMessageComponent component)
    {
        if (!UserHasHostRole(component.User))
        {
            await RespondTemporaryAsync(component, "Only players with the `Host` role can set hosted match players.");
            return;
        }

        var key = (component.Channel.Id, component.User.Id);
        if (!_setupSessions.TryGetValue(key, out var setup))
        {
            await RespondTemporaryAsync(component, "No setup is active. Use `/tabs-start` first.");
            return;
        }

        _awaitingOpponentMentions.Add(key);
        await component.UpdateAsync(properties =>
        {
            properties.Embed = DiscordMatchRenderer.BuildOpponentChatPromptEmbed(setup);
            properties.Components = DiscordMatchRenderer.BuildCancelOnlyComponents();
        });
        _setupPromptInteractions[key] = component;
    }

    private async Task HandleSaveHubButtonAsync(SocketMessageComponent component, TabsMatch match, string action)
    {
        if (!IsHost(component.User.Id, match))
        {
            await RespondTemporaryAsync(component, "Only the host can manage saves for this match.");
            return;
        }

        switch (action)
        {
            case "":
                await component.RespondAsync(
                    embed: DiscordMatchRenderer.BuildSaveHubEmbed(match),
                    components: DiscordMatchRenderer.BuildSaveHubComponents(),
                    ephemeral: true);
                TrackPlayerPanel(component, 0, isActionsView: false);
                return;

            case "newgame":
                await PromptNewGameConfirmAsync(component, updateExisting: true);
                return;

            case "load":
                var saves = _store.ListSaves(component.User.Id);
                if (saves.Count == 0)
                {
                    await RespondTemporaryAsync(component, "You do not have any hosted saves yet.");
                    return;
                }

                await component.UpdateAsync(properties =>
                {
                    properties.Content = null;
                    properties.Embed = DiscordMatchRenderer.BuildSavePickerEmbed("Load Save", "Choose one of your hosted saves to load into this match.");
                    properties.Components = DiscordMatchRenderer.BuildSavePickerComponents(saves, "tabs:matchload");
                });
                return;

            case "delete":
                var deleteSaves = _store.ListSaves(component.User.Id);
                if (deleteSaves.Count == 0)
                {
                    await RespondTemporaryAsync(component, "You do not have any hosted saves to delete.");
                    return;
                }

                await component.UpdateAsync(properties =>
                {
                    properties.Content = null;
                    properties.Embed = DiscordMatchRenderer.BuildSavePickerEmbed("Delete Save", "Choose one of your hosted saves to delete.");
                    properties.Components = DiscordMatchRenderer.BuildSavePickerComponents(deleteSaves, "tabs:matchdelete");
                });
                return;

            case "save":
                await OpenSaveModalAsync(component, match);
                return;
        }

        await RespondTemporaryAsync(component, "Unknown saves action.");
    }

    private async Task PromptNewGameConfirmAsync(SocketMessageComponent component, bool updateExisting)
    {
        var embed = DiscordMatchRenderer.BuildConfirmEmbed(
            "Start New Game?",
            "Are you sure you want to reset this match with the current mode settings?");
        var components = DiscordMatchRenderer.BuildConfirmComponents("tabs:confirm:newgame", "New Game");

        if (updateExisting)
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = null;
                properties.Embed = embed;
                properties.Components = components;
            });
        }
        else
        {
            await component.RespondAsync(embed: embed, components: components, ephemeral: true);
        }
    }

    private async Task PromptCloseMatchConfirmAsync(SocketMessageComponent component)
    {
        var embed = DiscordMatchRenderer.BuildConfirmEmbed(
            "Close Match?",
            "This will delete the entire match and any unsaved progress. Save the match first if you want to keep it.");
        var components = DiscordMatchRenderer.BuildConfirmComponents("tabs:confirm:closematch", "Close Match");
        await component.RespondAsync(embed: embed, components: components, ephemeral: true);
    }

    private async Task PromptForfeitConfirmAsync(SocketMessageComponent component, TabsMatch match, PlayerState player)
    {
        int forfeitingTeam = match.TeamOfPlayer(player.Id);
        int winningTeam = forfeitingTeam == 1 ? 2 : 1;
        var embed = DiscordMatchRenderer.BuildConfirmEmbed(
            "Forfeit Match?",
            $"This will immediately end the match and give {match.TeamName(winningTeam)} the win. This cannot be undone.");
        var components = DiscordMatchRenderer.BuildConfirmComponents(
            $"tabs:confirm:forfeit:{player.Id}:{component.User.Id}",
            "Forfeit Match");
        await component.RespondAsync(embed: embed, components: components, ephemeral: true);
    }

    private async Task HandleForfeitConfirmAsync(SocketMessageComponent component, TabsMatch match, string[] parts)
    {
        if (!int.TryParse(parts[3], out int playerId) ||
            !ulong.TryParse(parts[4], out ulong confirmingUserId) ||
            confirmingUserId != component.User.Id)
        {
            await RespondTemporaryAsync(component, "This forfeit confirmation does not belong to you.");
            return;
        }

        var player = match.GetPlayer(playerId);
        if (player == null || player.DiscordUserId != component.User.Id)
        {
            await RespondTemporaryAsync(component, "You can only forfeit your own side.");
            return;
        }

        MutationResult result = match.Forfeit(playerId);
        if (!result.Success)
        {
            await RespondTemporaryAsync(component, result.Message);
            return;
        }

        ScheduleGameDeletionIfMatchFinished(match);
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await DeleteOpenPanelsForChannelAsync(match.ChannelId);
        await DeleteTurnPanelsForChannelAsync(match.ChannelId);
        await DeleteTieTimerAnnouncementAsync(match.ChannelId);
        await DeleteMilestoneWarningsAsync(match.ChannelId);

        await component.UpdateAsync(properties =>
        {
            properties.Content = result.Message;
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        _ = DeleteOriginalResponseAfterDelayAsync(component);
    }

    private async Task HandleNewGameConfirmAsync(SocketMessageComponent component, TabsMatch match)
    {
        MutationResult result = NewGame(match);
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await RefreshOpenPlayerPanelsAsync(match);
        await RefreshTurnPanelsAsync(match);
        await DeleteTieTimerAnnouncementAsync(match.ChannelId);
        await DeleteMilestoneWarningsAsync(match.ChannelId);

        await component.UpdateAsync(properties =>
        {
            properties.Content = result.Message;
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        _ = DeleteOriginalResponseAfterDelayAsync(component);
    }

    private async Task HandleCloseMatchConfirmAsync(SocketMessageComponent component, TabsMatch match)
    {
        if (!IsHost(component.User.Id, match))
        {
            await RespondTemporaryAsync(component, "Only the host can close this match.");
            return;
        }

        var key = (component.Channel.Id, component.User.Id);
        await DeleteMatchMessageAsync(match);
        await _store.ClearActiveAsync(component.Channel.Id);
        await DeleteOpenPanelsForChannelAsync(component.Channel.Id);
        await DeleteTurnPanelsForChannelAsync(component.Channel.Id);
        await DeleteTieTimerAnnouncementAsync(component.Channel.Id);
        await DeleteMilestoneWarningsAsync(component.Channel.Id);
        _pendingSaveDeletes.Remove(key);
        _awaitingOpponentMentions.Remove(key);
        _setupSessions.Remove(key);

        await component.UpdateAsync(properties =>
        {
            if (match.IsPrivateMatchChannel)
            {
                properties.Content = "Match closed. This private match channel will be deleted.";
                properties.Embed = null;
                properties.Components = new ComponentBuilder().Build();
            }
            else
            {
                properties.Content = "Match closed and deleted.";
                properties.Embed = null;
                properties.Components = new ComponentBuilder().Build();
            }
        });

        if (match.IsPrivateMatchChannel)
            _ = DeletePrivateMatchChannelsAfterDelayAsync(match.ChannelId, match.PrivateVoiceChannelId, TimeSpan.FromSeconds(3));
    }

    private async Task HandleMatchLoadSelectAsync(SocketMessageComponent component)
    {
        var active = _store.GetActive(component.Channel.Id);
        if (active == null)
        {
            await RespondTemporaryAsync(component, "No active TABS match in this channel.");
            return;
        }

        if (!IsHost(component.User.Id, active))
        {
            await RespondTemporaryAsync(component, "Only the host can load saves for this match.");
            return;
        }

        string name = component.Data.Values.FirstOrDefault() ?? "";
        var loaded = await _store.LoadNamedAsync(component.User.Id, component.Channel.Id, name);
        if (loaded == null)
        {
            await RespondTemporaryAsync(component, $"No save named `{name}` was found under your host saves.");
            return;
        }

        loaded.HostUserId = component.User.Id;
        loaded.HostDisplayName = DisplayName(component.User);
        loaded.MessageId = active.MessageId;
        loaded.LoadedSaveName = MatchStore.NormalizeSaveName(name);
        loaded.IsPrivateMatchChannel = active.IsPrivateMatchChannel;
        loaded.MatchmakingChannelId = active.MatchmakingChannelId;
        loaded.PrivateVoiceChannelId = active.PrivateVoiceChannelId;
        loaded.ChannelDeleteAfterUtc = null;
        await _store.SetActiveAsync(loaded);
        await UpdateMatchMessageAsync(loaded);
        await RefreshOpenPlayerPanelsAsync(loaded);
        await RefreshTurnPanelsAsync(loaded);
        await RefreshMilestoneWarningsAsync(loaded);

        await component.UpdateAsync(properties =>
        {
            properties.Content = $"Loaded hosted save `{name}` into this match.";
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        _ = DeleteOriginalResponseAfterDelayAsync(component);
    }

    private async Task HandleMatchDeleteSelectAsync(SocketMessageComponent component)
    {
        var active = _store.GetActive(component.Channel.Id);
        if (active == null)
        {
            await RespondTemporaryAsync(component, "No active TABS match in this channel.");
            return;
        }

        if (!IsHost(component.User.Id, active))
        {
            await RespondTemporaryAsync(component, "Only the host can delete saves for this match.");
            return;
        }

        string name = component.Data.Values.FirstOrDefault() ?? "";
        _pendingSaveDeletes[(component.Channel.Id, component.User.Id)] = MatchStore.NormalizeSaveName(name);
        await component.UpdateAsync(properties =>
        {
            properties.Content = null;
            properties.Embed = DiscordMatchRenderer.BuildConfirmEmbed("Delete Save?", $"Are you sure you want to delete `{name}`?");
            properties.Components = DiscordMatchRenderer.BuildConfirmComponents("tabs:confirm:delete", "Delete Save");
        });
    }

    private async Task HandleDeleteSaveConfirmAsync(SocketMessageComponent component)
    {
        if (!UserHasHostRole(component.User))
        {
            await RespondTemporaryAsync(component, "Only players with the `Host` role can manage hosted saves.");
            return;
        }

        var key = (component.Channel.Id, component.User.Id);
        if (!_pendingSaveDeletes.TryGetValue(key, out string? name))
        {
            await RespondTemporaryAsync(component, "No save delete is waiting for confirmation.");
            return;
        }

        var active = _store.GetActive(component.Channel.Id);
        var savedMatch = active == null ? null : await _store.LoadNamedAsync(component.User.Id, component.Channel.Id, name);
        _pendingSaveDeletes.Remove(key);
        _setupSessions.Remove(key);
        bool deleted = _store.DeleteSave(component.User.Id, name);
        bool deletedCurrentMatch = deleted &&
            active != null &&
            IsHost(component.User.Id, active) &&
            (string.Equals(active.LoadedSaveName, name, StringComparison.OrdinalIgnoreCase) ||
             (savedMatch != null && string.Equals(active.Id, savedMatch.Id, StringComparison.OrdinalIgnoreCase)));

        if (deletedCurrentMatch && active != null)
        {
            await DeleteMatchMessageAsync(active);
            await _store.ClearActiveAsync(component.Channel.Id);
            await DeleteOpenPanelsForChannelAsync(component.Channel.Id, component);
            await DeleteTurnPanelsForChannelAsync(component.Channel.Id);
            await DeleteTieTimerAnnouncementAsync(component.Channel.Id);
            await DeleteMilestoneWarningsAsync(component.Channel.Id);

            _setupSessions[key] = new MatchSetupSession
            {
                ChannelId = component.Channel.Id,
                HostUserId = component.User.Id,
                HostDisplayName = DisplayName(component.User)
            };

            await component.UpdateAsync(properties =>
            {
                if (active.IsPrivateMatchChannel)
                {
                    properties.Content = $"Deleted hosted save `{name}`. This private match channel will be deleted.";
                    properties.Embed = null;
                    properties.Components = new ComponentBuilder().Build();
                }
                else
                {
                    properties.Content = $"Deleted hosted save `{name}`. The active match UI was removed because it was loaded from that save.";
                    properties.Embed = DiscordMatchRenderer.BuildStartEmbed(component.User);
                    properties.Components = DiscordMatchRenderer.BuildStartComponents();
                }
            });
            if (active.IsPrivateMatchChannel)
                _ = DeletePrivateMatchChannelsAfterDelayAsync(active.ChannelId, active.PrivateVoiceChannelId, TimeSpan.FromSeconds(3));
            return;
        }

        await component.UpdateAsync(properties =>
        {
            properties.Content = deleted ? $"Deleted hosted save `{name}`." : $"No save named `{name}` was found.";
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        _ = DeleteOriginalResponseAfterDelayAsync(component);
    }

    private static async Task OpenSaveModalAsync(SocketMessageComponent component, TabsMatch match)
    {
        var modal = new ModalBuilder()
            .WithTitle("Save Match")
            .WithCustomId("tabs:modal:save")
            .AddTextInput("Save name", "name", TextInputStyle.Short, placeholder: $"round-{match.Round}-{match.ModeLabel}", required: true)
            .Build();
        await component.RespondWithModalAsync(modal);
    }

    private async Task HandleSendInviteAsync(SocketMessageComponent component)
    {
        if (!UserHasHostRole(component.User))
        {
            await RespondTemporaryAsync(component, "Only players with the `Host` role can send hosted match invites.");
            return;
        }

        var key = (component.Channel.Id, component.User.Id);
        if (!_setupSessions.TryGetValue(key, out var setup))
        {
            await RespondTemporaryAsync(component, "No setup is active. Use `/tabs-start` first.");
            return;
        }

        if (!setup.HasRequiredPlayers)
        {
            string needed = setup.Format == TabsMatchFormat.OneVOne
                ? "Set an opponent before sending the invite."
                : "Set the red teammate and both blue players before sending the invite.";
            await RespondTemporaryAsync(component, needed);
            return;
        }

        var invite = new PendingInvite
        {
            ChannelId = setup.ChannelId,
            MatchmakingChannelId = setup.MatchmakingChannelId,
            UsesPrivateMatchChannel = setup.UsesPrivateMatchChannel,
            HostUserId = setup.HostUserId,
            HostDisplayName = setup.HostDisplayName,
            InvitedUserId = setup.InvitedUserId,
            InvitedDisplayName = setup.InvitedDisplayName,
            Player2UserId = setup.Player2UserId,
            Player2DisplayName = setup.Player2DisplayName,
            Player3UserId = setup.Player3UserId,
            Player3DisplayName = setup.Player3DisplayName,
            Player4UserId = setup.Player4UserId,
            Player4DisplayName = setup.Player4DisplayName,
            Format = setup.Format,
            Mode = setup.Mode,
            FactionMode = setup.FactionMode
        };
        _pendingInvites[key] = invite;
        await DeleteStoredSetupPromptAsync(key);

        string mentions = string.Join(" ", invite.RequiredInviteUserIds.Select(id => $"<@{id}>"));
        await component.Channel.SendMessageAsync(
            text: mentions,
            embed: DiscordMatchRenderer.BuildInviteEmbed(invite),
            components: DiscordMatchRenderer.BuildInviteComponents(setup.HostUserId));

        await component.UpdateAsync(properties =>
        {
            properties.Content = invite.Format == TabsMatchFormat.OneVOne
                ? "Invite sent. The match will start after they accept."
                : "Invite sent. The match will start after all 3 invited players accept.";
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
    }

    private async Task HandleInviteButtonAsync(SocketMessageComponent component, string action, ulong hostUserId)
    {
        var key = (component.Channel.Id, hostUserId);
        if (!_pendingInvites.TryGetValue(key, out var invite))
        {
            await RespondTemporaryAsync(component, "That invite is no longer active.");
            return;
        }

        if (action == "cancel")
        {
            if (component.User.Id != invite.HostUserId)
            {
                await RespondTemporaryAsync(component, "Only the host can cancel this match invite.");
                return;
            }

            await component.DeferAsync(ephemeral: true);
            _pendingInvites.Remove(key);
            _setupSessions.Remove(key);
            await component.Message.ModifyAsync(properties =>
            {
                properties.Content = $"<@{invite.HostUserId}> cancelled the TABS Arena invite.";
                properties.Embed = null;
                properties.Components = new ComponentBuilder().Build();
            });
            await FollowupTemporaryAsync(component, "Invite cancelled.");
            if (IsActualPrivateMatchSetupChannel(invite))
                _ = DeleteChannelAfterDelayAsync(invite.ChannelId, TimeSpan.FromSeconds(3));
            else if (invite.UsesPrivateMatchChannel && component.Channel is SocketTextChannel cancelLobbyChannel)
                _ = DeleteMatchmakingMatchHistoryAsync(cancelLobbyChannel, invite);
            return;
        }

        if (!invite.RequiredInviteUserIds.Contains(component.User.Id))
        {
            await RespondTemporaryAsync(component, "Only invited players can respond to this match invite.");
            return;
        }

        await component.DeferAsync(ephemeral: true);

        if (action == "decline")
        {
            _pendingInvites.Remove(key);
            await component.Message.ModifyAsync(properties =>
            {
                properties.Content = $"<@{component.User.Id}> declined the TABS Arena invite from <@{invite.HostUserId}>.";
                properties.Embed = null;
                properties.Components = new ComponentBuilder().Build();
            });
            await FollowupTemporaryAsync(component, "Invite declined.");
            if (IsActualPrivateMatchSetupChannel(invite))
                _ = DeleteChannelAfterDelayAsync(invite.ChannelId, TimeSpan.FromSeconds(3));
            else if (invite.UsesPrivateMatchChannel && component.Channel is SocketTextChannel declineLobbyChannel)
                _ = DeleteMatchmakingMatchHistoryAsync(declineLobbyChannel, invite);
            return;
        }

        invite.AcceptedUserIds.Add(component.User.Id);
        if (!invite.IsFullyAccepted)
        {
            await component.Message.ModifyAsync(properties =>
            {
                properties.Embed = DiscordMatchRenderer.BuildInviteEmbed(invite);
                properties.Components = DiscordMatchRenderer.BuildInviteComponents(invite.HostUserId);
            });
            await FollowupTemporaryAsync(component, "Accepted. Waiting for the remaining invited players.");
            return;
        }

        if (invite.Format == TabsMatchFormat.TwoVTwo)
        {
            invite.EnsureTeamAssignments();
            await component.Message.ModifyAsync(properties =>
            {
                properties.Content = "All players accepted. Arrange teams, then start the match.";
                properties.Embed = DiscordMatchRenderer.BuildTeamArrangementEmbed(invite);
                properties.Components = DiscordMatchRenderer.BuildTeamArrangementComponents(invite);
            });
            await FollowupTemporaryAsync(component, "All players accepted. Arrange teams, then start the match.");
            return;
        }

        await CreateAcceptedMatchOnceAsync(component, invite, key);
    }

    private async Task HandleTeamArrangementButtonAsync(SocketMessageComponent component, string[] parts)
    {
        string action = parts[2];
        ulong hostUserId = ulong.Parse(parts[3]);
        var key = (component.Channel.Id, hostUserId);
        if (!_pendingInvites.TryGetValue(key, out var invite))
        {
            await RespondTemporaryAsync(component, "That team arrangement is no longer active.");
            return;
        }

        if (!invite.AllParticipantUserIds.Contains(component.User.Id))
        {
            await RespondTemporaryAsync(component, "Only players in this match can arrange teams.");
            return;
        }

        invite.EnsureTeamAssignments();
        if (action == "toggle")
        {
            if (parts.Length < 5 || !ulong.TryParse(parts[4], out ulong targetUserId) || !invite.AllParticipantUserIds.Contains(targetUserId))
            {
                await RespondTemporaryAsync(component, "Choose a valid player to move.");
                return;
            }

            if (component.User.Id != invite.HostUserId && component.User.Id != targetUserId)
            {
                await RespondTemporaryAsync(component, "You can only move yourself. The host can move anyone.");
                return;
            }

            invite.ToggleTeam(targetUserId);
            await component.UpdateAsync(properties =>
            {
                properties.Content = "All players accepted. Arrange teams, then start the match.";
                properties.Embed = DiscordMatchRenderer.BuildTeamArrangementEmbed(invite);
                properties.Components = DiscordMatchRenderer.BuildTeamArrangementComponents(invite);
            });
            return;
        }

        if (action == "start")
        {
            int redCount = invite.TeamAssignments.Count(kvp => kvp.Value == 1);
            int blueCount = invite.TeamAssignments.Count(kvp => kvp.Value == 2);
            if (redCount != 2 || blueCount != 2)
            {
                await RespondTemporaryAsync(component, "2v2 needs to have at least 2 players on each team to start.");
                return;
            }

            await component.DeferAsync(ephemeral: true);
            await CreateAcceptedMatchOnceAsync(component, invite, key);
            return;
        }

        await RespondTemporaryAsync(component, "Unknown team arrangement action.");
    }

    private async Task CreateAcceptedMatchOnceAsync(SocketMessageComponent component, PendingInvite invite, (ulong ChannelId, ulong HostUserId) key)
    {
        if (!_matchCreationsInProgress.Add(key))
        {
            await FollowupTemporaryAsync(component, "This match is already being created.");
            return;
        }

        try
        {
            await CreateAcceptedMatchAsync(component, invite, key);
        }
        finally
        {
            _matchCreationsInProgress.Remove(key);
        }
    }

    private async Task CreateAcceptedMatchAsync(SocketMessageComponent component, PendingInvite invite, (ulong ChannelId, ulong HostUserId) key)
    {
        IMessageChannel matchChannel = component.Channel;
        ulong matchChannelId = component.Channel.Id;
        ulong privateVoiceChannelId = 0;
        bool matchUsesPrivateChannel = invite.UsesPrivateMatchChannel;
        SocketTextChannel? matchmakingCleanupChannel = null;
        if (invite.UsesPrivateMatchChannel)
        {
            if (component.Channel is not SocketTextChannel lobbyChannel)
            {
                await FollowupTemporaryAsync(component, "I could not create the private match room from this channel.");
                return;
            }

            try
            {
                var hostUser = lobbyChannel.Guild.GetUser(invite.HostUserId) ?? component.User;
                var privateChannels = await CreatePrivateMatchChannelsAsync(lobbyChannel, hostUser, invite);
                matchChannel = privateChannels.TextChannel;
                matchChannelId = privateChannels.TextChannel.Id;
                privateVoiceChannelId = privateChannels.VoiceChannel?.Id ?? 0;
                matchmakingCleanupChannel = lobbyChannel;
                if (!string.IsNullOrWhiteSpace(privateChannels.VoiceError))
                    Console.WriteLine($"Private voice channel creation failed: {privateChannels.VoiceError}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Private match channel creation failed: {ex}");
                await FollowupPersistentAsync(component, $"I could not create the private match channel. Check that the bot has Manage Channels and can create channels in this category. `{ex.Message}`");
                return;
            }
        }

        TabsMatch match;
        IUserMessage message;
        try
        {
            var slots = BuildMatchSlots(invite);
            match = TabsMatch.Create(
                matchChannelId,
                invite.Format,
                invite.Mode,
                invite.FactionMode,
                hostUserId: invite.HostUserId,
                hostDisplayName: invite.HostDisplayName,
                invitedUserId: invite.InvitedUserId,
                invitedDisplayName: invite.InvitedDisplayName,
                player1UserId: slots[0].UserId,
                player1DisplayName: slots[0].DisplayName,
                player2UserId: slots[1].UserId,
                player2DisplayName: slots[1].DisplayName,
                player3UserId: slots.ElementAtOrDefault(2).UserId,
                player3DisplayName: slots.ElementAtOrDefault(2).DisplayName,
                player4UserId: slots.ElementAtOrDefault(3).UserId,
                player4DisplayName: slots.ElementAtOrDefault(3).DisplayName,
                isPrivateMatchChannel: matchUsesPrivateChannel,
                matchmakingChannelId: invite.MatchmakingChannelId);
            match.PrivateVoiceChannelId = privateVoiceChannelId;
            if (matchUsesPrivateChannel && privateVoiceChannelId == 0)
                match.Log("Private text channel created, but the private voice channel could not be created. Check the bot's Manage Channels permission.");

            message = await matchChannel.SendMessageAsync(
                text: BuildPrivateVoiceChannelNotice(match),
                embed: BuildMatchEmbed(match),
                components: DiscordMatchRenderer.BuildMainComponents(match));

            if (matchUsesPrivateChannel)
                await SendPrivateMatchAttentionPingAsync(matchChannel, match);
        }
        catch (Exception ex)
        {
            if (matchUsesPrivateChannel && matchChannelId != component.Channel.Id)
                await DeletePrivateMatchChannelsAsync(matchChannelId, privateVoiceChannelId);

            Console.WriteLine($"Accepted match creation failed after channel creation: {ex}");
            await FollowupPersistentAsync(component, $"I created the private room but could not start the match, so I cleaned it up. `{ex.Message}`");
            return;
        }

        match.MessageId = message.Id;
        await _store.SetActiveAsync(match);
        _pendingInvites.Remove(key);
        _setupSessions.Remove(key);

        await component.Message.ModifyAsync(properties =>
        {
            properties.Content = invite.Format == TabsMatchFormat.OneVOne
                ? $"<@{invite.Player2UserId}> accepted <@{invite.HostUserId}>'s TABS Arena invite."
                : $"Teams locked. Match started.";
            if (invite.UsesPrivateMatchChannel)
                properties.Content += $" Match moved to {MentionUtils.MentionChannel(matchChannelId)}.";
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        await FollowupTemporaryAsync(
            component,
            invite.UsesPrivateMatchChannel ? "Match accepted and moved to the private match channel." : "Match accepted and created.",
            invite.UsesPrivateMatchChannel ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(2));
        if (matchmakingCleanupChannel != null)
        {
            await DeleteMatchmakingMatchHistoryAsync(matchmakingCleanupChannel, invite);
            await SendMatchmakingChannelRedirectAsync(matchmakingCleanupChannel, match);
        }
    }

    private static List<(ulong UserId, string DisplayName)> BuildMatchSlots(PendingInvite invite)
    {
        if (invite.Format == TabsMatchFormat.OneVOne)
        {
            return new List<(ulong UserId, string DisplayName)>
            {
                (invite.HostUserId, invite.HostDisplayName),
                (invite.Player2UserId, invite.Player2DisplayName)
            };
        }

        invite.EnsureTeamAssignments();
        var red = invite.AllParticipantUserIds
            .Where(userId => invite.TeamOf(userId) == 1)
            .Select(userId => (UserId: userId, DisplayName: invite.DisplayNameFor(userId)))
            .ToList();
        var blue = invite.AllParticipantUserIds
            .Where(userId => invite.TeamOf(userId) == 2)
            .Select(userId => (UserId: userId, DisplayName: invite.DisplayNameFor(userId)))
            .ToList();
        return red.Concat(blue).ToList();
    }

    private async Task HandleChosenFactionConfirmAsync(SocketMessageComponent component, TabsMatch match, string[] parts)
    {
        if (parts.Length < 5 || !int.TryParse(parts[2], out int player))
        {
            await RespondTemporaryAsync(component, "Choose a valid faction first.");
            return;
        }

        if (!CanUsePlayerControls(component.User.Id, match, player))
        {
            await RespondTemporaryAsync(component, "You can only use your own player actions.");
            return;
        }

        if (!CanUseTurnAction(component.User.Id, match, player, out string turnError))
        {
            await RespondTemporaryAsync(component, turnError);
            return;
        }

        bool free = parts[3] == "free";
        string faction = string.Join(':', parts.Skip(4));
        var result = match.BuyChosenFaction(player, faction, free);
        if (!result.Success)
        {
            await RespondTemporaryAsync(component, result.Message);
            return;
        }

        await component.UpdateAsync(properties =>
        {
            properties.Content = result.Message;
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await RefreshOpenPlayerPanelsAsync(match);
        _ = DeleteOriginalResponseAfterDelayAsync(component);
    }

    private async Task HandlePlayerActionButtonAsync(SocketMessageComponent component, TabsMatch match, int player, string action)
    {
        if (action is "spend" or "sell")
        {
            await RespondTemporaryAsync(component, "Use Army to buy tracked units or sell owned units.");
            return;
        }

        if (action == "bft")
        {
            var modal = new ModalBuilder()
                .WithTitle("BFT Unit")
                .WithCustomId($"tabs:modal:{action}:{player}")
                .AddTextInput("Unit gold", "amount", TextInputStyle.Short, placeholder: "100", required: true)
                .Build();
            await component.RespondWithModalAsync(modal);
            return;
        }

        if (action is "chosen" or "freefaction")
        {
            bool free = action == "freefaction";
            var p = match.RequirePlayer(player);
            if (!match.FactionModeEnabled)
            {
                await RespondTemporaryAsync(component, "Faction Mode is OFF.");
                return;
            }

            if (!free && p.Gold < Math.Ceiling(Math.Max(1m, match.GetChosenFactionCost(p) * (1m - p.NextChosenFactionDiscountPct / 100m))))
            {
                await RespondTemporaryAsync(component, $"{p.Name} does not have enough gold for chosen faction.");
                return;
            }

            if (free && !p.FreeFactionChoicePending)
            {
                await RespondTemporaryAsync(component, $"{p.Name} does not have a free faction choice waiting.");
                return;
            }

            if (!match.AvailableFactions(p).Any())
            {
                await RespondTemporaryAsync(component, $"{p.Name} already owns every faction.");
                return;
            }

            await component.RespondAsync(
                embed: DiscordMatchRenderer.BuildFactionChooseEmbed(match, player, free),
                components: DiscordMatchRenderer.BuildFactionSelect(match, player, free),
                ephemeral: true);
            return;
        }

        MutationResult result = action switch
        {
            "income" => match.BuyIncome(player),
            "perm" => match.BuyPermMove(player),
            "faction" => match.BuyRandomFaction(player),
            "move" => match.SingleTroopMove(player),
            "replay" => match.BuyReplay(player),
            _ => MutationResult.Fail("Unknown player action.")
        };

        if (!result.Success)
        {
            await RespondTemporaryAsync(component, result.Message);
            return;
        }

        await component.UpdateAsync(properties =>
        {
            properties.Embed = BuildPlayerEmbed(match, player);
            properties.Components = BuildPlayerComponentsForUser(match, player, component.User.Id);
        });
        TrackPlayerPanel(component, player, isActionsView: true);
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await RefreshOpenPlayerPanelsAsync(match, (component.User.Id, player));
        await FollowupTemporaryAsync(component, result.Message);
    }

    private async Task HandleEndTurnAsync(SocketMessageComponent component, TabsMatch match, int player)
    {
        var result = match.EndTurn(player);
        if (!result.Success)
        {
            await RespondTemporaryAsync(component, result.Message);
            return;
        }

        await component.DeferAsync(ephemeral: true);
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await DeleteOpenPanelsForPlayerAsync(match.ChannelId, player);
        await RefreshTurnPanelsAsync(match);
        await FollowupTemporaryAsync(component, result.Message);
    }

    private async Task HandleArmyButtonAsync(SocketMessageComponent component, TabsMatch match, string[] parts)
    {
        if (parts.Length == 3 && int.TryParse(parts[2], out int panelPlayer))
        {
            if (!CanUsePlayerControls(component.User.Id, match, panelPlayer))
            {
                await RespondTemporaryAsync(component, "You can only use your own player actions.");
                return;
            }

            if (!CanUseTurnAction(component.User.Id, match, panelPlayer, out string panelTurnError))
            {
                await RespondTemporaryAsync(component, panelTurnError);
                return;
            }

            await component.UpdateAsync(properties =>
            {
                properties.Embed = DiscordMatchRenderer.BuildArmyMenuEmbed(match, panelPlayer);
                properties.Components = DiscordMatchRenderer.BuildArmyMenuComponents(panelPlayer);
            });
            TrackPlayerPanel(component, panelPlayer, isActionsView: false);
            return;
        }

        if (parts.Length < 4 || !int.TryParse(parts[3], out int player))
        {
            await RespondTemporaryAsync(component, "Unknown army action.");
            return;
        }

        if (!CanUsePlayerControls(component.User.Id, match, player))
        {
            await RespondTemporaryAsync(component, "You can only use your own player actions.");
            return;
        }

        string action = parts[2];
        if (action != "close" && !CanUseTurnAction(component.User.Id, match, player, out string turnError))
        {
            await RespondTemporaryAsync(component, turnError);
            return;
        }

        switch (action)
        {
            case "close":
                await component.UpdateAsync(properties =>
                {
                    properties.Embed = BuildPlayerEmbed(match, player);
                    properties.Components = BuildPlayerComponentsForUser(match, player, component.User.Id);
                });
                TrackPlayerPanel(component, player, isActionsView: true);
                return;

            case "buyunits":
                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildArmyFactionEmbed(match, player);
                    properties.Components = DiscordMatchRenderer.BuildArmyFactionComponents(match, player, _factionEmojiByName);
                });
                TrackPlayerPanel(component, player, isActionsView: false);
                return;

            case "factionback":
                if (parts.Length < 5)
                {
                    await RespondTemporaryAsync(component, "Choose a faction first.");
                    return;
                }

                string faction = string.Join(':', parts.Skip(4));
                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildArmyUnitEmbed(match, player, faction);
                    properties.Components = DiscordMatchRenderer.BuildArmyUnitComponents(match, player, faction);
                });
                TrackPlayerPanel(component, player, isActionsView: false);
                return;

            case "factionpage":
                if (parts.Length < 6 || !int.TryParse(parts[4], out int page))
                {
                    await RespondTemporaryAsync(component, "Choose a faction page first.");
                    return;
                }

                string pageFaction = string.Join(':', parts.Skip(5));
                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildArmyUnitEmbed(match, player, pageFaction, page);
                    properties.Components = DiscordMatchRenderer.BuildArmyUnitComponents(match, player, pageFaction, page);
                });
                TrackPlayerPanel(component, player, isActionsView: false);
                return;

            case "sell":
                if (match.OwnedArmyUnits(player).Count == 0)
                {
                    await RespondTemporaryAsync(component, $"{match.RequirePlayer(player).Name} does not own any tracked units yet.");
                    return;
                }

                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildArmySellSelectEmbed(match, player);
                    properties.Components = DiscordMatchRenderer.BuildArmySellSelectComponents(match, player);
                });
                TrackPlayerPanel(component, player, isActionsView: false);
                return;

            case "buycustom":
            case "sellcustom":
                if (parts.Length < 5)
                {
                    await RespondTemporaryAsync(component, "Choose a unit first.");
                    return;
                }

                await OpenArmyQuantityModalAsync(component, player, parts[4], action == "buycustom");
                return;

            case "buy":
                await HandleArmyBuyConfirmAsync(component, match, player, parts);
                return;

            case "sellconfirm":
                await HandleArmySellConfirmAsync(component, match, player, parts);
                return;
        }

        await RespondTemporaryAsync(component, "Unknown army action.");
    }

    private async Task HandleArmyBuyConfirmAsync(SocketMessageComponent component, TabsMatch match, int player, string[] parts)
    {
        if (parts.Length < 6 || !int.TryParse(parts[5], out int quantity))
        {
            await RespondTemporaryAsync(component, "Choose a valid quantity.");
            return;
        }

        string unitSlug = parts[4];
        var result = match.BuyArmyUnit(player, unitSlug, quantity);
        if (!result.Success)
        {
            await RespondTemporaryAsync(component, result.Message);
            return;
        }

        await component.UpdateAsync(properties =>
        {
            properties.Embed = DiscordMatchRenderer.BuildArmyBuyEmbed(match, player, unitSlug, quantity);
            properties.Components = DiscordMatchRenderer.BuildArmyBuyComponents(player, unitSlug, quantity);
        });
        TrackPlayerPanel(component, player, isActionsView: false);
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await RefreshOpenPlayerPanelsAsync(match);
        await FollowupArmyBoughtAsync(component, match, player, unitSlug, quantity);
    }

    private async Task HandleArmySellConfirmAsync(SocketMessageComponent component, TabsMatch match, int player, string[] parts)
    {
        if (parts.Length < 6 || !int.TryParse(parts[5], out int quantity))
        {
            await RespondTemporaryAsync(component, "Choose a valid quantity.");
            return;
        }

        string unitSlug = parts[4];
        var result = match.SellArmyUnit(player, unitSlug, quantity);
        if (!result.Success)
        {
            await RespondTemporaryAsync(component, result.Message);
            return;
        }

        await component.UpdateAsync(properties =>
        {
            int remaining = match.OwnedArmyCount(player, unitSlug);
            if (remaining > 0)
            {
                properties.Embed = DiscordMatchRenderer.BuildArmySellEmbed(match, player, unitSlug, Math.Min(quantity, remaining));
                properties.Components = DiscordMatchRenderer.BuildArmySellComponents(match, player, unitSlug, Math.Min(quantity, remaining));
            }
            else if (match.OwnedArmyUnits(player).Count > 0)
            {
                properties.Embed = DiscordMatchRenderer.BuildArmySellSelectEmbed(match, player);
                properties.Components = DiscordMatchRenderer.BuildArmySellSelectComponents(match, player);
            }
            else
            {
                properties.Embed = DiscordMatchRenderer.BuildArmyMenuEmbed(match, player);
                properties.Components = DiscordMatchRenderer.BuildArmyMenuComponents(player);
            }
        });
        TrackPlayerPanel(component, player, isActionsView: false);
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await RefreshOpenPlayerPanelsAsync(match);
        await FollowupTemporaryAsync(component, result.Message, TimeSpan.FromSeconds(4));
    }

    private static async Task OpenArmyQuantityModalAsync(SocketMessageComponent component, int player, string unitSlug, bool buy)
    {
        var modal = new ModalBuilder()
            .WithTitle(buy ? "Buy Custom Quantity" : "Sell Custom Quantity")
            .WithCustomId($"tabs:modal:{(buy ? "armybuy" : "armysell")}:{player}:{unitSlug}")
            .AddTextInput("Quantity", "quantity", TextInputStyle.Short, placeholder: "2", required: true)
            .Build();
        await component.RespondWithModalAsync(modal);
    }

    private async Task HandleArmySelectAsync(SocketMessageComponent component)
    {
        var match = ResolveMatch(component);
        if (match == null)
        {
            await RespondTemporaryAsync(component, "No active match in this channel.");
            return;
        }

        if (!CanUseMatch(component.User.Id, match))
        {
            await RespondTemporaryAsync(component, "Only the host and assigned match players can use this match.");
            return;
        }

        var parts = component.Data.CustomId.Split(':');
        if (parts.Length < 4 || !int.TryParse(parts[3], out int player))
        {
            await RespondTemporaryAsync(component, "Unknown army selection.");
            return;
        }

        if (!CanUsePlayerControls(component.User.Id, match, player))
        {
            await RespondTemporaryAsync(component, "You can only use your own player actions.");
            return;
        }

        if (!CanUseTurnAction(component.User.Id, match, player, out string turnError))
        {
            await RespondTemporaryAsync(component, turnError);
            return;
        }

        string value = component.Data.Values.FirstOrDefault() ?? "";
        switch (parts[2])
        {
            case "faction":
                if (match.FactionModeEnabled && !match.RequirePlayer(player).Factions.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    await RespondTemporaryAsync(component, $"{match.RequirePlayer(player).Name} does not own {value}.");
                    return;
                }

                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildArmyUnitEmbed(match, player, value);
                    properties.Components = DiscordMatchRenderer.BuildArmyUnitComponents(match, player, value);
                });
                TrackPlayerPanel(component, player, isActionsView: false);
                return;

            case "unit":
                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildArmyBuyEmbed(match, player, value, 1);
                    properties.Components = DiscordMatchRenderer.BuildArmyBuyComponents(player, value, 1);
                });
                TrackPlayerPanel(component, player, isActionsView: false);
                return;

            case "sellunit":
                await component.UpdateAsync(properties =>
                {
                    properties.Embed = DiscordMatchRenderer.BuildArmySellEmbed(match, player, value, 1);
                    properties.Components = DiscordMatchRenderer.BuildArmySellComponents(match, player, value, 1);
                });
                TrackPlayerPanel(component, player, isActionsView: false);
                return;
        }

        await RespondTemporaryAsync(component, "Unknown army selection.");
    }

    private async Task HandleArmyModalAsync(SocketModal modal, TabsMatch match, string[] parts)
    {
        if (!CanUseMatch(modal.User.Id, match))
        {
            await RespondTemporaryAsync(modal, "Only the host and assigned match players can use this match.");
            return;
        }

        int player = int.Parse(parts[3]);
        if (!CanUsePlayerControls(modal.User.Id, match, player))
        {
            await RespondTemporaryAsync(modal, "You can only use your own player actions.");
            return;
        }

        if (!CanUseTurnAction(modal.User.Id, match, player, out string turnError))
        {
            await RespondTemporaryAsync(modal, turnError);
            return;
        }

        string unitSlug = parts[4];
        string raw = modal.Data.Components.First(c => c.CustomId == "quantity").Value;
        if (!int.TryParse(raw, out int quantity) || quantity <= 0)
        {
            await RespondTemporaryAsync(modal, "Enter a positive whole number.");
            return;
        }

        if (parts[2] == "armysell")
        {
            var result = match.SellArmyUnit(player, unitSlug, quantity);
            if (!result.Success)
            {
                await RespondTemporaryAsync(modal, result.Message);
                return;
            }

            await RespondTemporaryAsync(modal, result.Message, TimeSpan.FromSeconds(4));
            await _store.SetActiveAsync(match);
            await UpdateMatchMessageAsync(match);
            await RefreshOpenPlayerPanelsAsync(match);
            return;
        }

        var buyResult = match.BuyArmyUnit(player, unitSlug, quantity);
        if (!buyResult.Success)
        {
            await RespondTemporaryAsync(modal, buyResult.Message);
            return;
        }

        await RespondArmyBoughtAsync(modal, match, player, unitSlug, quantity);
        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await RefreshOpenPlayerPanelsAsync(match);
    }

    private async Task SelectMenuExecutedAsync(SocketMessageComponent component)
    {
        string id = component.Data.CustomId;

        if (id.StartsWith("tabs:setup:", StringComparison.Ordinal))
        {
            await HandleSetupSelectAsync(component);
            return;
        }

        if (id == "tabs:startload")
        {
            await HandleStartLoadSelectAsync(component);
            return;
        }

        if (id == "tabs:startdelete")
        {
            await HandleStartDeleteSelectAsync(component);
            return;
        }

        if (id == "tabs:matchload")
        {
            await HandleMatchLoadSelectAsync(component);
            return;
        }

        if (id == "tabs:matchdelete")
        {
            await HandleMatchDeleteSelectAsync(component);
            return;
        }

        if (id.StartsWith("tabs:army:", StringComparison.Ordinal))
        {
            await HandleArmySelectAsync(component);
            return;
        }

        if (!id.StartsWith("tabs:choose:", StringComparison.Ordinal))
            return;

        var match = ResolveMatch(component);
        if (match == null)
        {
            await RespondTemporaryAsync(component, "No active match in this channel.");
            return;
        }

        if (!CanUseMatch(component.User.Id, match))
        {
            await RespondTemporaryAsync(component, "Only the host and assigned match players can use this match.");
            return;
        }

        var parts = id.Split(':');
        int player = int.Parse(parts[2]);
        if (!CanUsePlayerControls(component.User.Id, match, player))
        {
            await RespondTemporaryAsync(component, "You can only use your own player actions.");
            return;
        }

        if (!CanUseTurnAction(component.User.Id, match, player, out string turnError))
        {
            await RespondTemporaryAsync(component, turnError);
            return;
        }

        bool free = parts[3] == "free";
        string faction = component.Data.Values.First();
        await component.UpdateAsync(properties =>
        {
            properties.Content = null;
            properties.Embed = DiscordMatchRenderer.BuildFactionChooseEmbed(match, player, free, faction);
            properties.Components = DiscordMatchRenderer.BuildFactionConfirmComponents(match, player, free, faction);
        });
    }

    private async Task HandleSetupSelectAsync(SocketMessageComponent component)
    {
        var key = (component.Channel.Id, component.User.Id);
        if (!_setupSessions.TryGetValue(key, out var setup))
        {
            await RespondTemporaryAsync(component, "No setup is active. Use `/tabs-start` first.");
            return;
        }

        string value = component.Data.Values.FirstOrDefault() ?? "";
        switch (component.Data.CustomId)
        {
            case "tabs:setup:format":
                setup.Format = value == "2v2" ? TabsMatchFormat.TwoVTwo : TabsMatchFormat.OneVOne;
                break;
            case "tabs:setup:mode":
                setup.Mode = value switch
                {
                    "ft13" => TabsMatchMode.FT13,
                    "ft10" => TabsMatchMode.FT13,
                    "ft30" => TabsMatchMode.FT30,
                    _ => TabsMatchMode.FT20
                };
                break;
            case "tabs:setup:faction":
                setup.FactionMode = value == "on";
                break;
        }

        _setupSessions[key] = setup;
        await component.UpdateAsync(properties =>
        {
            properties.Embed = DiscordMatchRenderer.BuildSetupEmbed(setup);
            properties.Components = DiscordMatchRenderer.BuildSetupComponents(setup);
        });
    }

    private async Task HandleStartLoadSelectAsync(SocketMessageComponent component)
    {
        if (!UserHasHostRole(component.User))
        {
            await RespondTemporaryAsync(component, "Only players with the `Host` role can manage hosted saves.");
            return;
        }

        string name = component.Data.Values.FirstOrDefault() ?? "";
        var match = await _store.LoadNamedAsync(component.User.Id, component.Channel.Id, name);
        if (match == null)
        {
            await RespondTemporaryAsync(component, $"No save named `{name}` was found under your host saves.");
            return;
        }

        match.HostUserId = component.User.Id;
        match.HostDisplayName = DisplayName(component.User);
        match.LoadedSaveName = MatchStore.NormalizeSaveName(name);
        match.IsPrivateMatchChannel = false;
        match.MatchmakingChannelId = 0;
        match.PrivateVoiceChannelId = 0;
        match.ChannelDeleteAfterUtc = null;
        var existing = _store.GetActive(component.Channel.Id);
        if (existing != null)
        {
            await DeleteMatchMessageAsync(existing);
            await DeleteOpenPanelsForChannelAsync(component.Channel.Id);
            await DeleteTurnPanelsForChannelAsync(component.Channel.Id);
            await DeleteTieTimerAnnouncementAsync(component.Channel.Id);
            await DeleteMilestoneWarningsAsync(component.Channel.Id);
        }

        var message = await component.Channel.SendMessageAsync(
            embed: BuildMatchEmbed(match),
            components: DiscordMatchRenderer.BuildMainComponents(match));
        match.MessageId = message.Id;
        await _store.SetActiveAsync(match);
        await RefreshTurnPanelsAsync(match);
        await RefreshMilestoneWarningsAsync(match);
        _setupSessions.Remove((component.Channel.Id, component.User.Id));

        await component.UpdateAsync(properties =>
        {
            properties.Content = $"Loaded hosted save `{name}` into this channel.";
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        _ = DeleteOriginalResponseAfterDelayAsync(component);
    }

    private async Task HandleStartDeleteSelectAsync(SocketMessageComponent component)
    {
        if (!UserHasHostRole(component.User))
        {
            await RespondTemporaryAsync(component, "Only players with the `Host` role can manage hosted saves.");
            return;
        }

        string name = component.Data.Values.FirstOrDefault() ?? "";
        _pendingSaveDeletes[(component.Channel.Id, component.User.Id)] = MatchStore.NormalizeSaveName(name);
        await component.UpdateAsync(properties =>
        {
            properties.Content = null;
            properties.Embed = DiscordMatchRenderer.BuildConfirmEmbed("Delete Save?", $"Are you sure you want to delete `{name}`?");
            properties.Components = DiscordMatchRenderer.BuildConfirmComponents("tabs:confirm:delete", "Delete Save");
        });
    }

    private async Task ModalSubmittedAsync(SocketModal modal)
    {
        string id = modal.Data.CustomId;
        if (!id.StartsWith("tabs:modal:", StringComparison.Ordinal))
            return;

        var match = ResolveMatch(modal);
        if (match == null)
        {
            await RespondTemporaryAsync(modal, "No active match in this channel.");
            return;
        }

        var parts = id.Split(':');
        if (parts.Length >= 3 && parts[2] == "save")
        {
            if (!IsHost(modal.User.Id, match))
            {
                await RespondTemporaryAsync(modal, "Only the host can save this match.");
                return;
            }

            string name = modal.Data.Components.First(c => c.CustomId == "name").Value.Trim();
            if (name.Length == 0)
            {
                await RespondTemporaryAsync(modal, "Save name cannot be blank.");
                return;
            }

            string saveName = MatchStore.NormalizeSaveName(name);
            match.LoadedSaveName = saveName;
            await _store.SaveNamedAsync(match, saveName);
            await _store.SetActiveAsync(match);
            await RespondTemporaryAsync(modal, $"Saved active match as `{saveName}`.");
            return;
        }

        if (parts.Length >= 5 && parts[2] is "armybuy" or "armysell")
        {
            await HandleArmyModalAsync(modal, match, parts);
            return;
        }

        if (!CanUseMatch(modal.User.Id, match))
        {
            await RespondTemporaryAsync(modal, "Only the host and assigned match players can use this match.");
            return;
        }

        string action = parts[2];
        int player = int.Parse(parts[3]);
        if (!CanUsePlayerControls(modal.User.Id, match, player))
        {
            await RespondTemporaryAsync(modal, "You can only use your own player actions.");
            return;
        }

        if (!CanUseTurnAction(modal.User.Id, match, player, out string turnError))
        {
            await RespondTemporaryAsync(modal, turnError);
            return;
        }

        string raw = modal.Data.Components.First(c => c.CustomId == "amount").Value;
        if (!int.TryParse(raw, out int amount) || amount <= 0)
        {
            await RespondTemporaryAsync(modal, "Enter a positive whole number.");
            return;
        }

        MutationResult result = action switch
        {
            "spend" => match.CustomSpend(player, amount),
            "sell" => match.SellUnit(player, amount),
            "bft" => match.BuyForTeammate(player, amount),
            _ => MutationResult.Fail("Unknown modal action.")
        };

        await _store.SetActiveAsync(match);
        await UpdateMatchMessageAsync(match);
        await RefreshOpenPlayerPanelsAsync(match);
        await RespondTemporaryAsync(modal, result.Message);
    }

    private static MutationResult NewGame(TabsMatch match)
    {
        match.ResetForCurrentSettings();
        match.LoadedSaveName = "";
        match.ChannelDeleteAfterUtc = null;
        match.Log("New game started with current mode settings.");
        return MutationResult.Ok("New game started with current mode settings.");
    }

    private static MutationResult HandleTimer(TabsMatch match, string action)
    {
        if (action == "restart")
            return match.RestartTimer();

        match.RefreshTimerClock();
        return match.TimerRunning ? match.StopTimer() : match.StartTimer();
    }

    private static MutationResult HandleMode(TabsMatch match, string action)
    {
        return action switch
        {
            "ft13" => match.SetMode(TabsMatchMode.FT13),
            "ft10" => match.SetMode(TabsMatchMode.FT13),
            "ft20" => match.SetMode(TabsMatchMode.FT20),
            "ft30" => match.SetMode(TabsMatchMode.FT30),
            "faction" => match.SetFactionMode(!match.FactionModeEnabled),
            _ => MutationResult.Fail("Unknown mode action.")
        };
    }

    private Embed BuildMatchEmbed(TabsMatch match)
    {
        return DiscordMatchRenderer.BuildMatchEmbed(match, _factionEmojiByName);
    }

    private Embed BuildPlayerEmbed(TabsMatch match, int player)
    {
        return DiscordMatchRenderer.BuildPlayerEmbed(match, player, _factionEmojiByName);
    }

    private MessageComponent BuildPlayerComponentsForUser(TabsMatch match, int player, ulong userId)
    {
        bool isHost = IsHost(userId, match);
        bool controlsDisabled = !isHost && !match.IsPlayerTurnActive(player);
        bool endTurnDisabled = !isHost && !match.IsPlayerTurnActive(player);
        bool replayDisabled = match.ArmyTransactionThisRound || !CanUseReplayAction(match, out _);
        return DiscordMatchRenderer.BuildPlayerComponents(match, player, controlsDisabled, endTurnDisabled, replayDisabled);
    }

    private static bool IsMatchmakingChannel(IChannel channel)
    {
        return channel is SocketTextChannel textChannel &&
               textChannel.Name.Contains(MatchmakingChannelName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActualPrivateMatchSetupChannel(MatchSetupSession setup)
    {
        return setup.UsesPrivateMatchChannel && setup.ChannelId != 0 && setup.ChannelId != setup.MatchmakingChannelId;
    }

    private static bool IsActualPrivateMatchSetupChannel(PendingInvite invite)
    {
        return invite.UsesPrivateMatchChannel && invite.ChannelId != 0 && invite.ChannelId != invite.MatchmakingChannelId;
    }

    private static OverwritePermissions PrivateMatchAllowPermissions()
    {
        return OverwritePermissions.InheritAll.Modify(
            viewChannel: PermValue.Allow,
            sendMessages: PermValue.Allow,
            embedLinks: PermValue.Allow,
            attachFiles: PermValue.Allow,
            readMessageHistory: PermValue.Allow,
            useApplicationCommands: PermValue.Allow);
    }

    private static OverwritePermissions PrivateMatchVoiceAllowPermissions()
    {
        return OverwritePermissions.InheritAll.Modify(
            viewChannel: PermValue.Allow,
            connect: PermValue.Allow,
            speak: PermValue.Allow);
    }

    private static OverwritePermissions PrivateMatchDenyPermissions()
    {
        return OverwritePermissions.InheritAll.Modify(viewChannel: PermValue.Deny);
    }

    private static async Task<(ITextChannel TextChannel, IVoiceChannel? VoiceChannel, string VoiceError)> CreatePrivateMatchChannelsAsync(SocketTextChannel lobbyChannel, SocketUser host, PendingInvite invite)
    {
        var guild = lobbyChannel.Guild;
        var textAllow = PrivateMatchAllowPermissions();
        var voiceAllow = PrivateMatchVoiceAllowPermissions();
        var textOverwrites = new List<Overwrite>
        {
            new(guild.EveryoneRole.Id, PermissionTarget.Role, PrivateMatchDenyPermissions()),
            new(guild.CurrentUser.Id, PermissionTarget.User, textAllow)
        };
        var voiceOverwrites = new List<Overwrite>
        {
            new(guild.EveryoneRole.Id, PermissionTarget.Role, PrivateMatchDenyPermissions()),
            new(guild.CurrentUser.Id, PermissionTarget.User, voiceAllow)
        };

        foreach (ulong userId in new[] { host.Id }.Concat(invite.RequiredInviteUserIds).Where(id => id != 0).Distinct())
        {
            textOverwrites.Add(new Overwrite(userId, PermissionTarget.User, textAllow));
            voiceOverwrites.Add(new Overwrite(userId, PermissionTarget.User, voiceAllow));
        }

        string channelName = BuildPrivateMatchChannelName(invite);
        var textChannel = await guild.CreateTextChannelAsync(channelName, properties =>
        {
            properties.CategoryId = lobbyChannel.CategoryId;
            properties.Topic = $"Private TABS Arena {invite.Format.ToString()} {invite.Mode.ToString().ToUpperInvariant()} match hosted by {host.Username}.";
            properties.PermissionOverwrites = textOverwrites;
        });

        try
        {
            var voiceChannel = await guild.CreateVoiceChannelAsync($"{channelName}-vc", properties =>
            {
                properties.CategoryId = lobbyChannel.CategoryId;
                properties.PermissionOverwrites = voiceOverwrites;
            });

            return (textChannel, voiceChannel, "");
        }
        catch (Exception ex)
        {
            return (textChannel, null, ex.Message);
        }
    }

    private static string? BuildPrivateVoiceChannelNotice(TabsMatch match)
    {
        return match.PrivateVoiceChannelId == 0
            ? null
            : $"Private voice channel: {MentionUtils.MentionChannel(match.PrivateVoiceChannelId)}";
    }

    private static string? BuildMatchMessageContent(TabsMatch match)
    {
        if (!match.MatchEndAnnounced)
            return BuildPrivateVoiceChannelNotice(match);

        string mentions = string.Join(" ", match.Players
            .Select(player => player.DiscordUserId)
            .Where(id => id != 0)
            .Distinct()
            .Select(MentionUtils.MentionUser));

        int winner = match.WinningTeam;
        string winnerText = winner == 0 ? "Game over." : $"{match.TeamName(winner)} wins!";
        return string.IsNullOrWhiteSpace(mentions)
            ? winnerText
            : $"{mentions}\n{winnerText}";
    }

    private static async Task SendPrivateMatchAttentionPingAsync(IMessageChannel channel, TabsMatch match)
    {
        string mentions = string.Join(" ", match.Players
            .Select(player => player.DiscordUserId)
            .Where(id => id != 0)
            .Distinct()
            .Select(MentionUtils.MentionUser));

        string voiceText = match.PrivateVoiceChannelId == 0
            ? "Your private match room is ready."
            : $"Your private match room is ready. Join {MentionUtils.MentionChannel(match.PrivateVoiceChannelId)}.";

        var ping = await channel.SendMessageAsync($"{mentions}\n{voiceText}");
        _ = DeleteMessageAfterDelayAsync(ping, TimeSpan.FromSeconds(7));
    }

    private static async Task SendMatchmakingChannelRedirectAsync(IMessageChannel matchmakingChannel, TabsMatch match)
    {
        string mentions = string.Join(" ", match.Players
            .Select(player => player.DiscordUserId)
            .Where(id => id != 0)
            .Distinct()
            .Select(MentionUtils.MentionUser));
        string channelMention = MentionUtils.MentionChannel(match.ChannelId);
        var redirect = await matchmakingChannel.SendMessageAsync(
            $"{mentions}\nYour **{match.FormatLabel} {match.ModeLabel}** match is ready: {channelMention}");
        _ = DeleteMessageAfterDelayAsync(redirect, TimeSpan.FromSeconds(20));
    }

    private static string BuildPrivateMatchChannelName(PendingInvite invite)
    {
        string formatAndMode = $"{(invite.Format == TabsMatchFormat.OneVOne ? "1v1" : "2v2")}{invite.Mode.ToString().ToLowerInvariant()}";
        string matchup;
        if (invite.Format == TabsMatchFormat.OneVOne)
        {
            matchup = $"{SanitizeChannelNamePart(invite.HostDisplayName)}-vs-{SanitizeChannelNamePart(invite.Player2DisplayName)}";
        }
        else
        {
            invite.EnsureTeamAssignments();
            string red = string.Join("-and-", invite.AllParticipantUserIds
                .Where(userId => invite.TeamOf(userId) == 1)
                .Select(userId => SanitizeChannelNamePart(invite.DisplayNameFor(userId))));
            string blue = string.Join("-and-", invite.AllParticipantUserIds
                .Where(userId => invite.TeamOf(userId) == 2)
                .Select(userId => SanitizeChannelNamePart(invite.DisplayNameFor(userId))));
            matchup = $"{red}-vs-{blue}";
        }

        string channelName = $"{formatAndMode}-{matchup}";
        if (channelName.Length > 100)
            channelName = channelName[..100].Trim('-');
        return channelName;
    }

    private static string SanitizeChannelNamePart(string displayName)
    {
        string safe = string.Concat((displayName ?? "").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-'));
        while (safe.Contains("--", StringComparison.Ordinal))
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        safe = safe.Trim('-');
        if (safe.Length == 0)
            safe = "player";
        if (safe.Length > 28)
            safe = safe[..28].Trim('-');
        return safe;
    }

    private async Task DeleteMatchmakingMatchHistoryAsync(IMessageChannel channel, PendingInvite invite)
    {
        var authorIds = new HashSet<ulong>(invite.RequiredInviteUserIds) { invite.HostUserId };
        if (_client.CurrentUser != null)
            authorIds.Add(_client.CurrentUser.Id);

        await DeleteMatchmakingMessagesByAuthorsAsync(channel, authorIds);
    }

    private async Task DeleteMatchmakingSetupHistoryAsync(IMessageChannel channel, MatchSetupSession setup)
    {
        var authorIds = new HashSet<ulong> { setup.HostUserId };
        foreach (ulong playerId in new[] { setup.Player2UserId, setup.Player3UserId, setup.Player4UserId }.Where(id => id != 0))
            authorIds.Add(playerId);

        if (_client.CurrentUser != null)
            authorIds.Add(_client.CurrentUser.Id);

        await DeleteMatchmakingMessagesByAuthorsAsync(channel, authorIds);
    }

    private async Task CleanupStaleMatchmakingMentionsAsync()
    {
        foreach (var channel in _client.Guilds.SelectMany(guild => guild.TextChannels).Where(IsMatchmakingChannel))
        {
            try
            {
                if (!CanDeleteMessagesIn(channel))
                {
                    LogCleanupPermissionWarning(channel, "Missing Manage Messages permission; matchmaking cleanup is disabled for this channel.");
                    continue;
                }

                var messages = await channel.GetMessagesAsync(50).FlattenAsync();
                foreach (var message in messages.Where(IsStaleMatchmakingCleanupMessage))
                {
                    await DeleteMessageWithLogAsync(message, $"stale matchmaking cleanup message in #{channel.Name}");
                    await Task.Delay(150);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not clean stale matchmaking mentions in #{channel.Name}: {ex.Message}");
            }
        }
    }

    private async Task CleanupLegacyPublicTurnPanelsAsync()
    {
        foreach (var match in _store.ActiveMatches)
        {
            if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
                continue;

            try
            {
                var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                foreach (var message in messages.Where(message =>
                             message.Author.Id == _client.CurrentUser.Id &&
                             (message.Content ?? "").Contains("it is your turn", StringComparison.OrdinalIgnoreCase)))
                {
                    await DeleteMessageWithLogAsync(message, "legacy public turn panel");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not clean legacy public turn panels in channel {match.ChannelId}: {ex.Message}");
            }
        }
    }

    private async Task CleanupStaleMilestoneWarningsAsync()
    {
        foreach (var match in _store.ActiveMatches)
        {
            if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
                continue;

            try
            {
                var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                foreach (var message in messages.Where(message =>
                             message.Author.Id == _client.CurrentUser.Id &&
                             (message.Content ?? "").Contains("1 point away from the", StringComparison.OrdinalIgnoreCase) &&
                             (message.Content ?? "").Contains("milestone", StringComparison.OrdinalIgnoreCase)))
                {
                    await message.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not clean stale milestone warnings in channel {match.ChannelId}: {ex.Message}");
            }
        }
    }

    private static bool CanDeleteMessagesIn(SocketTextChannel channel)
    {
        var currentUser = channel.Guild.CurrentUser;
        if (currentUser == null)
            return false;

        var permissions = currentUser.GetPermissions(channel);
        return permissions.ManageMessages && permissions.ViewChannel && permissions.ReadMessageHistory;
    }

    private void LogCleanupPermissionWarning(SocketTextChannel channel, string message)
    {
        var now = DateTimeOffset.UtcNow;
        if (_nextCleanupPermissionWarningByChannel.TryGetValue(channel.Id, out var nextAllowed) && now < nextAllowed)
            return;

        _nextCleanupPermissionWarningByChannel[channel.Id] = now.AddMinutes(10);
        Console.WriteLine($"Matchmaking cleanup skipped in #{channel.Name}: {message}");
    }

    private static bool IsMentionOnlyMessage(IMessage message)
    {
        if (message.Author.IsBot)
            return false;

        string content = (message.Content ?? "").Trim();
        if (content.Length == 0)
            return false;

        bool rawMentionOnly = System.Text.RegularExpressions.Regex.IsMatch(content, @"^(<@!?\d+>\s*)+$");
        bool shortMentionMessage = content.Length <= 120 &&
                                   message.MentionedUserIds.Any() &&
                                   !message.Attachments.Any() &&
                                   !message.Embeds.Any();

        return rawMentionOnly || shortMentionMessage;
    }

    private bool IsStaleMatchmakingCleanupMessage(IMessage message)
    {
        if (IsMentionOnlyMessage(message))
            return true;

        if (_client.CurrentUser == null || message.Author.Id != _client.CurrentUser.Id)
            return false;

        string content = (message.Content ?? "").Trim();
        return content.Contains("Invite sent.", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("accepted", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("declined", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("cancelled the TABS Arena invite", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("Match moved to", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DeleteMatchmakingMessagesByAuthorsAsync(IMessageChannel channel, HashSet<ulong> authorIds)
    {
        if (channel is SocketTextChannel textChannel && !CanDeleteMessagesIn(textChannel))
        {
            LogCleanupPermissionWarning(textChannel, "Missing Manage Messages permission; could not clean setup/invite messages.");
            return;
        }

        ulong beforeId = 0;
        for (int page = 0; page < 100; page++)
        {
            var messages = beforeId == 0
                ? (await channel.GetMessagesAsync(100).FlattenAsync()).ToList()
                : (await channel.GetMessagesAsync(beforeId, Direction.Before, 100).FlattenAsync()).ToList();

            if (messages.Count == 0)
                break;

            beforeId = messages.Min(message => message.Id);
            foreach (var message in messages.Where(message => authorIds.Contains(message.Author.Id)))
            {
                try
                {
                    await DeleteMessageWithLogAsync(message, "matchmaking cleanup message");
                    await Task.Delay(150);
                }
                catch
                {
                    // Best-effort lobby cleanup; Discord permissions/history age can block individual deletes.
                }
            }
        }
    }

    private static void ScheduleGameDeletionIfMatchFinished(TabsMatch match)
    {
        if (!match.MatchEndAnnounced || match.ChannelDeleteAfterUtc != null)
            return;

        match.ChannelDeleteAfterUtc = DateTimeOffset.UtcNow.Add(FinishedMatchDeleteDelay);
        match.Log("Game over. This game will be deleted in 3 minutes.");
    }

    private async Task DeletePrivateMatchChannelsAfterDelayAsync(ulong textChannelId, ulong voiceChannelId, TimeSpan delay)
    {
        await Task.Delay(delay);
        await DeletePrivateMatchChannelsAsync(textChannelId, voiceChannelId);
    }

    private async Task DeleteChannelAfterDelayAsync(ulong channelId, TimeSpan delay)
    {
        await DeletePrivateMatchChannelsAfterDelayAsync(channelId, 0, delay);
    }

    private async Task DeletePrivateMatchChannelsAsync(ulong textChannelId, ulong voiceChannelId)
    {
        if (voiceChannelId != 0)
            await DeleteDiscordChannelAsync(voiceChannelId);

        await DeleteDiscordChannelAsync(textChannelId);
    }

    private async Task DeleteDiscordChannelAsync(ulong channelId)
    {
        try
        {
            if (_client.GetChannel(channelId) is SocketGuildChannel channel)
                await channel.DeleteAsync();
        }
        catch
        {
            // If Discord refuses the deletion, leave the stored state alone where the caller decides.
        }
    }

    private void TrackPlayerPanel(SocketMessageComponent component, int playerId, bool isActionsView)
    {
        _openPlayerPanels[(component.Channel.Id, component.User.Id, playerId)] = new OpenPlayerPanel
        {
            Interaction = component,
            IsActionsView = isActionsView,
            LastTouchedUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task DeleteOpenPanelsForPlayerAsync(ulong channelId, int playerId)
    {
        foreach (var entry in _openPlayerPanels
                     .Where(entry => entry.Key.ChannelId == channelId && entry.Key.PlayerId == playerId)
                     .ToList())
        {
            try
            {
                await entry.Value.Interaction.DeleteOriginalResponseAsync();
            }
            catch
            {
                // The panel may already be gone or expired.
            }

            _openPlayerPanels.Remove(entry.Key);
        }
    }

    private async Task RefreshTurnPanelsAsync(TabsMatch match)
    {
        var activePlayers = match.FirstTurnChosen && !match.TurnsComplete && match.ActiveTurnTeam != 0
            ? match.TeamPlayerIds(match.ActiveTurnTeam)
                .Where(playerId => !match.EndedTurnPlayers.Contains(playerId))
                .ToHashSet()
            : new HashSet<int>();

        foreach (var entry in _turnPanelMessages
                     .Where(entry => entry.Key.ChannelId == match.ChannelId && !activePlayers.Contains(entry.Key.PlayerId))
                     .ToList())
        {
            try
            {
                await entry.Value.DeleteAsync();
            }
            catch
            {
                // Missing or already deleted turn panels only need their tracker removed.
            }

            _turnPanelMessages.Remove(entry.Key);
        }

        foreach (int playerId in activePlayers)
        {
            var player = match.RequirePlayer(playerId);
            if (player.DiscordUserId == 0)
                continue;

            var key = (match.ChannelId, playerId);
            string content = $"<@{player.DiscordUserId}>, it is your turn. Open your controls below, then press **End Turn** when finished.";
            var components = new ComponentBuilder()
                .WithButton("Open Turn Controls", $"tabs:turnopen:{playerId}", ButtonStyle.Primary)
                .Build();

            if (_turnPanelMessages.TryGetValue(key, out IUserMessage? existingPanel))
            {
                try
                {
                    await existingPanel.ModifyAsync(properties =>
                    {
                        properties.Content = content;
                        properties.Embed = null;
                        properties.Components = components;
                    });
                    continue;
                }
                catch
                {
                    // Recreate the panel below.
                }
            }

            try
            {
                if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
                    continue;

                var newPanel = await channel.SendMessageAsync(
                    text: content,
                    components: components);
                _turnPanelMessages[key] = newPanel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not send turn prompt for {player.Name}: {ex.Message}");
            }
        }
    }

    private async Task DeleteTurnPanelsForChannelAsync(ulong channelId)
    {
        foreach (var entry in _turnPanelMessages.Where(entry => entry.Key.ChannelId == channelId).ToList())
        {
            try
            {
                await entry.Value.DeleteAsync();
            }
            catch
            {
                // Best-effort cleanup.
            }

            _turnPanelMessages.Remove(entry.Key);
        }
    }

    private async Task AnnounceTieTimerStartedAsync(TabsMatch match)
    {
        await DeleteTieTimerAnnouncementAsync(match.ChannelId);
        if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
            return;

        string mentions = string.Join(
            " ",
            match.Players
                .Select(player => player.DiscordUserId)
                .Where(userId => userId != 0)
                .Distinct()
                .Select(userId => $"<@{userId}>"));
        string text = string.IsNullOrWhiteSpace(mentions)
            ? "⏱️ **The 2:00 tie timer has started.**"
            : $"{mentions}\n⏱️ **The 2:00 tie timer has started.**";

        var announcement = await channel.SendMessageAsync(text);
        _tieTimerAnnouncements[match.ChannelId] = announcement;
        _ = DeleteTieTimerAnnouncementAfterDelayAsync(match.ChannelId, announcement.Id);
    }

    private async Task DeleteTieTimerAnnouncementAfterDelayAsync(ulong channelId, ulong messageId)
    {
        await Task.Delay(TimeSpan.FromMinutes(1));
        if (!_tieTimerAnnouncements.TryGetValue(channelId, out IUserMessage? current) ||
            current.Id != messageId)
            return;

        await DeleteTieTimerAnnouncementAsync(channelId);
    }

    private async Task DeleteTieTimerAnnouncementAsync(ulong channelId)
    {
        if (!_tieTimerAnnouncements.Remove(channelId, out IUserMessage? announcement))
            return;

        try
        {
            await announcement.DeleteAsync();
        }
        catch
        {
            // The announcement may already have been removed with the match channel.
        }
    }

    private async Task RefreshMilestoneWarningsAsync(TabsMatch match)
    {
        if (match.MatchEndAnnounced || match.RewardQueue.Count == 0)
        {
            await DeleteMilestoneWarningsAsync(match.ChannelId);
            return;
        }

        string reward = match.NextRewardText;
        var desired = Enumerable.Range(1, 2)
            .Where(team => match.NextMilestoneForTeam(team) - match.TeamPoints(team) == 1)
            .ToDictionary(team => team, team => match.NextMilestoneForTeam(team));

        foreach (var entry in _milestoneWarnings
                     .Where(entry => entry.Key.ChannelId == match.ChannelId)
                     .ToList())
        {
            bool keep = desired.TryGetValue(entry.Key.Team, out int threshold) &&
                        entry.Value.Threshold == threshold &&
                        string.Equals(entry.Value.Reward, reward, StringComparison.Ordinal);
            if (keep)
                continue;

            await DeleteMilestoneWarningAsync(entry.Key);
        }

        if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
            return;

        string mentions = string.Join(
            " ",
            match.Players
                .Select(player => player.DiscordUserId)
                .Where(userId => userId != 0)
                .Distinct()
                .Select(userId => $"<@{userId}>"));

        foreach (var desiredWarning in desired)
        {
            var key = (match.ChannelId, desiredWarning.Key);
            if (_milestoneWarnings.ContainsKey(key))
                continue;

            string teamName = match.TeamName(desiredWarning.Key);
            string text = $"{mentions}\n**{teamName} Team is 1 point away from the {reward} milestone.**";
            var message = await channel.SendMessageAsync(text);
            _milestoneWarnings[key] = new MilestoneWarning
            {
                Message = message,
                Threshold = desiredWarning.Value,
                Reward = reward
            };
        }
    }

    private async Task DeleteMilestoneWarningsAsync(ulong channelId)
    {
        foreach (var key in _milestoneWarnings.Keys
                     .Where(key => key.ChannelId == channelId)
                     .ToList())
        {
            await DeleteMilestoneWarningAsync(key);
        }
    }

    private async Task DeleteMilestoneWarningAsync((ulong ChannelId, int Team) key)
    {
        if (!_milestoneWarnings.Remove(key, out MilestoneWarning? warning))
            return;

        try
        {
            await warning.Message.DeleteAsync();
        }
        catch
        {
            // The warning may already have been deleted with the match channel.
        }
    }

    private async Task DeleteOpenPanelsForChannelAsync(ulong channelId, SocketInteraction? skipInteraction = null)
    {
        foreach (var entry in _openPlayerPanels.Where(entry => entry.Key.ChannelId == channelId).ToList())
        {
            if (skipInteraction != null && entry.Value.Interaction.Id == skipInteraction.Id)
                continue;

            try
            {
                await entry.Value.Interaction.DeleteOriginalResponseAsync();
            }
            catch
            {
                // The panel may already be gone or expired; removing the tracker is enough.
            }

            var key = entry.Key;
            _openPlayerPanels.Remove(key);
        }
    }

    private async Task RefreshOpenPlayerPanelsAsync(TabsMatch match, (ulong UserId, int PlayerId)? skip = null)
    {
        if (_openPlayerPanels.Count == 0)
            return;

        var stale = new List<(ulong ChannelId, ulong UserId, int PlayerId)>();
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in _openPlayerPanels.ToArray())
        {
            var key = entry.Key;
            var panel = entry.Value;
            if (key.ChannelId != match.ChannelId || !panel.IsActionsView)
                continue;

            if (skip is { } skipKey && key.UserId == skipKey.UserId && key.PlayerId == skipKey.PlayerId)
                continue;

            if (now - panel.LastTouchedUtc > TimeSpan.FromMinutes(14) || match.Players.All(player => player.Id != key.PlayerId))
            {
                stale.Add(key);
                continue;
            }

            try
            {
                await panel.Interaction.ModifyOriginalResponseAsync(properties =>
                {
                    properties.Embed = BuildPlayerEmbed(match, key.PlayerId);
                    properties.Components = BuildPlayerComponentsForUser(match, key.PlayerId, key.UserId);
                });
            }
            catch
            {
                stale.Add(key);
            }
        }

        foreach (var key in stale)
            _openPlayerPanels.Remove(key);
    }

    private async Task UpdateMatchMessageAsync(TabsMatch match)
    {
        if (match.MessageId == 0)
            return;

        if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
            return;

        if (await channel.GetMessageAsync(match.MessageId) is not IUserMessage message)
            return;

        await message.ModifyAsync(properties =>
        {
            properties.Content = BuildMatchMessageContent(match);
            properties.Embed = BuildMatchEmbed(match);
            properties.Components = DiscordMatchRenderer.BuildMainComponents(match);
        });
    }

    private async Task MaybeMoveMatchOverviewAfterChatAsync(ulong channelId)
    {
        var match = _store.GetActive(channelId);
        if (match == null || match.MessageId == 0 || match.MatchEndAnnounced)
            return;

        var now = DateTimeOffset.UtcNow;
        if (_nextOverviewMoveByChannel.TryGetValue(channelId, out var nextAllowed) && now < nextAllowed)
            return;

        _nextOverviewMoveByChannel[channelId] = now.AddSeconds(20);
        await MoveMatchOverviewToBottomAsync(match);
    }

    private async Task MoveMatchOverviewToBottomAsync(TabsMatch match)
    {
        if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
            return;

        IUserMessage? oldMessage = null;
        if (match.MessageId != 0)
            oldMessage = await channel.GetMessageAsync(match.MessageId) as IUserMessage;

        var newMessage = await channel.SendMessageAsync(
            text: BuildMatchMessageContent(match),
            embed: BuildMatchEmbed(match),
            components: DiscordMatchRenderer.BuildMainComponents(match));

        match.MessageId = newMessage.Id;
        await _store.SetActiveAsync(match);

        if (oldMessage != null)
        {
            try
            {
                await oldMessage.DeleteAsync();
            }
            catch
            {
                // The old overview may already be gone; the new one is now authoritative.
            }
        }
    }

    private async Task DeleteMatchMessageAsync(TabsMatch match)
    {
        if (match.MessageId == 0)
            return;

        if (_client.GetChannel(match.ChannelId) is not IMessageChannel channel)
            return;

        if (await channel.GetMessageAsync(match.MessageId) is not IUserMessage message)
            return;

        await message.DeleteAsync();
    }

    private async Task TimerLoopAsync()
    {
        while (true)
        {
            await Task.Delay(1000);
            if (DateTimeOffset.UtcNow >= _nextMatchmakingMentionCleanupUtc)
            {
                _nextMatchmakingMentionCleanupUtc = DateTimeOffset.UtcNow.AddMinutes(1);
                await CleanupStaleMatchmakingMentionsAsync();
            }

            foreach (var match in _store.ActiveMatches)
            {
                if (match.ChannelDeleteAfterUtc is { } deleteAt && deleteAt <= DateTimeOffset.UtcNow)
                {
                    await DeleteOpenPanelsForChannelAsync(match.ChannelId);
                    await DeleteTurnPanelsForChannelAsync(match.ChannelId);
                    await DeleteTieTimerAnnouncementAsync(match.ChannelId);
                    await DeleteMilestoneWarningsAsync(match.ChannelId);
                    await _store.ClearActiveAsync(match.ChannelId);
                    if (match.IsPrivateMatchChannel)
                        await DeletePrivateMatchChannelsAsync(match.ChannelId, match.PrivateVoiceChannelId);
                    else
                        await DeleteMatchMessageAsync(match);
                    continue;
                }

                bool tieWasRunning = match.TimerRunning;
                bool matchWasRunning = match.MatchTimerRunning;
                match.RefreshTimerClock();
                match.RefreshMatchTimerClock();

                bool changed = false;
                bool tieExpired = false;
                if (matchWasRunning && match.GetMatchTimerRemainingSeconds() == 0 && !match.MatchTimerExpiredNotified)
                {
                    match.MatchTimerExpiredNotified = true;
                    match.Log("Match timer finished. Tie timer is now available.");
                    changed = true;
                }

                if (tieWasRunning && match.GetTimerRemainingSeconds() == 0 && !match.TimerExpiredNotified)
                {
                    match.TimerExpiredNotified = true;
                    match.Log("Tie timer finished. Force a tie if nobody won.");
                    changed = true;
                    tieExpired = true;
                }

                if (!changed)
                    continue;

                await _store.SetActiveAsync(match);
                await UpdateMatchMessageAsync(match);
                await RefreshOpenPlayerPanelsAsync(match);

                if (tieExpired && _client.GetChannel(match.ChannelId) is IMessageChannel channel)
                    await channel.SendMessageAsync($"⏰ Tie timer finished for TABS Arena match `{match.Id}`.");
            }
        }
    }

    private static bool IsHost(ulong userId, TabsMatch match)
    {
        return match.HostUserId == 0 || match.HostUserId == userId;
    }

    private TabsMatch? ResolveMatch(SocketMessageComponent component)
    {
        return _store.GetActive(component.Channel.Id);
    }

    private TabsMatch? ResolveMatch(SocketModal modal)
    {
        return _store.GetActive(modal.Channel.Id);
    }

    private static bool CanUseMatch(ulong userId, TabsMatch match)
    {
        return match.HostUserId == 0 ||
               match.HostUserId == userId ||
               (match.InviteAccepted && match.InvitedUserId == userId) ||
               match.Players.Any(player => player.DiscordUserId == userId);
    }

    private static bool CanUsePlayerControls(ulong userId, TabsMatch match, int player)
    {
        if (IsHost(userId, match))
            return true;

        return match.InviteAccepted && match.GetPlayer(player)?.DiscordUserId == userId;
    }

    private static bool CanUseTurnAction(ulong userId, TabsMatch match, int player, out string error)
    {
        error = "";
        if (match.NeedsFirstTurnChoice)
        {
            error = "The host must choose who goes first before player actions are available.";
            return false;
        }

        if (IsHost(userId, match))
            return true;

        if (match.IsPlayerTurnActive(player))
            return true;

        if (match.TurnsComplete)
            error = "Both sides have ended turns. Wait for the next round.";
        else if (match.ActiveTurnTeam == 0)
            error = "No team is currently taking actions.";
        else
            error = $"It is {match.TeamName(match.ActiveTurnTeam)}'s turn right now.";
        return false;
    }

    private static bool CanUseReplayAction(TabsMatch match, out string error)
    {
        error = "";
        if (match.ArmyTransactionThisRound)
        {
            error = "Replay is disabled until next round because a unit was bought or sold.";
            return false;
        }

        if (match.NeedsFirstTurnChoice)
        {
            error = "The host must choose who goes first before replay can be bought.";
            return false;
        }

        if (match.TurnsComplete)
        {
            error = "Both sides have ended turns. Wait for the next round.";
            return false;
        }

        return true;
    }

    private static bool UserHasHostRole(SocketUser user)
    {
        return user is SocketGuildUser guildUser &&
               guildUser.Roles.Any(role => string.Equals(role.Name, "Host", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveSetupPlayers(
        SocketUser requester,
        IEnumerable<IUser> mentionedUsers,
        TabsMatchFormat format,
        out List<(ulong UserId, string DisplayName)> players,
        out string error)
    {
        players = new List<(ulong UserId, string DisplayName)>();
        error = "";

        int required = format == TabsMatchFormat.OneVOne ? 1 : 3;
        var seen = new HashSet<ulong> { requester.Id };
        foreach (var user in mentionedUsers)
        {
            if (user.IsBot)
            {
                error = "Choose players, not bots.";
                return false;
            }

            if (!seen.Add(user.Id))
            {
                error = "Each player slot needs a different Discord user.";
                return false;
            }

            players.Add((user.Id, DisplayName(user)));
        }

        if (players.Count != required)
        {
            error = format == TabsMatchFormat.OneVOne
                ? "Mention exactly 1 opponent, like `@Player`."
                : "Mention exactly 3 players in order: red teammate, blue player 1, blue player 2.";
            return false;
        }

        return true;
    }

    private static bool TryResolveOpponent(
        SocketUser requester,
        SocketGuildChannel? channel,
        IEnumerable<IUser> mentionedUsers,
        string raw,
        out ulong userId,
        out string displayName,
        out string error)
    {
        userId = 0;
        displayName = "";
        error = "";

        var mentioned = mentionedUsers.FirstOrDefault(user => user.Id != requester.Id);
        if (mentioned != null)
        {
            if (mentioned.IsBot)
            {
                error = "Choose a player, not a bot.";
                return false;
            }

            userId = mentioned.Id;
            displayName = DisplayName(mentioned);
            return true;
        }

        string cleaned = raw.Trim();
        if (cleaned.StartsWith("<@", StringComparison.Ordinal) && cleaned.EndsWith(">", StringComparison.Ordinal))
            cleaned = cleaned.Trim('<', '>', '@', '!');

        if (ulong.TryParse(cleaned, out ulong parsedId))
        {
            if (parsedId == requester.Id)
            {
                error = "Choose someone other than yourself as the opponent.";
                return false;
            }

            var cachedUser = channel?.Guild.GetUser(parsedId);

            if (cachedUser?.IsBot == true)
            {
                error = "Choose a player, not a bot.";
                return false;
            }

            userId = parsedId;
            displayName = cachedUser == null ? $"Player {parsedId}" : DisplayName(cachedUser);
            return true;
        }

        string name = cleaned.TrimStart('@').Trim();
        if (name.Length == 0)
        {
            error = "Type an opponent mention, like `@Player`, or paste their Discord user ID.";
            return false;
        }

        if (channel == null)
        {
            error = "Type a Discord mention or raw user ID.";
            return false;
        }

        var match = channel.Guild.Users.FirstOrDefault(user =>
            string.Equals(user.DisplayName, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Username, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.GlobalName, name, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            error = "I could not find that player. Try pasting their raw Discord user ID instead.";
            return false;
        }

        if (match.Id == requester.Id)
        {
            error = "Choose someone other than yourself as the opponent.";
            return false;
        }

        if (match.IsBot)
        {
            error = "Choose a player, not a bot.";
            return false;
        }

        userId = match.Id;
        displayName = DisplayName(match);
        return true;
    }

    private static string DisplayName(IUser user)
    {
        if (user is SocketGuildUser guildUser && !string.IsNullOrWhiteSpace(guildUser.DisplayName))
            return guildUser.DisplayName;

        if (user is SocketUser socketUser && !string.IsNullOrWhiteSpace(socketUser.GlobalName))
            return socketUser.GlobalName;

        return user.Username;
    }

    private async Task CancelPromptAsync(SocketMessageComponent component)
    {
        var key = (component.Channel.Id, component.User.Id);
        _setupSessions.TryGetValue(key, out var setup);
        _pendingInvites.TryGetValue(key, out var invite);
        bool deletePrivateChannel =
            (setup != null && IsActualPrivateMatchSetupChannel(setup)) ||
            (invite != null && IsActualPrivateMatchSetupChannel(invite));
        bool cleanupMatchmakingChannel =
            component.Channel is SocketTextChannel &&
            ((setup != null && setup.UsesPrivateMatchChannel) ||
             (invite != null && invite.UsesPrivateMatchChannel));

        _setupSessions.Remove(key);
        _pendingInvites.Remove(key);
        _pendingSaveDeletes.Remove(key);
        _awaitingOpponentMentions.Remove(key);
        _setupPromptInteractions.Remove(key);

        await component.UpdateAsync(properties =>
        {
            properties.Content = "Cancelled.";
            properties.Embed = null;
            properties.Components = new ComponentBuilder().Build();
        });
        if (deletePrivateChannel)
            _ = DeleteChannelAfterDelayAsync(component.Channel.Id, TimeSpan.FromSeconds(3));
        else if (cleanupMatchmakingChannel)
        {
            if (invite != null)
                _ = DeleteMatchmakingMatchHistoryAsync(component.Channel, invite);
            else if (setup != null)
                _ = DeleteMatchmakingSetupHistoryAsync(component.Channel, setup);
        }
        _ = DeleteOriginalResponseAfterDelayAsync(component);
    }

    private async Task RespondTemporaryAsync(SocketInteraction interaction, string message, TimeSpan? deleteAfter = null)
    {
        if (interaction.HasResponded)
        {
            await FollowupTemporaryAsync(interaction, message, deleteAfter);
            return;
        }

        await interaction.RespondAsync(message, ephemeral: true);
        _ = DeleteOriginalResponseAfterDelayAsync(interaction, deleteAfter ?? TimeSpan.FromSeconds(2));
    }

    private async Task RespondArmyBoughtAsync(SocketInteraction interaction, TabsMatch match, int player, string unitSlug, int quantity)
    {
        var embed = DiscordMatchRenderer.BuildArmyBoughtEmbed(match, player, unitSlug, quantity);
        if (interaction.HasResponded)
        {
            await FollowupArmyBoughtAsync(interaction, match, player, unitSlug, quantity);
            return;
        }

        await interaction.RespondAsync(embed: embed, ephemeral: true);
        _ = DeleteOriginalResponseAfterDelayAsync(interaction, TimeSpan.FromSeconds(5));
    }

    private async Task FollowupTemporaryAsync(SocketInteraction interaction, string message, TimeSpan? deleteAfter = null)
    {
        var followup = await interaction.FollowupAsync(message, ephemeral: true);
        _ = DeleteFollowupAfterDelayAsync(followup, deleteAfter ?? TimeSpan.FromSeconds(2));
    }

    private async Task FollowupArmyBoughtAsync(SocketInteraction interaction, TabsMatch match, int player, string unitSlug, int quantity)
    {
        var followup = await interaction.FollowupAsync(
            embed: DiscordMatchRenderer.BuildArmyBoughtEmbed(match, player, unitSlug, quantity),
            ephemeral: true);
        _ = DeleteFollowupAfterDelayAsync(followup, TimeSpan.FromSeconds(5));
    }

    private static async Task FollowupPersistentAsync(SocketInteraction interaction, string message)
    {
        await interaction.FollowupAsync(message, ephemeral: true);
    }

    private static async Task DeleteOriginalResponseAfterDelayAsync(SocketInteraction interaction)
    {
        await DeleteOriginalResponseAfterDelayAsync(interaction, TimeSpan.FromSeconds(2));
    }

    private static async Task DeleteOriginalResponseAfterDelayAsync(SocketInteraction interaction, TimeSpan delay)
    {
        await Task.Delay(delay);
        try
        {
            await interaction.DeleteOriginalResponseAsync();
        }
        catch
        {
            // Discord may already have dismissed or expired ephemeral interaction messages.
        }
    }

    private async Task DeleteStoredSetupPromptAsync((ulong ChannelId, ulong HostUserId) key)
    {
        if (!_setupPromptInteractions.Remove(key, out var interaction))
            return;

        try
        {
            await interaction.DeleteOriginalResponseAsync();
        }
        catch
        {
            // Setup prompts are ephemeral and can expire or be dismissed client-side.
        }
    }

    private static async Task DeleteMessageAfterDelayAsync(IMessage message, TimeSpan delay)
    {
        await Task.Delay(delay);
        await DeleteMessageWithLogAsync(message, "delayed message cleanup");
    }

    private static async Task DeleteMessageWithLogAsync(IMessage message, string reason)
    {
        try
        {
            await message.DeleteAsync();
        }
        catch (Exception ex)
        {
            string channelName = message.Channel?.Name ?? message.Channel?.Id.ToString() ?? "unknown";
            Console.WriteLine($"Could not delete {reason} in {channelName}: {ex.Message}");
        }
    }

    private static async Task DeleteFollowupAfterDelayAsync(RestFollowupMessage message, TimeSpan delay)
    {
        await Task.Delay(delay);
        try
        {
            await message.DeleteAsync();
        }
        catch
        {
            // Discord may already have dismissed or expired ephemeral followups.
        }
    }

    private static async Task SafeRespondAsync(SocketSlashCommand command, string message, bool ephemeral)
    {
        if (command.HasResponded)
            await command.FollowupAsync(message, ephemeral: ephemeral);
        else
            await command.RespondAsync(message, ephemeral: ephemeral);
    }

    private sealed class OpenPlayerPanel
    {
        public required SocketMessageComponent Interaction { get; init; }
        public bool IsActionsView { get; init; }
        public DateTimeOffset LastTouchedUtc { get; init; }
    }

    private sealed class MilestoneWarning
    {
        public required IUserMessage Message { get; init; }
        public required int Threshold { get; init; }
        public required string Reward { get; init; }
    }

    private static string BuildGuide()
    {
        return """
        **TABS Arena Bot Guide**

        `/tabs-start` opens the hosted start menu for players with the Host role.
        Create New Match lets the host choose format, FT mode, Faction Mode, and the required invited players before sending an invite.
        All invited players must accept before the match is created.
        Saves are bound to the host's Discord account. Use the match Saves button for new game, load save, delete save, or save with a popup name.
        Use `/tabs-start` to create a hosted match invite or manage saves before a match is open.
        Use the main buttons to choose who goes first, mark round results, run the tie timer, advance rounds, undo, or open Saves.
        Use the player action buttons for upgrades, utility buys, BFT, and Army.
        Army lets players buy tracked units, custom buy quantities, sell owned units, and custom sell quantities.
        Player names come from each player's Discord display name.
        """;
    }
}
