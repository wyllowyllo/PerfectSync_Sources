using System.Collections.Generic;

public static class RaceFinalRankingStore
{
    private static int[] _ranksByTeamIndex;

    public static void Store(int[] ranksByTeamIndex)
    {
        _ranksByTeamIndex = ranksByTeamIndex;
    }

    public static void Clear()
    {
        _ranksByTeamIndex = null;
    }

    public static bool HasData => _ranksByTeamIndex != null && _ranksByTeamIndex.Length > 0;

    public static bool TryGetRank(int teamNumber, out int rank)
    {
        rank = 0;
        if (_ranksByTeamIndex == null) return false;
        int idx = teamNumber - 1;
        if (idx < 0 || idx >= _ranksByTeamIndex.Length) return false;
        int r = _ranksByTeamIndex[idx];
        if (r <= 0) return false;
        rank = r;
        return true;
    }

    public static List<TeamRankEntry> GetAllOrderedByRank()
    {
        var list = new List<TeamRankEntry>();
        if (_ranksByTeamIndex == null) return list;

        for (int i = 0; i < _ranksByTeamIndex.Length; i++)
        {
            int rank = _ranksByTeamIndex[i];
            if (rank <= 0) continue;
            list.Add(new TeamRankEntry
            {
                TeamNumber = i + 1,
                Rank = rank,
                BestProgress = 0f
            });
        }
        list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        return list;
    }
}