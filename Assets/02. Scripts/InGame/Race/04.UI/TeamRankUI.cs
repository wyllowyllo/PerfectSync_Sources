using UnityEngine;
using TMPro;

public class TeamRankUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _rankText;

    private int _lastRank = -1;

    private void Start()
    {
        if (RaceRankingManager.Instance != null)
        {
            RaceRankingManager.Instance.OnRankingsUpdated += HandleRankingsUpdated;
            HandleRankingsUpdated(RaceRankingManager.Instance.CurrentRankings);
        }
    }

    private void OnDestroy()
    {
        if (RaceRankingManager.Instance != null)
            RaceRankingManager.Instance.OnRankingsUpdated -= HandleRankingsUpdated;
    }

    private void HandleRankingsUpdated(System.Collections.Generic.IReadOnlyList<TeamRankEntry> rankings)
    {
        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        if (myTeam == PhotonTeamManager.TeamNone) return;

        foreach (var entry in rankings)
        {
            if (entry.TeamNumber != myTeam) continue;

            if (entry.Rank == _lastRank) return;
            _lastRank = entry.Rank;

            if (_rankText != null)
                _rankText.text = RankToOrdinal(entry.Rank);
            return;
        }

        _lastRank = -1;
        _rankText.text = "";
    }

    private static string RankToOrdinal(int rank)
    {
        return rank switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            4 => "4th",
            _ => rank.ToString()
        };
    }
}
