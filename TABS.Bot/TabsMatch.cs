using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tabs.Bot;

public enum TabsMatchFormat
{
    OneVOne,
    TwoVTwo
}

public enum TabsMatchMode
{
    FT13,
    FT20,
    FT30
}

public sealed class MutationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";

    public static MutationResult Ok(string message) => new() { Success = true, Message = message };
    public static MutationResult Fail(string message) => new() { Success = false, Message = message };
}

public sealed class PlayerState
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ulong DiscordUserId { get; set; }
    public int Gold { get; set; } = 1200;
    public int Income { get; set; }
    public int IncomeUpgrades { get; set; }
    public decimal IncomeCost { get; set; } = 130m;
    public bool BoughtIncomeThisRound { get; set; }
    public int IncomeMissedRounds { get; set; }
    public int IncomeDecayPct { get; set; }
    public int PermMoveUpgrades { get; set; }
    public int PermMovePurchases { get; set; }
    public bool PermMoveCapUnlocked { get; set; }
    public int FactionPurchases { get; set; }
    public int ChosenFactionPurchases { get; set; }
    public List<string> Factions { get; set; } = new();
    public int NextIncomeDiscountPct { get; set; }
    public int NextSellBonusPct { get; set; }
    public int NextFactionDiscountPct { get; set; }
    public int NextChosenFactionDiscountPct { get; set; }
    public int NextPermMoveDiscountPct { get; set; }
    public int SellbackPct { get; set; } = 50;
    public bool HasFullRefund { get; set; }
    public bool ReplayBoughtThisRound { get; set; }
    public bool FreeFactionChoicePending { get; set; }
    public Dictionary<string, int> ArmyUnits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TabsMatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public ulong MatchmakingChannelId { get; set; }
    public bool IsPrivateMatchChannel { get; set; }
    public ulong PrivateVoiceChannelId { get; set; }
    public DateTimeOffset? ChannelDeleteAfterUtc { get; set; }
    public string LoadedSaveName { get; set; } = "";
    public ulong HostUserId { get; set; }
    public string HostDisplayName { get; set; } = "";
    public ulong InvitedUserId { get; set; }
    public string InvitedDisplayName { get; set; } = "";
    public ulong Player1UserId { get; set; }
    public string Player1DisplayName { get; set; } = "";
    public ulong Player2UserId { get; set; }
    public string Player2DisplayName { get; set; } = "";
    public ulong Player3UserId { get; set; }
    public string Player3DisplayName { get; set; } = "";
    public ulong Player4UserId { get; set; }
    public string Player4DisplayName { get; set; } = "";
    public bool InviteAccepted { get; set; }
    public TabsMatchFormat Format { get; set; }
    public TabsMatchMode Mode { get; set; } = TabsMatchMode.FT20;
    public bool FactionModeEnabled { get; set; } = true;
    public bool ModesLocked { get; set; }
    public int Round { get; set; } = 1;
    public int PendingWinner { get; set; }
    public int LastRoundWinner { get; set; }
    public bool FirstTurnChosen { get; set; }
    public int FirstTurnTeam { get; set; }
    public int ActiveTurnTeam { get; set; }
    public int TurnPassesThisRound { get; set; }
    public HashSet<int> EndedTurnPlayers { get; set; } = new();
    public int RedPoints { get; set; }
    public int BluePoints { get; set; }
    public bool RedReplayBoughtThisRound { get; set; }
    public bool BlueReplayBoughtThisRound { get; set; }
    public bool ArmyTransactionThisRound { get; set; }
    public int RedPermanentSellbackBonusPct { get; set; }
    public int BluePermanentSellbackBonusPct { get; set; }
    public int RedBftSurchargePct { get; set; } = 15;
    public int BlueBftSurchargePct { get; set; } = 15;
    public bool MatchEndAnnounced { get; set; }
    public int ForfeitedTeam { get; set; }
    public List<PlayerState> Players { get; set; } = new();
    public List<string> RewardQueue { get; set; } = new();
    public HashSet<int> ClaimedMilestones { get; set; } = new();
    public int NextMilestoneThreshold { get; set; }
    public List<string> ActionLog { get; set; } = new();
    public int TimerRemainingSeconds { get; set; } = 120;
    public bool TimerRunning { get; set; }
    public DateTimeOffset? TimerEndsAtUtc { get; set; }
    public bool TimerExpiredNotified { get; set; }
    public int MatchTimerRemainingSeconds { get; set; } = 240;
    public bool MatchTimerRunning { get; set; }
    public DateTimeOffset? MatchTimerEndsAtUtc { get; set; }
    public bool MatchTimerExpiredNotified { get; set; }

    [JsonIgnore]
    public List<string> UndoStack { get; set; } = new();

    public static readonly string[] AllFactions =
    {
        "Ancient", "Good", "Dynasty", "Farmer", "Evil", "Legacy",
        "Medieval", "New Units", "New Units 2", "Pirate", "Renaissance", "Secret",
        "Tribal", "Viking", "Wild West", "Spooky"
    };

    private static readonly string[] OneVOneBaseRewardPool =
    {
        "perm_move_upgrade", "perm_move_upgrade", "sellback_20",
        "income_discount", "income_discount", "full_refund", "full_refund"
    };

    private static readonly string[] OneVOneBaseRewardPoolNoIncome =
    {
        "perm_move_upgrade", "perm_move_upgrade", "sellback_20", "full_refund", "full_refund"
    };

    private static readonly string[] OneVOneFactionRewardPool =
    {
        "choose_free_faction", "free_faction", "free_faction", "free_faction", "free_faction",
        "perm_move_upgrade", "sellback_20", "income_discount", "income_discount", "full_refund"
    };

    private static readonly string[] OneVOneFactionRewardPoolNoIncome =
    {
        "choose_free_faction", "free_faction", "free_faction", "free_faction", "free_faction",
        "perm_move_upgrade", "sellback_20", "full_refund"
    };

    public static TabsMatch Create(
        ulong channelId,
        TabsMatchFormat format,
        TabsMatchMode mode,
        bool factionMode,
        ulong hostUserId = 0,
        string hostDisplayName = "",
        ulong invitedUserId = 0,
        string invitedDisplayName = "",
        ulong player1UserId = 0,
        string player1DisplayName = "",
        ulong player2UserId = 0,
        string player2DisplayName = "",
        ulong player3UserId = 0,
        string player3DisplayName = "",
        ulong player4UserId = 0,
        string player4DisplayName = "",
        bool isPrivateMatchChannel = false,
        ulong matchmakingChannelId = 0)
    {
        player1UserId = player1UserId == 0 ? hostUserId : player1UserId;
        player1DisplayName = string.IsNullOrWhiteSpace(player1DisplayName) ? hostDisplayName : player1DisplayName;
        player2UserId = player2UserId == 0 ? invitedUserId : player2UserId;
        player2DisplayName = string.IsNullOrWhiteSpace(player2DisplayName) ? invitedDisplayName : player2DisplayName;
        var match = new TabsMatch
        {
            ChannelId = channelId,
            MatchmakingChannelId = matchmakingChannelId,
            IsPrivateMatchChannel = isPrivateMatchChannel,
            HostUserId = hostUserId,
            HostDisplayName = hostDisplayName,
            InvitedUserId = invitedUserId,
            InvitedDisplayName = invitedDisplayName,
            Player1UserId = player1UserId,
            Player1DisplayName = player1DisplayName,
            Player2UserId = player2UserId,
            Player2DisplayName = player2DisplayName,
            Player3UserId = player3UserId,
            Player3DisplayName = player3DisplayName,
            Player4UserId = player4UserId,
            Player4DisplayName = player4DisplayName,
            InviteAccepted = invitedUserId != 0,
            Format = format,
            Mode = mode,
            FactionModeEnabled = factionMode
        };

        match.ResetForCurrentSettings(false);
        match.Log($"Created {match.FormatLabel} {match.ModeLabel} match. Faction Mode: {(factionMode ? "ON" : "OFF")}.");
        return match;
    }

    public string FormatLabel => Format == TabsMatchFormat.OneVOne ? "1v1" : "2v2";
    public string ModeLabel => Mode.ToString().ToUpperInvariant();
    public int PlayerCount => Format == TabsMatchFormat.OneVOne ? 2 : 4;
    public int GoalPoints => Mode == TabsMatchMode.FT13 ? 13 : Mode == TabsMatchMode.FT20 ? 20 : 30;
    public bool IsIncomeAvailable => true;
    public int TimedMilestoneStep => Mode == TabsMatchMode.FT13 ? 3 : 4;
    public bool IsTimedTeamMilestoneMode => Format == TabsMatchFormat.TwoVTwo && (Mode == TabsMatchMode.FT13 || Mode == TabsMatchMode.FT20);

    public void ResetForCurrentSettings(bool snapshot = true)
    {
        if (snapshot)
            PushUndo();

        Round = 1;
        PendingWinner = 0;
        LastRoundWinner = 0;
        FirstTurnChosen = false;
        FirstTurnTeam = 0;
        ActiveTurnTeam = 0;
        TurnPassesThisRound = 0;
        EndedTurnPlayers = new HashSet<int>();
        ModesLocked = false;
        RedPoints = 0;
        BluePoints = 0;
        RedReplayBoughtThisRound = false;
        BlueReplayBoughtThisRound = false;
        ArmyTransactionThisRound = false;
        RedPermanentSellbackBonusPct = 0;
        BluePermanentSellbackBonusPct = 0;
        RedBftSurchargePct = 15;
        BlueBftSurchargePct = 15;
        MatchEndAnnounced = false;
        ForfeitedTeam = 0;
        TimerRemainingSeconds = 120;
        TimerRunning = false;
        TimerEndsAtUtc = null;
        TimerExpiredNotified = false;
        MatchTimerRemainingSeconds = 240;
        MatchTimerRunning = false;
        MatchTimerEndsAtUtc = null;
        MatchTimerExpiredNotified = false;
        ClaimedMilestones = new HashSet<int>();
        ActionLog = new List<string>();
        Players = BuildStartingPlayers();
        BuildRewardQueue();
        if (FactionModeEnabled)
            AssignStartingFactions();
    }

    public MutationResult SetMode(TabsMatchMode mode)
    {
        if (ModesLocked)
            return MutationResult.Fail("Modes are locked after the first round has advanced.");

        PushUndo();
        Mode = mode;
        ResetForCurrentSettings(false);
        Log($"Mode switched to {ModeLabel}. Match reset with current faction setting.");
        return MutationResult.Ok($"Mode switched to {ModeLabel}.");
    }

    public MutationResult SetFactionMode(bool enabled)
    {
        if (ModesLocked)
            return MutationResult.Fail("Faction Mode is locked after the first round has advanced.");

        PushUndo();
        FactionModeEnabled = enabled;
        ResetForCurrentSettings(false);
        Log($"Faction Mode switched {(enabled ? "ON" : "OFF")}. Match reset.");
        return MutationResult.Ok($"Faction Mode switched {(enabled ? "ON" : "OFF")}.");
    }

    public MutationResult RenamePlayer(int player, string name)
    {
        var p = GetPlayer(player);
        if (p == null)
            return MutationResult.Fail("That player does not exist in this format.");

        name = name.Trim();
        if (name.Length == 0)
            return MutationResult.Fail("Name cannot be blank.");

        PushUndo();
        p.Name = name.Length > 32 ? name[..32] : name;
        Log($"P{player} renamed to {p.Name}.");
        return MutationResult.Ok($"P{player} renamed to {p.Name}.");
    }

    public MutationResult ChooseFirst(int team)
    {
        if (Round != 1 || FirstTurnChosen)
            return MutationResult.Fail("First turn has already been chosen.");

        PushUndo();
        FirstTurnChosen = true;
        FirstTurnTeam = team;
        BeginRoundTurns();
        int bonus = Format == TabsMatchFormat.OneVOne ? 50 : 40;
        foreach (int playerId in TeamPlayerIds(team))
            GetPlayer(playerId)!.Gold += bonus;

        Log($"{TeamName(team)} goes first and receives +{bonus}g per player.");
        return MutationResult.Ok($"{TeamName(team)} goes first. +{bonus}g applied.");
    }

    public MutationResult MarkWinner(int winner)
    {
        if (winner < 1 || winner > 3)
            return MutationResult.Fail("Invalid round result.");

        PushUndo();
        PendingWinner = winner;
        Log($"Pending result set: {(winner == 3 ? "Tie" : TeamName(winner) + " wins")}.");
        return MutationResult.Ok($"Pending result set: {(winner == 3 ? "Tie" : TeamName(winner) + " wins")}.");
    }

    public MutationResult Forfeit(int player)
    {
        if (MatchEndAnnounced)
            return MutationResult.Fail("This match is already over.");

        var forfeitingPlayer = RequirePlayer(player);
        int forfeitingTeam = TeamOfPlayer(player);
        int winningTeam = OtherTeam(forfeitingTeam);

        PushUndo();
        ForfeitedTeam = forfeitingTeam;
        MatchEndAnnounced = true;
        PendingWinner = 0;
        TimerRunning = false;
        TimerEndsAtUtc = null;
        MatchTimerRunning = false;
        MatchTimerEndsAtUtc = null;

        string message = $"{forfeitingPlayer.Name} forfeited. {TeamName(winningTeam)} wins by forfeit.";
        Log(message);
        return MutationResult.Ok(message);
    }

    public MutationResult NextRound()
    {
        if (PendingWinner == 0)
            return MutationResult.Fail("Set a round winner or tie before advancing.");

        PushUndo();
        int winner = PendingWinner;
        int previousRed = RedPoints;
        int previousBlue = BluePoints;
        ModesLocked = true;

        if (winner == 1)
            RedPoints++;
        else if (winner == 2)
            BluePoints++;

        foreach (var player in Players)
        {
            int interest = CalcInterest(player.Gold);
            if (interest > 0)
                player.Gold += interest;
        }

        if (IsIncomeAvailable)
        {
            foreach (var player in Players)
            {
                if (player.Income > 0)
                    player.Gold += player.Income;
            }
        }

        ApplyIncomeDecay();
        ResetRoundPurchaseFlags();
        ApplyRoundRewards(winner, out int winnerReward, out int loserReward, out int tieReward);
        CheckMilestones(winner, previousRed, previousBlue);

        string resultText = winner == 3
            ? $"Round {Round} ended in a tie. Everyone +{tieReward}g."
            : $"Round {Round}: {TeamName(winner)} won. Winners +{winnerReward}g, losers +{loserReward}g.";
        Log(resultText);

        if (winner == 1 || winner == 2)
            LastRoundWinner = winner;

        PendingWinner = 0;
        Round++;
        RestartTimer(false);
        ResetMatchTimer(false);
        BeginRoundTurns();
        AnnounceMatchEndIfNeeded();
        return MutationResult.Ok(resultText);
    }

    public MutationResult BuyIncome(int player)
    {
        var p = RequirePlayer(player);
        if (p.BoughtIncomeThisRound)
            return MutationResult.Fail($"{p.Name} already bought income this round.");

        int totalDiscountPct = p.IncomeDecayPct + p.NextIncomeDiscountPct;
        int cost = (int)Math.Ceiling(Math.Max(1m, Math.Round(p.IncomeCost * (1m - totalDiscountPct / 100m))));
        if (p.Gold < cost)
            return MutationResult.Fail($"{p.Name} needs {cost}g and has {p.Gold}g.");

        PushUndo();
        int gain = Mode == TabsMatchMode.FT13 ? 18 : Mode == TabsMatchMode.FT20 ? 13 : 10;
        p.Gold -= cost;
        p.Income += gain;
        p.IncomeUpgrades++;
        p.BoughtIncomeThisRound = true;
        p.IncomeMissedRounds = 0;
        p.IncomeDecayPct = 0;
        p.IncomeCost = Math.Round(GetBaseIncomeCost() * (decimal)Math.Pow(1.24, p.IncomeUpgrades));
        p.NextIncomeDiscountPct = 0;
        Log($"{p.Name} bought income +{gain} for {cost}g. Income now +{p.Income}.");
        return MutationResult.Ok($"{p.Name} bought income +{gain} for {cost}g.");
    }

    public MutationResult BuyPermMove(int player)
    {
        var p = RequirePlayer(player);
        int max = p.PermMoveCapUnlocked ? 3 : 2;
        if (p.PermMovePurchases >= max)
            return MutationResult.Fail($"{p.Name} has max paid perm move purchases ({max}).");

        int cost = Discounted(GetPermMoveBaseCost(), p.NextPermMoveDiscountPct);
        if (p.Gold < cost)
            return MutationResult.Fail($"{p.Name} needs {cost}g and has {p.Gold}g.");

        PushUndo();
        p.Gold -= cost;
        p.PermMovePurchases++;
        p.PermMoveUpgrades++;
        p.NextPermMoveDiscountPct = 0;
        Log($"{p.Name} bought perm move for {cost}g. Total perm move: {p.PermMoveUpgrades}.");
        return MutationResult.Ok($"{p.Name} bought perm move for {cost}g.");
    }

    public MutationResult BuyRandomFaction(int player)
    {
        if (!FactionModeEnabled)
            return MutationResult.Fail("Faction Mode is OFF.");

        var p = RequirePlayer(player);
        var available = AvailableFactions(p).ToList();
        if (available.Count == 0)
            return MutationResult.Fail($"{p.Name} already owns every faction.");

        int cost = Discounted(GetFactionCost(p), p.NextFactionDiscountPct);
        if (p.Gold < cost)
            return MutationResult.Fail($"{p.Name} needs {cost}g and has {p.Gold}g.");

        PushUndo();
        string faction = available[Random.Shared.Next(available.Count)];
        p.Gold -= cost;
        p.Factions.Add(faction);
        p.FactionPurchases++;
        p.NextFactionDiscountPct = 0;
        Log($"{p.Name} bought random faction {faction} for {cost}g. Next faction: {GetFactionCost(p)}g.");
        return MutationResult.Ok($"{p.Name} received {faction} for {cost}g.");
    }

    public MutationResult BuyChosenFaction(int player, string faction, bool free)
    {
        if (!FactionModeEnabled)
            return MutationResult.Fail("Faction Mode is OFF.");

        var p = RequirePlayer(player);
        if (!AllFactions.Contains(faction))
            return MutationResult.Fail("That faction is not valid.");
        if (p.Factions.Contains(faction))
            return MutationResult.Fail($"{p.Name} already owns {faction}.");

        int cost = free ? 0 : Discounted(GetChosenFactionCost(p), p.NextChosenFactionDiscountPct);
        if (!free && p.Gold < cost)
            return MutationResult.Fail($"{p.Name} needs {cost}g and has {p.Gold}g.");
        if (free && !p.FreeFactionChoicePending)
            return MutationResult.Fail($"{p.Name} does not have a free faction choice waiting.");

        PushUndo();
        p.Gold -= cost;
        p.Factions.Add(faction);
        p.FactionPurchases++;
        if (free)
            p.FreeFactionChoicePending = false;
        else
            p.NextChosenFactionDiscountPct = 0;

        Log(free
            ? $"{p.Name} claimed free chosen faction: {faction}."
            : $"{p.Name} bought chosen faction {faction} for {cost}g.");
        return MutationResult.Ok(free ? $"{p.Name} claimed {faction} for free." : $"{p.Name} bought {faction} for {cost}g.");
    }

    public MutationResult SingleTroopMove(int player)
    {
        int cost = Mode == TabsMatchMode.FT13 ? 20 : 25;
        return Spend(player, cost, $"single troop move ({cost}g)");
    }

    public MutationResult BuyReplay(int player)
    {
        var p = RequirePlayer(player);
        int team = TeamOfPlayer(player);
        if (ArmyTransactionThisRound)
            return MutationResult.Fail("Replay is disabled this round because a unit was bought or sold.");

        bool used = Format == TabsMatchFormat.OneVOne
            ? p.ReplayBoughtThisRound
            : team == 1 ? RedReplayBoughtThisRound : BlueReplayBoughtThisRound;
        if (used)
            return MutationResult.Fail($"{TeamName(team)} already bought replay this round.");

        if (p.Gold < 10)
            return MutationResult.Fail($"{p.Name} needs 10g and has {p.Gold}g.");

        PushUndo();
        p.Gold -= 10;
        if (Format == TabsMatchFormat.OneVOne)
            p.ReplayBoughtThisRound = true;
        else if (team == 1)
            RedReplayBoughtThisRound = true;
        else
            BlueReplayBoughtThisRound = true;

        Log($"{p.Name} bought replay for 10g.");
        return MutationResult.Ok($"{p.Name} bought replay for 10g.");
    }

    public MutationResult CustomSpend(int player, int amount)
    {
        if (amount <= 0)
            return MutationResult.Fail("Enter a positive amount.");
        return Spend(player, amount, $"custom troop spend ({amount}g)");
    }

    public MutationResult SellUnit(int player, int value)
    {
        if (value <= 0)
            return MutationResult.Fail("Enter a positive unit value.");

        var p = RequirePlayer(player);
        PushUndo();
        int totalPct;
        int refund;
        if (Format == TabsMatchFormat.OneVOne && p.HasFullRefund)
        {
            totalPct = 100;
            refund = value;
            p.HasFullRefund = false;
        }
        else
        {
            totalPct = GetDisplayedSellbackPct(player);
            refund = (int)Math.Floor(value * (totalPct / 100.0));
        }

        p.Gold += refund;
        p.NextSellBonusPct = 0;
        ArmyTransactionThisRound = true;
        Log($"{p.Name} sold unit worth {value}g for {refund}g ({totalPct}%).");
        return MutationResult.Ok($"{p.Name} sold unit for +{refund}g ({totalPct}%).");
    }

    public MutationResult BuyArmyUnit(int player, string unitSlug, int quantity)
    {
        if (quantity <= 0)
            return MutationResult.Fail("Enter a positive quantity.");

        var unit = ArmyCatalog.FindUnit(unitSlug);
        if (unit == null)
            return MutationResult.Fail("That unit has not been added yet.");

        var p = RequirePlayer(player);
        if (FactionModeEnabled && !p.Factions.Contains(unit.Faction, StringComparer.OrdinalIgnoreCase))
            return MutationResult.Fail($"{p.Name} does not own {unit.Faction}.");

        int total = unit.Gold * quantity;
        if (p.Gold < total)
            return MutationResult.Fail($"{p.Name} needs {total}g and has {p.Gold}g.");

        PushUndo();
        p.Gold -= total;
        EnsureArmyUnits(p);
        p.ArmyUnits[unit.Slug] = p.ArmyUnits.GetValueOrDefault(unit.Slug) + quantity;
        ArmyTransactionThisRound = true;
        Log($"{p.Name} bought {quantity}x {unit.Name} for {total}g.");
        return MutationResult.Ok($"{p.Name} bought {quantity}x {unit.Name} for {total}g.");
    }

    public MutationResult SellArmyUnit(int player, string unitSlug, int quantity)
    {
        if (quantity <= 0)
            return MutationResult.Fail("Enter a positive quantity.");

        var unit = ArmyCatalog.FindUnit(unitSlug);
        if (unit == null)
            return MutationResult.Fail("That unit has not been added yet.");

        var p = RequirePlayer(player);
        EnsureArmyUnits(p);
        int owned = p.ArmyUnits.GetValueOrDefault(unit.Slug);
        if (owned < quantity)
            return MutationResult.Fail($"{p.Name} only owns {owned}x {unit.Name}.");

        PushUndo();
        int refund = PreviewArmySellGold(player, unit.Slug, quantity);
        p.Gold += refund;
        p.ArmyUnits[unit.Slug] = owned - quantity;
        if (p.ArmyUnits[unit.Slug] <= 0)
            p.ArmyUnits.Remove(unit.Slug);
        p.NextSellBonusPct = 0;
        if (Format == TabsMatchFormat.OneVOne && p.HasFullRefund)
            p.HasFullRefund = false;

        ArmyTransactionThisRound = true;
        Log($"{p.Name} sold {quantity}x {unit.Name} for {refund}g ({GetDisplayedSellbackPct(player)}%).");
        return MutationResult.Ok($"{p.Name} sold {quantity}x {unit.Name} for +{refund}g.");
    }

    public int PreviewArmyBuyGold(string unitSlug, int quantity)
    {
        var unit = ArmyCatalog.FindUnit(unitSlug);
        return unit == null || quantity <= 0 ? 0 : unit.Gold * quantity;
    }

    public int PreviewArmySellGold(int player, string unitSlug, int quantity)
    {
        var unit = ArmyCatalog.FindUnit(unitSlug);
        if (unit == null || quantity <= 0)
            return 0;

        var p = RequirePlayer(player);
        int remaining = quantity;
        int refund = 0;
        if (Format == TabsMatchFormat.OneVOne && p.HasFullRefund)
        {
            refund += unit.Gold;
            remaining--;
        }

        if (remaining > 0)
            refund += (int)Math.Floor(unit.Gold * remaining * (GetDisplayedSellbackPct(player) / 100.0));

        return refund;
    }

    public int OwnedArmyCount(int player, string unitSlug)
    {
        var p = RequirePlayer(player);
        EnsureArmyUnits(p);
        return p.ArmyUnits.GetValueOrDefault(unitSlug);
    }

    public IReadOnlyList<ArmyUnit> OwnedArmyUnits(int player)
    {
        var p = RequirePlayer(player);
        EnsureArmyUnits(p);
        return p.ArmyUnits
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => ArmyCatalog.FindUnit(kvp.Key))
            .Where(unit => unit != null)
            .Select(unit => unit!)
            .OrderBy(unit => unit.Gold)
            .ThenBy(unit => unit.Name)
            .ToList();
    }

    public MutationResult BuyForTeammate(int player, int unitCost)
    {
        if (Format != TabsMatchFormat.TwoVTwo)
            return MutationResult.Fail("BFT is only used in 2v2.");
        if (unitCost <= 0)
            return MutationResult.Fail("Enter a positive unit gold value.");

        var p = RequirePlayer(player);
        int team = TeamOfPlayer(player);
        int surcharge = team == 1 ? RedBftSurchargePct : BlueBftSurchargePct;
        int total = (int)Math.Ceiling(unitCost * (1.0 + surcharge / 100.0));
        if (p.Gold < total)
            return MutationResult.Fail($"{p.Name} needs {total}g and has {p.Gold}g.");

        PushUndo();
        p.Gold -= total;
        Log($"{p.Name} BFT unit ({unitCost}g) -> paid {total}g (+{surcharge}% surcharge).");
        return MutationResult.Ok($"{p.Name} paid {total}g for BFT.");
    }

    public MutationResult EndTurn(int player)
    {
        var p = RequirePlayer(player);
        if (!FirstTurnChosen)
            return MutationResult.Fail("The host must choose who goes first before turns can end.");
        if (TurnPassesThisRound >= 2)
            return MutationResult.Fail("Both sides already ended their turns for this round.");
        if (ActiveTurnTeam == 0)
            return MutationResult.Fail("No team is currently taking actions.");

        int team = TeamOfPlayer(player);
        if (team != ActiveTurnTeam)
            return MutationResult.Fail($"It is {TeamName(ActiveTurnTeam)}'s turn right now.");
        if (EndedTurnPlayers.Contains(player))
            return MutationResult.Fail($"{p.Name} already ended their turn.");

        PushUndo();
        EndedTurnPlayers.Add(player);
        var waiting = TeamPlayerIds(team).Where(id => !EndedTurnPlayers.Contains(id)).Select(id => RequirePlayer(id).Name).ToList();
        if (waiting.Count > 0)
        {
            string waitText = $"{p.Name} ended turn. Waiting for {string.Join(", ", waiting)}.";
            Log(waitText);
            return MutationResult.Ok(waitText);
        }

        TurnPassesThisRound++;
        if (TurnPassesThisRound == 1)
        {
            ActiveTurnTeam = OtherTeam(team);
            string nextText = $"{TeamName(team)} ended turn. {TeamName(ActiveTurnTeam)} may act now.";
            Log(nextText);
            return MutationResult.Ok(nextText);
        }

        ActiveTurnTeam = 0;
        StartMatchTimerAfterTurns();
        string doneText = "Both sides ended their turns. Match timer started for 4:00.";
        Log(doneText);
        return MutationResult.Ok(doneText);
    }

    public MutationResult StartTimer()
    {
        RefreshMatchTimerClock();
        if (!CanUseTieTimer)
            return MutationResult.Fail("The match timer must finish before the tie timer can start.");

        RefreshTimerClock();
        if (TimerRunning)
            return MutationResult.Fail("Tie timer is already running.");

        PushUndo();
        if (TimerRemainingSeconds <= 0)
            TimerRemainingSeconds = 120;
        TimerEndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(TimerRemainingSeconds);
        TimerRunning = true;
        TimerExpiredNotified = false;
        Log("Tie timer started.");
        return MutationResult.Ok("Tie timer started.");
    }

    public MutationResult StopTimer()
    {
        RefreshTimerClock();
        if (!TimerRunning)
            return MutationResult.Fail("Tie timer is not running.");

        PushUndo();
        TimerRemainingSeconds = GetTimerRemainingSeconds();
        TimerRunning = false;
        TimerEndsAtUtc = null;
        Log($"Tie timer stopped at {TimerText}.");
        return MutationResult.Ok($"Tie timer stopped at {TimerText}.");
    }

    public MutationResult RestartTimer(bool snapshot = true)
    {
        if (snapshot)
            PushUndo();
        TimerRemainingSeconds = 120;
        TimerRunning = false;
        TimerEndsAtUtc = null;
        TimerExpiredNotified = false;
        if (snapshot)
            Log("Tie timer restarted to 2:00.");
        return MutationResult.Ok("Tie timer restarted to 2:00.");
    }

    public void ResetMatchTimer(bool snapshot = true)
    {
        if (snapshot)
            PushUndo();
        MatchTimerRemainingSeconds = 240;
        MatchTimerRunning = false;
        MatchTimerEndsAtUtc = null;
        MatchTimerExpiredNotified = false;
    }

    public void RefreshTimerClock()
    {
        if (!TimerRunning || TimerEndsAtUtc == null)
            return;

        int remaining = GetTimerRemainingSeconds();
        if (remaining <= 0)
        {
            TimerRemainingSeconds = 0;
            TimerRunning = false;
            TimerEndsAtUtc = null;
        }
    }

    public void RefreshMatchTimerClock()
    {
        if (!MatchTimerRunning || MatchTimerEndsAtUtc == null)
            return;

        int remaining = GetMatchTimerRemainingSeconds();
        if (remaining <= 0)
        {
            MatchTimerRemainingSeconds = 0;
            MatchTimerRunning = false;
            MatchTimerEndsAtUtc = null;
        }
    }

    public int GetTimerRemainingSeconds()
    {
        if (TimerRunning && TimerEndsAtUtc != null)
            return Math.Max(0, (int)Math.Ceiling((TimerEndsAtUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
        return Math.Max(0, TimerRemainingSeconds);
    }

    public int GetMatchTimerRemainingSeconds()
    {
        if (MatchTimerRunning && MatchTimerEndsAtUtc != null)
            return Math.Max(0, (int)Math.Ceiling((MatchTimerEndsAtUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
        return Math.Max(0, MatchTimerRemainingSeconds);
    }

    public string TimerText
    {
        get
        {
            int remaining = GetTimerRemainingSeconds();
            return $"{remaining / 60}:{remaining % 60:00}";
        }
    }

    public string MatchTimerText
    {
        get
        {
            int remaining = GetMatchTimerRemainingSeconds();
            return $"{remaining / 60}:{remaining % 60:00}";
        }
    }

    public MutationResult Undo()
    {
        if (UndoStack.Count == 0)
            return MutationResult.Fail("Nothing to undo.");

        string json = UndoStack[^1];
        UndoStack.RemoveAt(UndoStack.Count - 1);
        var restored = JsonSerializer.Deserialize<TabsMatch>(json, TabsJson.Options);
        if (restored == null)
            return MutationResult.Fail("Could not restore undo state.");

        var history = UndoStack;
        ulong currentChannelId = ChannelId;
        ulong currentMessageId = MessageId;
        ulong currentPrivateVoiceChannelId = PrivateVoiceChannelId;
        ulong currentMatchmakingChannelId = MatchmakingChannelId;
        bool currentIsPrivateMatchChannel = IsPrivateMatchChannel;
        DateTimeOffset? currentChannelDeleteAfterUtc = ChannelDeleteAfterUtc;
        CopyFrom(restored);
        ChannelId = currentChannelId;
        MessageId = currentMessageId;
        PrivateVoiceChannelId = currentPrivateVoiceChannelId;
        MatchmakingChannelId = currentMatchmakingChannelId;
        IsPrivateMatchChannel = currentIsPrivateMatchChannel;
        ChannelDeleteAfterUtc = currentChannelDeleteAfterUtc;
        UndoStack = history;
        Log("Undo applied.");
        return MutationResult.Ok("Undo applied.");
    }

    public PlayerState? GetPlayer(int id) => Players.FirstOrDefault(p => p.Id == id);
    public PlayerState RequirePlayer(int id) => GetPlayer(id) ?? throw new InvalidOperationException($"Player {id} not found.");

    public int TeamOfPlayer(int player)
    {
        if (Format == TabsMatchFormat.OneVOne)
            return player == 1 ? 1 : 2;
        return player <= 2 ? 1 : 2;
    }

    public IEnumerable<int> TeamPlayerIds(int team)
    {
        if (Format == TabsMatchFormat.OneVOne)
            return team == 1 ? new[] { 1 } : new[] { 2 };
        return team == 1 ? new[] { 1, 2 } : new[] { 3, 4 };
    }

    public string TeamName(int team) => team == 1 ? "Red" : "Blue";

    public int TeamPoints(int team) => team == 1 ? RedPoints : BluePoints;
    public int WinningTeam =>
        MatchEndAnnounced && ForfeitedTeam is 1 or 2
            ? OtherTeam(ForfeitedTeam)
            : MatchEndAnnounced && RedPoints != BluePoints
                ? RedPoints > BluePoints ? 1 : 2
                : 0;

    public bool NeedsFirstTurnChoice => !FirstTurnChosen;
    public bool TurnsComplete => FirstTurnChosen && TurnPassesThisRound >= 2;
    public bool CanUseTieTimer => TurnsComplete && GetMatchTimerRemainingSeconds() == 0 && !MatchTimerRunning;
    public bool IsPlayerTurnActive(int player) =>
        FirstTurnChosen &&
        TurnPassesThisRound < 2 &&
        ActiveTurnTeam == TeamOfPlayer(player) &&
        !EndedTurnPlayers.Contains(player);
    public bool IsPlayerTurnEnded(int player) => EndedTurnPlayers.Contains(player);

    public string TurnStatusText
    {
        get
        {
            if (!FirstTurnChosen)
                return "Waiting for host to choose first turn.";
            if (TurnsComplete)
                return "Both sides ended turns. Battle timer active.";
            if (ActiveTurnTeam == 0)
                return "No active turn.";

            var waiting = TeamPlayerIds(ActiveTurnTeam)
                .Where(id => !EndedTurnPlayers.Contains(id))
                .Select(id => Format == TabsMatchFormat.OneVOne ? TeamName(TeamOfPlayer(id)) : $"P{id}")
                .ToList();
            return $"{TeamName(ActiveTurnTeam)} turn - waiting on {string.Join(", ", waiting)}.";
        }
    }

    public string PendingText => PendingWinner == 0 ? "Not set" : PendingWinner == 3 ? "Tie" : $"{TeamName(PendingWinner)} wins";

    public string TurnOrderText
    {
        get
        {
            int first = 0;
            if (Round == 1 && FirstTurnTeam != 0)
                first = FirstTurnTeam;
            else if (RedPoints > BluePoints)
                first = 1;
            else if (BluePoints > RedPoints)
                first = 2;
            else if (LastRoundWinner != 0)
                first = LastRoundWinner;
            else if (FirstTurnTeam != 0)
                first = FirstTurnTeam;

            return first == 0 ? "Not available yet" : first == 1 ? "Red -> Blue" : "Blue -> Red";
        }
    }

    public int GetRoundRewardTier()
    {
        return Mode switch
        {
            TabsMatchMode.FT13 => ((Round - 1) / 2) * 35,
            TabsMatchMode.FT20 => ((Round - 1) / 3) * 20,
            _ => ((Round - 1) / 5) * 10
        };
    }

    public int GetWinnerRewardBase() => Mode switch
    {
        TabsMatchMode.FT13 => 95,
        TabsMatchMode.FT20 => 75,
        _ => 55
    };

    public int GetLoserRewardBase() => Mode switch
    {
        TabsMatchMode.FT13 => 125,
        TabsMatchMode.FT20 => 105,
        _ => 85
    };

    public int GetWinnerReward() => GetWinnerRewardBase() + GetRoundRewardTier();
    public int GetLoserReward() => GetLoserRewardBase() + GetRoundRewardTier();
    public int GetTieReward() => ((GetWinnerRewardBase() + GetLoserRewardBase()) / 2) + GetRoundRewardTier();
    public int GetPermMoveBaseCost() => Mode == TabsMatchMode.FT13 ? 150 : Mode == TabsMatchMode.FT20 ? 175 : 200;
    public decimal GetBaseIncomeCost() => Mode == TabsMatchMode.FT13 ? 140m : Mode == TabsMatchMode.FT20 ? 130m : 100m;
    public int GetMilestoneStep() => Mode == TabsMatchMode.FT13 ? 3 : Mode == TabsMatchMode.FT20 ? 4 : 5;
    public int CalcInterest(int gold) => Math.Min((gold / 50) * 10, 100);

    public int GetDisplayedIncomeCost(PlayerState p)
    {
        int totalDiscountPct = p.IncomeDecayPct + p.NextIncomeDiscountPct;
        return (int)Math.Ceiling(Math.Max(1m, Math.Round(p.IncomeCost * (1m - totalDiscountPct / 100m))));
    }

    public int GetFactionCost(PlayerState p)
    {
        int baseCost = 50;
        int scale = 20;
        return baseCost + p.FactionPurchases * scale;
    }

    public int GetChosenFactionCost(PlayerState p)
    {
        int baseCost = 280;
        return Math.Max(1, baseCost - (p.Factions.Count * 15));
    }

    public int GetDisplayedSellbackPct(int player)
    {
        var p = RequirePlayer(player);
        if (Format == TabsMatchFormat.OneVOne)
            return p.SellbackPct;

        int teamBonus = TeamOfPlayer(player) == 1 ? RedPermanentSellbackBonusPct : BluePermanentSellbackBonusPct;
        return 50 + teamBonus + p.NextSellBonusPct;
    }

    public IEnumerable<string> AvailableFactions(PlayerState player)
    {
        return AllFactions.Where(f => !player.Factions.Contains(f, StringComparer.OrdinalIgnoreCase));
    }

    public string RewardLabel(string reward)
    {
        return reward switch
        {
            "perm_move_upgrade" => "Perm Move Upgrade",
            "sellback_20" => "Sellback +20%",
            "income_discount" => "Income Discount (15%)",
            "full_refund" => "Full Unit Refund",
            "choose_free_faction" => "Choose Free Faction",
            "free_faction" => "Free Faction",
            "80% Off Next Faction" => "80% Off Next Faction",
            "80% Off Next Chosen Faction" => "80% Off Next Chosen Faction",
            "80% Off Next Perm Move" => "80% Off Next Perm Move",
            "Sellback +15%" => "Sellback +15%",
            "10% Off Next Income" => "10% Off Next Income",
            "+30% Next Sell" => "+30% Next Sell",
            "-5% BFT Surcharge" => "-5% BFT Surcharge",
            _ => reward
        };
    }

    public string RewardIcon(string reward)
    {
        return reward switch
        {
            "perm_move_upgrade" or "80% Off Next Perm Move" => "🏃",
            "sellback_20" or "Sellback +15%" or "+30% Next Sell" => "💱",
            "income_discount" or "10% Off Next Income" => "📈",
            "full_refund" => "↩️",
            "choose_free_faction" or "free_faction" or "80% Off Next Faction" or "80% Off Next Chosen Faction" => "⚔️",
            "-5% BFT Surcharge" => "🤝",
            _ => "🏆"
        };
    }

    public string NextRewardText => RewardQueue.Count == 0 ? "None left" : $"{RewardIcon(RewardQueue[0])} {RewardLabel(RewardQueue[0])}";

    public Dictionary<string, int> RewardCounts()
    {
        return RewardQueue.GroupBy(RewardLabel).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
    }

    public int NextMilestoneForTeam(int team)
    {
        if (Format == TabsMatchFormat.TwoVTwo)
            return NextMilestoneThreshold;

        int current = TeamPoints(team);
        int step = GetMilestoneStep();
        int check = ((current / step) + 1) * step;
        while (ClaimedMilestones.Contains(check))
            check += step;
        return check;
    }

    public void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        ActionLog.Insert(0, message);
        if (ActionLog.Count > 30)
            ActionLog.RemoveRange(30, ActionLog.Count - 30);
    }

    private List<PlayerState> BuildStartingPlayers()
    {
        string player1Name = PlayerNameOrDefault(Player1DisplayName, PlayerNameOrDefault(HostDisplayName, "Player 1"));
        string player2Name = PlayerNameOrDefault(Player2DisplayName, PlayerNameOrDefault(InvitedDisplayName, "Player 2"));
        string player3Name = PlayerNameOrDefault(Player3DisplayName, "Player 3");
        string player4Name = PlayerNameOrDefault(Player4DisplayName, "Player 4");
        var slots = Format == TabsMatchFormat.OneVOne
            ? new[]
            {
                (Name: player1Name, UserId: Player1UserId == 0 ? HostUserId : Player1UserId),
                (Name: player2Name, UserId: Player2UserId == 0 ? InvitedUserId : Player2UserId)
            }
            : new[]
            {
                (Name: player1Name, UserId: Player1UserId == 0 ? HostUserId : Player1UserId),
                (Name: player2Name, UserId: Player2UserId),
                (Name: player3Name, UserId: Player3UserId),
                (Name: player4Name, UserId: Player4UserId)
            };

        return slots.Select((slot, index) => new PlayerState
        {
            Id = index + 1,
            Name = slot.Name,
            DiscordUserId = slot.UserId,
            Gold = 1200,
            IncomeCost = GetBaseIncomeCost(),
            SellbackPct = 50
        }).ToList();
    }

    private void BeginRoundTurns()
    {
        EndedTurnPlayers = new HashSet<int>();
        TurnPassesThisRound = 0;
        ActiveTurnTeam = FirstTurnChosen ? DetermineFirstTurnTeamForCurrentRound() : 0;
        ResetMatchTimer(false);
    }

    private int DetermineFirstTurnTeamForCurrentRound()
    {
        if (Round == 1 && FirstTurnTeam != 0)
            return FirstTurnTeam;
        if (RedPoints > BluePoints)
            return 1;
        if (BluePoints > RedPoints)
            return 2;
        if (LastRoundWinner != 0)
            return LastRoundWinner;
        return FirstTurnTeam;
    }

    private void StartMatchTimerAfterTurns()
    {
        MatchTimerRemainingSeconds = 240;
        MatchTimerEndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(MatchTimerRemainingSeconds);
        MatchTimerRunning = true;
        MatchTimerExpiredNotified = false;
    }

    private static int OtherTeam(int team) => team == 1 ? 2 : 1;

    private static string PlayerNameOrDefault(string? name, string fallback)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0)
            return fallback;

        return name.Length > 28 ? name[..28] : name;
    }

    private void BuildRewardQueue()
    {
        IEnumerable<string> rewards;
        if (Format == TabsMatchFormat.OneVOne)
        {
            rewards = FactionModeEnabled ? OneVOneFactionRewardPool : OneVOneBaseRewardPool;
            NextMilestoneThreshold = GetMilestoneStep();
        }
        else
        {
            rewards = BuildTwoVTwoRewardPool();
            NextMilestoneThreshold = IsTimedTeamMilestoneMode ? TimedMilestoneStep : 5;
        }

        RewardQueue = rewards.OrderBy(_ => Random.Shared.Next()).ToList();
    }

    private IEnumerable<string> BuildTwoVTwoRewardPool()
    {
        var pool = new List<string>();
        if (FactionModeEnabled)
        {
            pool.AddRange(new[]
            {
                "80% Off Next Faction", "80% Off Next Faction", "80% Off Next Faction", "80% Off Next Faction",
                "80% Off Next Chosen Faction", "80% Off Next Perm Move",
                "Sellback +15%", "+30% Next Sell", "-5% BFT Surcharge"
            });
            pool.AddRange(new[] { "10% Off Next Income", "10% Off Next Income" });
        }
        else
        {
            pool.AddRange(new[]
            {
                "80% Off Next Perm Move", "80% Off Next Perm Move",
                "Sellback +15%", "+30% Next Sell", "+30% Next Sell", "-5% BFT Surcharge"
            });
            pool.AddRange(new[] { "10% Off Next Income", "10% Off Next Income", "10% Off Next Income" });
        }

        return pool;
    }

    private void AssignStartingFactions()
    {
        foreach (var player in Players)
        {
            player.Factions.Clear();
            var pool = AllFactions.ToList();
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int index = Random.Shared.Next(pool.Count);
                player.Factions.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }
    }

    private void ApplyRoundRewards(int winner, out int winnerReward, out int loserReward, out int tieReward)
    {
        winnerReward = GetWinnerReward();
        loserReward = GetLoserReward();
        tieReward = GetTieReward();

        if (winner == 3)
        {
            foreach (var player in Players)
                player.Gold += tieReward;
            return;
        }

        foreach (var player in Players)
        {
            bool won = TeamOfPlayer(player.Id) == winner;
            player.Gold += won ? winnerReward : loserReward;
        }
    }

    private void CheckMilestones(int winner, int previousRed, int previousBlue)
    {
        if (winner != 1 && winner != 2)
            return;

        if (Format == TabsMatchFormat.OneVOne)
        {
            CheckOneVOneMilestone(winner, winner == 1 ? previousRed : previousBlue);
            return;
        }

        bool redHit = RedPoints >= NextMilestoneThreshold;
        bool blueHit = BluePoints >= NextMilestoneThreshold;
        if (redHit && RewardQueue.Count > 0)
            AwardTeamReward(1);
        if (blueHit && RewardQueue.Count > 0)
            AwardTeamReward(2);
    }

    private void CheckOneVOneMilestone(int winner, int previousWinnerPoints)
    {
        int points = TeamPoints(winner);
        int step = GetMilestoneStep();
        if (points <= 0 || points % step != 0 || points <= previousWinnerPoints || ClaimedMilestones.Contains(points))
            return;

        ClaimedMilestones.Add(points);
        AwardOneVOneReward(winner);
    }

    private void AwardOneVOneReward(int playerId)
    {
        if (RewardQueue.Count == 0)
        {
            Log("Milestone reached, but reward pool is empty.");
            return;
        }

        var player = RequirePlayer(playerId);
        string reward = RewardQueue[0];
        RewardQueue.RemoveAt(0);

        switch (reward)
        {
            case "choose_free_faction":
                player.FreeFactionChoicePending = true;
                Log($"Milestone: {player.Name} may choose one free faction.");
                break;
            case "free_faction":
                var faction = AvailableFactions(player).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
                if (faction == null)
                    Log($"Milestone: {player.Name} already owns all factions.");
                else
                {
                    player.Factions.Add(faction);
                    player.FactionPurchases++;
                    Log($"Milestone: {player.Name} receives free faction: {faction}.");
                }
                break;
            case "perm_move_upgrade":
                player.PermMoveUpgrades++;
                Log($"Milestone: {player.Name} receives +1 perm move.");
                break;
            case "sellback_20":
                player.SellbackPct = Math.Min(100, player.SellbackPct + 20);
                Log($"Milestone: {player.Name} sellback is now {player.SellbackPct}%.");
                break;
            case "income_discount":
                player.NextIncomeDiscountPct = 15;
                Log($"Milestone: {player.Name} receives 15% off next income.");
                break;
            case "full_refund":
                player.HasFullRefund = true;
                Log($"Milestone: {player.Name} receives a one-time full troop refund.");
                break;
        }
    }

    private void AwardTeamReward(int team)
    {
        if (RewardQueue.Count == 0)
            return;

        string reward = RewardQueue[0];
        RewardQueue.RemoveAt(0);
        ApplyTeamReward(reward, team);
        Log($"Milestone {NextMilestoneThreshold}: {TeamName(team)} receives {RewardLabel(reward)}.");
        NextMilestoneThreshold += IsTimedTeamMilestoneMode ? TimedMilestoneStep : 5;
    }

    private void ApplyTeamReward(string reward, int team)
    {
        var players = TeamPlayerIds(team).Select(RequirePlayer).ToArray();
        switch (reward)
        {
            case "80% Off Next Faction":
                foreach (var player in players)
                    player.NextFactionDiscountPct = 80;
                break;
            case "80% Off Next Chosen Faction":
                foreach (var player in players)
                    player.NextChosenFactionDiscountPct = 80;
                break;
            case "80% Off Next Perm Move":
                foreach (var player in players)
                {
                    player.NextPermMoveDiscountPct = 80;
                    player.PermMoveCapUnlocked = true;
                }
                break;
            case "Sellback +15%":
                if (team == 1)
                    RedPermanentSellbackBonusPct += 15;
                else
                    BluePermanentSellbackBonusPct += 15;
                break;
            case "10% Off Next Income":
                foreach (var player in players)
                    player.NextIncomeDiscountPct = 10;
                break;
            case "+30% Next Sell":
                foreach (var player in players)
                    player.NextSellBonusPct = 30;
                break;
            case "-5% BFT Surcharge":
                if (team == 1)
                    RedBftSurchargePct = Math.Max(0, RedBftSurchargePct - 5);
                else
                    BlueBftSurchargePct = Math.Max(0, BlueBftSurchargePct - 5);
                break;
        }
    }

    private void ApplyIncomeDecay()
    {
        foreach (var player in Players)
        {
            if (!IsIncomeAvailable)
            {
                player.IncomeMissedRounds = 0;
                player.IncomeDecayPct = 0;
                player.IncomeCost = Math.Round(GetBaseIncomeCost() * (decimal)Math.Pow(1.24, player.IncomeUpgrades));
                continue;
            }

            if (player.BoughtIncomeThisRound)
            {
                player.IncomeMissedRounds = 0;
                player.IncomeDecayPct = 0;
            }
            else
            {
                player.IncomeMissedRounds++;
                player.IncomeDecayPct = Mode switch
                {
                    TabsMatchMode.FT13 => player.IncomeMissedRounds < 3
                        ? 0
                        : Math.Min(100, (player.IncomeMissedRounds - 2) * 10),
                    TabsMatchMode.FT20 => player.IncomeMissedRounds < 3
                        ? 0
                        : Math.Min(100, (player.IncomeMissedRounds - 2) * 6),
                    _ => player.IncomeMissedRounds < 4
                        ? 0
                        : Math.Min(100, (player.IncomeMissedRounds - 3) * 3)
                };
            }

            decimal full = GetBaseIncomeCost() * (decimal)Math.Pow(1.24, player.IncomeUpgrades);
            player.IncomeCost = player.IncomeDecayPct > 0
                ? Math.Max(1m, Math.Round(full * (1m - player.IncomeDecayPct / 100m)))
                : Math.Round(full);
        }
    }

    private void ResetRoundPurchaseFlags()
    {
        foreach (var player in Players)
        {
            player.BoughtIncomeThisRound = false;
            player.ReplayBoughtThisRound = false;
        }

        RedReplayBoughtThisRound = false;
        BlueReplayBoughtThisRound = false;
        ArmyTransactionThisRound = false;
    }

    private static void EnsureArmyUnits(PlayerState player)
    {
        if (player.ArmyUnits == null)
            player.ArmyUnits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    private MutationResult Spend(int player, int amount, string label)
    {
        var p = RequirePlayer(player);
        if (p.Gold < amount)
            return MutationResult.Fail($"{p.Name} needs {amount}g and has {p.Gold}g.");

        PushUndo();
        p.Gold -= amount;
        Log($"{p.Name} spent {amount}g on {label}.");
        return MutationResult.Ok($"{p.Name} spent {amount}g.");
    }

    private int Discounted(int baseCost, int discountPct)
    {
        return (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));
    }

    private void AnnounceMatchEndIfNeeded()
    {
        if (MatchEndAnnounced)
            return;

        int winner = 0;
        if (RedPoints >= GoalPoints && RedPoints > BluePoints)
            winner = 1;
        if (BluePoints >= GoalPoints && BluePoints > RedPoints)
            winner = 2;
        if (winner == 0)
            return;

        bool winByTwoActive = RedPoints >= GoalPoints - 1 && BluePoints >= GoalPoints - 1;
        if (winByTwoActive && Math.Abs(RedPoints - BluePoints) < 2)
            return;

        MatchEndAnnounced = true;
        Log($"Match complete: {TeamName(winner)} wins {RedPoints}-{BluePoints}.");
    }

    private void PushUndo()
    {
        UndoStack.Add(JsonSerializer.Serialize(this, TabsJson.Options));
        if (UndoStack.Count > 30)
            UndoStack.RemoveAt(0);
    }

    private void CopyFrom(TabsMatch other)
    {
        Id = other.Id;
        ChannelId = other.ChannelId;
        MessageId = other.MessageId;
        MatchmakingChannelId = other.MatchmakingChannelId;
        IsPrivateMatchChannel = other.IsPrivateMatchChannel;
        PrivateVoiceChannelId = other.PrivateVoiceChannelId;
        ChannelDeleteAfterUtc = other.ChannelDeleteAfterUtc;
        LoadedSaveName = other.LoadedSaveName;
        HostUserId = other.HostUserId;
        HostDisplayName = other.HostDisplayName;
        InvitedUserId = other.InvitedUserId;
        InvitedDisplayName = other.InvitedDisplayName;
        Player1UserId = other.Player1UserId;
        Player1DisplayName = other.Player1DisplayName;
        Player2UserId = other.Player2UserId;
        Player2DisplayName = other.Player2DisplayName;
        Player3UserId = other.Player3UserId;
        Player3DisplayName = other.Player3DisplayName;
        Player4UserId = other.Player4UserId;
        Player4DisplayName = other.Player4DisplayName;
        InviteAccepted = other.InviteAccepted;
        Format = other.Format;
        Mode = other.Mode;
        FactionModeEnabled = other.FactionModeEnabled;
        ModesLocked = other.ModesLocked;
        Round = other.Round;
        PendingWinner = other.PendingWinner;
        LastRoundWinner = other.LastRoundWinner;
        FirstTurnChosen = other.FirstTurnChosen;
        FirstTurnTeam = other.FirstTurnTeam;
        ActiveTurnTeam = other.ActiveTurnTeam;
        TurnPassesThisRound = other.TurnPassesThisRound;
        EndedTurnPlayers = new HashSet<int>(other.EndedTurnPlayers ?? new HashSet<int>());
        RedPoints = other.RedPoints;
        BluePoints = other.BluePoints;
        RedReplayBoughtThisRound = other.RedReplayBoughtThisRound;
        BlueReplayBoughtThisRound = other.BlueReplayBoughtThisRound;
        ArmyTransactionThisRound = other.ArmyTransactionThisRound;
        RedPermanentSellbackBonusPct = other.RedPermanentSellbackBonusPct;
        BluePermanentSellbackBonusPct = other.BluePermanentSellbackBonusPct;
        RedBftSurchargePct = other.RedBftSurchargePct;
        BlueBftSurchargePct = other.BlueBftSurchargePct;
        MatchEndAnnounced = other.MatchEndAnnounced;
        ForfeitedTeam = other.ForfeitedTeam;
        Players = other.Players;
        RewardQueue = other.RewardQueue;
        ClaimedMilestones = other.ClaimedMilestones;
        NextMilestoneThreshold = other.NextMilestoneThreshold;
        ActionLog = other.ActionLog;
        TimerRemainingSeconds = other.TimerRemainingSeconds;
        TimerRunning = other.TimerRunning;
        TimerEndsAtUtc = other.TimerEndsAtUtc;
        TimerExpiredNotified = other.TimerExpiredNotified;
        MatchTimerRemainingSeconds = other.MatchTimerRemainingSeconds;
        MatchTimerRunning = other.MatchTimerRunning;
        MatchTimerEndsAtUtc = other.MatchTimerEndsAtUtc;
        MatchTimerExpiredNotified = other.MatchTimerExpiredNotified;
    }
}
