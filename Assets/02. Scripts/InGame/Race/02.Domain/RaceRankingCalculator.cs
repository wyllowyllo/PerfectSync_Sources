using System;
using System.Collections.Generic;

public static class RaceRankingCalculator
{
    /// <summary>미완주 팀은 Progress(실시간 코스 거리) 내림차순. 같은 스플라인이면 길이·t에 의해 같은 스칼라로 비교됨.</summary>
    private static readonly Comparison<(int team, float progress)> NotFinishedByProgressDesc =
        (a, b) => b.progress.CompareTo(a.progress);
        
    public static void Compute(
        IReadOnlyDictionary<int, float> teamProgress,
        IReadOnlyDictionary<int, int> teamMaxCheckpoint,
        int lastCheckpointIndex,
        List<int> finishOrder,
        List<RaceFinishEvent> newFinishes,
        List<TeamRankEntry> rankings,
        List<(int team, float progress)> notFinishedBuffer)
    {
        newFinishes.Clear();
        rankings.Clear();

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

        int rank = 1;

        foreach (int team in finishOrder)
        {
            float progress = teamProgress.TryGetValue(team, out float p) ? p : 0f;
            rankings.Add(new TeamRankEntry
            {
                TeamNumber = team,
                BestProgress = progress,
                Rank = rank++
            });
        }

        notFinishedBuffer.Clear();
        foreach (var kvp in teamProgress)
        {
            if (finishOrder.Contains(kvp.Key))
                continue;
            notFinishedBuffer.Add((kvp.Key, kvp.Value));
        }

        notFinishedBuffer.Sort(NotFinishedByProgressDesc);

        foreach (var (team, progress) in notFinishedBuffer)
        {
            rankings.Add(new TeamRankEntry
            {
                TeamNumber = team,
                BestProgress = progress,
                Rank = rank++
            });
        }
    }
}