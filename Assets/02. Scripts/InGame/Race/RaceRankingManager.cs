using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct TeamRankEntry
{
    public int TeamNumber;
    public float BestProgress;
    public int Rank;
}

public class RaceRankingManager : MonoBehaviour
{
    public static RaceRankingManager Instance { get; private set; }

    [SerializeField] private RaceSegment[] _segments;

    [Header("Debug")]
    [SerializeField] private bool _rankingDebugLog;
    [SerializeField] private int _debugLogIntervalFrames = 60;

    private Dictionary<int, List<RaceSegment>> _segmentsByFrom;
    private int _lastCheckpointIndex;
    private List<RaceProgressTracker> _trackers = new();
    private List<TeamRankEntry> _currentRankings = new();
    private List<int> _finishOrder = new();

    public IReadOnlyList<TeamRankEntry> CurrentRankings => _currentRankings;
    public event Action<IReadOnlyList<TeamRankEntry>> OnRankingsUpdated;
    public static event Action<int> OnFirstPlaceFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildSegmentLookup();

        if (_rankingDebugLog)
        {
            int segCount = _segments != null ? _segments.Length : 0;
            Debug.Log($"[RaceRankingManager] Awake: segments={segCount}, lastCheckpointIndex={_lastCheckpointIndex}, " +
                      $"fromKeys={(_segmentsByFrom != null ? _segmentsByFrom.Count : 0)}");
        }
    }

    public void RegisterTracker(RaceProgressTracker tracker)
    {
        if (!_trackers.Contains(tracker))
            _trackers.Add(tracker);
    }

    public void UnregisterTracker(RaceProgressTracker tracker)
    {
        _trackers.Remove(tracker);
    }

    public List<RaceSegment> GetSegments(int fromCheckpoint)
    {
        if (_segmentsByFrom != null && _segmentsByFrom.TryGetValue(fromCheckpoint, out var list))
            return list;
        return null;
    }

    private void Update()
    {
        CalculateRankings();
    }

    private void BuildSegmentLookup()
    {
        _segmentsByFrom = new Dictionary<int, List<RaceSegment>>();
        _lastCheckpointIndex = -1;

        if (_segments == null) return;

        foreach (var seg in _segments)
        {
            if (!_segmentsByFrom.ContainsKey(seg.FromCheckpoint))
                _segmentsByFrom[seg.FromCheckpoint] = new List<RaceSegment>();
            _segmentsByFrom[seg.FromCheckpoint].Add(seg);

            if (seg.ToCheckpoint > _lastCheckpointIndex)
                _lastCheckpointIndex = seg.ToCheckpoint;
        }
    }

    private void CalculateRankings()
    {
        var teamBest = new Dictionary<int, float>();
        var teamMaxCheckpoint = new Dictionary<int, int>();

        foreach (var tracker in _trackers)
        {
            int team = tracker.TeamNumber;
            if (team == PhotonTeamManager.TeamNone) continue;

            if (!teamBest.ContainsKey(team) || tracker.Progress > teamBest[team])
                teamBest[team] = tracker.Progress;

            if (!teamMaxCheckpoint.ContainsKey(team) || tracker.CheckpointsPassed > teamMaxCheckpoint[team])
                teamMaxCheckpoint[team] = tracker.CheckpointsPassed;
        }

        if (_lastCheckpointIndex >= 0)
        {
            foreach (var kvp in teamMaxCheckpoint)
            {
                int team = kvp.Key;
                if (kvp.Value < _lastCheckpointIndex) continue;
                if (_finishOrder.Contains(team)) continue;

                if (_finishOrder.Count == 0)
                    OnFirstPlaceFinished?.Invoke(team);

                _finishOrder.Add(team);
            }
        }

        _currentRankings.Clear();
        int rank = 1;

        foreach (int team in _finishOrder)
        {
            float progress = teamBest.TryGetValue(team, out float p) ? p : 0f;
            _currentRankings.Add(new TeamRankEntry
            {
                TeamNumber = team,
                BestProgress = progress,
                Rank = rank++
            });
        }

        var notFinished = new List<(int team, float progress)>();
        foreach (var kvp in teamBest)
        {
            if (_finishOrder.Contains(kvp.Key)) continue;
            notFinished.Add((kvp.Key, kvp.Value));
        }

        notFinished.Sort((a, b) => b.progress.CompareTo(a.progress));

        foreach (var (team, progress) in notFinished)
        {
            _currentRankings.Add(new TeamRankEntry
            {
                TeamNumber = team,
                BestProgress = progress,
                Rank = rank++
            });
        }

        if (_rankingDebugLog && _debugLogIntervalFrames > 0 && Time.frameCount % _debugLogIntervalFrames == 0)
            LogRankingDebugSnapshot();

        OnRankingsUpdated?.Invoke(_currentRankings);
    }

    private void LogRankingDebugSnapshot()
    {
        Debug.Log($"[RaceRankingManager] trackers={_trackers.Count}, lastCP={_lastCheckpointIndex}, finishOrder={_finishOrder.Count}");

        foreach (var t in _trackers)
        {
            var segs = GetSegments(t.CheckpointsPassed);
            int segCount = segs != null ? segs.Count : 0;
            Debug.Log($"  tracker '{t.gameObject.name}' team={t.TeamNumber} progress={t.Progress:F3} cp={t.CheckpointsPassed} segmentsForFrom={segCount}");
        }

        Debug.Log($"  rankings count={_currentRankings.Count}");
        foreach (var e in _currentRankings)
            Debug.Log($"    team={e.TeamNumber} rank={e.Rank} progress={e.BestProgress:F3}");

        int myTeam = InGameManager.GetLocalPlayerTeam();
        Debug.Log($"  localPlayerTeam={myTeam}");
    }
}
