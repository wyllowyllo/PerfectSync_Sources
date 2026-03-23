public static class TeamRules
{
    public const int MaxTeams = 4;
    public const int PlayersPerTeam = 2;

    public static bool IsValidAssignedTeam(int teamRaw) =>
        teamRaw >= 1 && teamRaw <= MaxTeams;
}
