using System;
using System.Collections.Generic;
using UnityEngine;

public class RaceRankingManager : SingletonMonoBehaviour<RaceRankingManager>
{
    protected override bool PersistAcrossScenes => false;

    [SerializeField] private RaceSegment[] _segments;

    private Dictionary<int, List<RaceSegment>> _segmentsByFrom;
    private int _lastCheckpointIndex;
    private List<RaceProgressTracker> _trackers = new();
    private List<TeamRankEntry> _currentRankings = new();
    private List<int> _finishOrder = new();

    private readonly Dictionary<int, float> _calcTeamBest = new(PhotonTeamManager.MaxTeams);
    private readonly Dictionary<int, int> _calcTeamMaxCheckpoint = new(PhotonTeamManager.MaxTeams);
    private readonly List<RaceFinishEvent> _calcNewFinishes = new(PhotonTeamManager.MaxTeams);
    private readonly List<(int team, float progress)> _calcNotFinished = new(PhotonTeamManager.MaxTeams);

    public IReadOnlyList<TeamRankEntry> CurrentRankings => _currentRankings;

    public int LastCheckpointIndex => _lastCheckpointIndex;

    public int FinishedTeamCount => _finishOrder.Count;

    public IReadOnlyList<int> FinishedTeamsInOrder => _finishOrder;

    public event Action<IReadOnlyList<TeamRankEntry>> OnRankingsUpdated;
    public static event Action<int> OnFirstPlaceFinished;
    public static event Action<int, int> OnTeamFinished;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        BuildSegmentLookup();
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

    public List<RaceTeamProgressInfo> GetAllTeamsProgress()
    {
        var agg = new Dictionary<int, (int cp, float prog)>();

        foreach (var tracker in _trackers)
        {
            int team = tracker.TeamNumber;
            if (team == PhotonTeamManager.TeamNone) continue;

            if (!agg.TryGetValue(team, out var cur))
                cur = (0, 0f);

            int cp = tracker.CheckpointsPassed > cur.cp ? tracker.CheckpointsPassed : cur.cp;
            float p = tracker.Progress > cur.prog ? tracker.Progress : cur.prog;
            agg[team] = (cp, p);
        }

        int last = _lastCheckpointIndex;
        var list = new List<RaceTeamProgressInfo>(agg.Count);
        foreach (var kv in agg)
        {
            bool finished = last >= 0 && kv.Value.cp >= last;
            list.Add(new RaceTeamProgressInfo(kv.Key, kv.Value.cp, kv.Value.prog, finished));
        }

        list.Sort((a, b) => a.TeamNumber.CompareTo(b.TeamNumber));
        return list;
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

        var edges = new List<(int from, int to)>(_segments.Length);
        foreach (var seg in _segments)
        {
            if (!_segmentsByFrom.ContainsKey(seg.FromCheckpoint))
                _segmentsByFrom[seg.FromCheckpoint] = new List<RaceSegment>();
            _segmentsByFrom[seg.FromCheckpoint].Add(seg);

            edges.Add((seg.FromCheckpoint, seg.ToCheckpoint));
        }

        _lastCheckpointIndex = RaceCourseTopology.ComputeLastCheckpointIndex(edges);
    }

    private void CalculateRankings()
    {
        _calcTeamBest.Clear();
        _calcTeamMaxCheckpoint.Clear();

        foreach (var tracker in _trackers)
        {
            int team = tracker.TeamNumber;
            if (team == PhotonTeamManager.TeamNone) continue;

            if (!_calcTeamBest.TryGetValue(team, out float best) || tracker.Progress > best)
                _calcTeamBest[team] = tracker.Progress;

            if (!_calcTeamMaxCheckpoint.TryGetValue(team, out int maxCp) || tracker.CheckpointsPassed > maxCp)
                _calcTeamMaxCheckpoint[team] = tracker.CheckpointsPassed;
        }

        int previousFinishCount = _finishOrder.Count;
        RaceRankingCalculator.Compute(
            _calcTeamBest,
            _calcTeamMaxCheckpoint,
            _lastCheckpointIndex,
            _finishOrder,
            _calcNewFinishes,
            _currentRankings,
            _calcNotFinished);

        if (_calcNewFinishes.Count > 0 && previousFinishCount == 0)
            OnFirstPlaceFinished?.Invoke(_calcNewFinishes[0].TeamNumber);

        foreach (var ev in _calcNewFinishes)
            OnTeamFinished?.Invoke(ev.TeamNumber, ev.FinishPlace);

        OnRankingsUpdated?.Invoke(_currentRankings);
    }
}