using System.Collections.Generic;

public static class RaceRankingCalculator
{
    public static RaceRankingComputationResult Compute(
        IReadOnlyDictionary<int, float> teamBest,
        IReadOnlyDictionary<int, int> teamMaxCheckpoint,
        int lastCheckpointIndex,
        IReadOnlyList<int> previousFinishOrder)
    {
        var finishOrder = new List<int>(previousFinishOrder);
        var newFinishes = new List<RaceFinishEvent>();

        if (lastCheckpointIndex >= 0)
        {
            foreach (var kvp in teamMaxCheckpoint)
            {
                int team = kvp.Key;
                if (kvp.Value < lastCheckpointIndex)
                    continue;
                if (finishOrder.Contains(team))
                    continue;

                finishOrder.Add(team);
                newFinishes.Add(new RaceFinishEvent
                {
                    TeamNumber = team,
                    FinishPlace = finishOrder.Count
                });
            }
        }

        var rankings = new List<TeamRankEntry>();
        int rank = 1;

        foreach (int team in finishOrder)
        {
            float progress = teamBest.TryGetValue(team, out float p) ? p : 0f;
            rankings.Add(new TeamRankEntry
            {
                TeamNumber = team,
                BestProgress = progress,
                Rank = rank++
            });
        }

        var notFinished = new List<(int team, float progress)>();
        foreach (var kvp in teamBest)
        {
            if (finishOrder.Contains(kvp.Key))
                continue;
            notFinished.Add((kvp.Key, kvp.Value));
        }

        notFinished.Sort((a, b) => b.progress.CompareTo(a.progress));

        foreach (var (team, progress) in notFinished)
        {
            rankings.Add(new TeamRankEntry
            {
                TeamNumber = team,
                BestProgress = progress,
                Rank = rank++
            });
        }

        return new RaceRankingComputationResult
        {
            Rankings = rankings,
            FinishOrder = finishOrder,
            NewFinishes = newFinishes
        };
    }
}
