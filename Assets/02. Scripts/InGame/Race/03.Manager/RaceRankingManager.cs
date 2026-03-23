using System;
using System.Collections.Generic;
using UnityEngine;

public class RaceRankingManager : SingletonMonoBehaviour<RaceRankingManager>
{
    protected override bool PersistAcrossScenes => false;

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

    /// <summary>코스 상 마지막 체크포인트 인덱스(이 값 이상이면 완주로 간주).</summary>
    public int LastCheckpointIndex => _lastCheckpointIndex;

    /// <summary>완주 확정된 팀 수(결승 처리된 팀).</summary>
    public int FinishedTeamCount => _finishOrder.Count;

    /// <summary>완주 순서대로 팀 번호(1등, 2등, …).</summary>
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

    /// <summary>
    /// 등록된 트래커 기준 팀별 최대 통과 CP·최고 진행도·완주 여부.
    /// (트래커가 여러 개인 팀은 CP/Progress 각각 최댓값)
    /// </summary>
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

        int previousFinishCount = _finishOrder.Count;
        var result = RaceRankingCalculator.Compute(
            teamBest,
            teamMaxCheckpoint,
            _lastCheckpointIndex,
            _finishOrder);

        _finishOrder = result.FinishOrder;

        if (result.NewFinishes.Count > 0 && previousFinishCount == 0)
            OnFirstPlaceFinished?.Invoke(result.NewFinishes[0].TeamNumber);

        foreach (var ev in result.NewFinishes)
            OnTeamFinished?.Invoke(ev.TeamNumber, ev.FinishPlace);

        _currentRankings.Clear();
        _currentRankings.AddRange(result.Rankings);

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

        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        Debug.Log($"  localPlayerTeam={myTeam}");
    }
}
