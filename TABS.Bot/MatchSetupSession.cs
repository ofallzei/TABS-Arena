namespace Tabs.Bot;

public sealed class MatchSetupSession
{
    public ulong ChannelId { get; set; }
    public ulong MatchmakingChannelId { get; set; }
    public bool UsesPrivateMatchChannel { get; set; }
    public ulong HostUserId { get; set; }
    public string HostDisplayName { get; set; } = "";
    public ulong InvitedUserId { get; set; }
    public string InvitedDisplayName { get; set; } = "";
    public ulong Player2UserId { get; set; }
    public string Player2DisplayName { get; set; } = "";
    public ulong Player3UserId { get; set; }
    public string Player3DisplayName { get; set; } = "";
    public ulong Player4UserId { get; set; }
    public string Player4DisplayName { get; set; } = "";
    public TabsMatchFormat Format { get; set; } = TabsMatchFormat.OneVOne;
    public TabsMatchMode Mode { get; set; } = TabsMatchMode.FT20;
    public bool FactionMode { get; set; } = true;

    public bool HasRequiredPlayers =>
        Format == TabsMatchFormat.OneVOne
            ? Player2UserId != 0
            : Player2UserId != 0 && Player3UserId != 0 && Player4UserId != 0;
}

public sealed class PendingInvite
{
    public ulong ChannelId { get; set; }
    public ulong MatchmakingChannelId { get; set; }
    public bool UsesPrivateMatchChannel { get; set; }
    public ulong HostUserId { get; set; }
    public string HostDisplayName { get; set; } = "";
    public ulong InvitedUserId { get; set; }
    public string InvitedDisplayName { get; set; } = "";
    public ulong Player2UserId { get; set; }
    public string Player2DisplayName { get; set; } = "";
    public ulong Player3UserId { get; set; }
    public string Player3DisplayName { get; set; } = "";
    public ulong Player4UserId { get; set; }
    public string Player4DisplayName { get; set; } = "";
    public HashSet<ulong> AcceptedUserIds { get; set; } = new();
    public Dictionary<ulong, int> TeamAssignments { get; set; } = new();
    public TabsMatchFormat Format { get; set; }
    public TabsMatchMode Mode { get; set; }
    public bool FactionMode { get; set; }

    public IReadOnlyList<ulong> RequiredInviteUserIds =>
        Format == TabsMatchFormat.OneVOne
            ? new[] { Player2UserId }
            : new[] { Player2UserId, Player3UserId, Player4UserId };

    public bool IsFullyAccepted => RequiredInviteUserIds.All(id => id != 0 && AcceptedUserIds.Contains(id));

    public IReadOnlyList<ulong> AllParticipantUserIds =>
        Format == TabsMatchFormat.OneVOne
            ? new[] { HostUserId, Player2UserId }
            : new[] { HostUserId, Player2UserId, Player3UserId, Player4UserId };

    public void EnsureTeamAssignments()
    {
        if (Format != TabsMatchFormat.TwoVTwo || TeamAssignments.Count > 0)
            return;

        TeamAssignments[HostUserId] = 1;
        TeamAssignments[Player2UserId] = 1;
        TeamAssignments[Player3UserId] = 2;
        TeamAssignments[Player4UserId] = 2;
    }

    public int TeamOf(ulong userId)
    {
        EnsureTeamAssignments();
        return TeamAssignments.TryGetValue(userId, out int team) ? team : 0;
    }

    public void ToggleTeam(ulong userId)
    {
        EnsureTeamAssignments();
        TeamAssignments[userId] = TeamOf(userId) == 1 ? 2 : 1;
    }

    public string DisplayNameFor(ulong userId)
    {
        if (userId == HostUserId)
            return HostDisplayName;
        if (userId == Player2UserId)
            return Player2DisplayName;
        if (userId == Player3UserId)
            return Player3DisplayName;
        if (userId == Player4UserId)
            return Player4DisplayName;
        return $"Player {userId}";
    }
}
