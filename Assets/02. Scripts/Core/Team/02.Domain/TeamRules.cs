public static class TeamRules
{
    public const int MaxTeams = 4;
    public const int PlayersPerTeam = 2;

    public const int SlotNone = 0;
    public const int SlotHost = 1;
    public const int SlotGuest = 2;

    public static bool IsValidAssignedTeam(int teamRaw) =>
        teamRaw >= 1 && teamRaw <= MaxTeams;

    public static bool IsValidSlot(int slot) =>
        slot >= SlotHost && slot <= PlayersPerTeam;
}
