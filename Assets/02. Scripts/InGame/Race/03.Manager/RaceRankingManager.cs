using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RaceRankingManager : SingletonMonoBehaviour<RaceRankingManager>
{
    protected override bool PersistAcrossScenes => false;

    [SerializeField] private RaceSegment[] _segments;

    private Dictionary<int, List<RaceSegment>> _segmentsByFrom;
    private Dictionary<RaceSegment, float> _raceDistanceAtSegmentStart;
    private Dictionary<RaceSegment, float> _segmentWorldLength;
    private int _distanceCacheRebuildFrame = -1;
    private int _lastCheckpointIndex;
    private List<RaceProgressTracker> _trackers = new();
    private List<TeamRankEntry> _currentRankings = new();
    private List<int> _finishOrder = new();

    /// <summary>해당 프레임 기준 팀별 진행도(같은 팀 트래커 여럿이면 그중 최댓값). 역대 최고가 아님.</summary>
    private readonly Dictionary<int, float> _calcTeamProgress = new(PhotonTeamManager.MaxTeams);
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

    private void Start()
    {
        if (Instance != this)
            return;
        RebuildRaceDistanceCaches();
    }

    public void RefreshRaceDistanceCaches()
    {
        RebuildRaceDistanceCaches();
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

    /// <summary>All configured segments (same array as topology). Used for nearest-spline progress.</summary>
    public RaceSegment[] RaceSegments => _segments;

    /// <summary>
    /// 코스 시작(인입 차수 0인 체크포인트)부터의 누적 월드 거리 + 스플라인 상 normalized t 구간의 호장 거리.
    /// </summary>
    public float GetDistanceAlongRace(RaceSegment segment, float normalizedSplineT)
    {
        if (segment == null || _raceDistanceAtSegmentStart == null)
            return 0f;

        var container = segment.SplineContainer;
        if (container == null || container.Spline == null)
            return _raceDistanceAtSegmentStart.TryGetValue(segment, out float bOnly) ? bOnly : 0f;

        MaybeRebuildRaceDistanceCachesIfSegmentStale(segment, container);

        var spline = container.Spline;
        float alongLocal = spline.ConvertIndexUnit(normalizedSplineT, PathIndexUnit.Normalized, PathIndexUnit.Distance);
        float localFull = spline.ConvertIndexUnit(1f, PathIndexUnit.Normalized, PathIndexUnit.Distance);

        if (!_segmentWorldLength.TryGetValue(segment, out float worldLen) || worldLen < 1e-4f)
            worldLen = SplineUtility.CalculateLength(spline, (float4x4)container.transform.localToWorldMatrix);

        float alongWorld = localFull > 1e-5f ? alongLocal * (worldLen / localFull) : 0f;
        float baseDist = _raceDistanceAtSegmentStart.TryGetValue(segment, out float bd) ? bd : 0f;
        return baseDist + alongWorld;
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

    private void LateUpdate()
    {
        // 트래커들이 Update에서 Progress를 갱신한 뒤 같은 프레임에 순위 반영
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
            if (seg == null) continue;

            if (!_segmentsByFrom.ContainsKey(seg.FromCheckpoint))
                _segmentsByFrom[seg.FromCheckpoint] = new List<RaceSegment>();
            _segmentsByFrom[seg.FromCheckpoint].Add(seg);

            edges.Add((seg.FromCheckpoint, seg.ToCheckpoint));
        }

        _lastCheckpointIndex = RaceCourseTopology.ComputeLastCheckpointIndex(edges);
        RebuildRaceDistanceCaches();
    }

    private void MaybeRebuildRaceDistanceCachesIfSegmentStale(RaceSegment segment, SplineContainer container)
    {
        if (_segmentWorldLength == null || _segments == null)
            return;

        float freshLen = SplineUtility.CalculateLength(container.Spline, (float4x4)container.transform.localToWorldMatrix);
        if (freshLen < 1e-4f)
            return;

        bool lenMissingOrZero = !_segmentWorldLength.TryGetValue(segment, out float cachedLen) || cachedLen < 1e-4f;
        bool startMissing = !_raceDistanceAtSegmentStart.TryGetValue(segment, out _);
        if (!lenMissingOrZero && !startMissing)
            return;

        if (_distanceCacheRebuildFrame == Time.frameCount)
            return;
        _distanceCacheRebuildFrame = Time.frameCount;
        RebuildRaceDistanceCaches();
    }

    /// <summary>
    /// 세그먼트 그래프에서 코스 입구(다른 곳에서 들어오지 않는 체크포인트)부터의 최장 누적 거리를 구해
    /// 각 세그먼트 t=0 지점의 레이스 거리를 캐시합니다. 갈림길이 합쳐지는 지점은 더 긴 쪽 경로 기준입니다.
    /// </summary>
    private void RebuildRaceDistanceCaches()
    {
        _raceDistanceAtSegmentStart = new Dictionary<RaceSegment, float>();
        _segmentWorldLength = new Dictionary<RaceSegment, float>();

        if (_segments == null) return;

        var edgeList = new List<(RaceSegment seg, int from, int to, float len)>();
        var nodes = new HashSet<int>();

        foreach (var seg in _segments)
        {
            if (seg == null) continue;

            var c = seg.SplineContainer;
            if (c == null || c.Spline == null)
            {
                _segmentWorldLength[seg] = 0f;
                continue;
            }

            float worldLen = SplineUtility.CalculateLength(c.Spline, (float4x4)c.transform.localToWorldMatrix);
            _segmentWorldLength[seg] = worldLen;
            edgeList.Add((seg, seg.FromCheckpoint, seg.ToCheckpoint, worldLen));
            nodes.Add(seg.FromCheckpoint);
            nodes.Add(seg.ToCheckpoint);
        }

        if (edgeList.Count == 0) return;

        var appearsAsTo = new HashSet<int>();
        foreach (var e in edgeList)
            appearsAsTo.Add(e.to);

        var startNodes = new List<int>();
        foreach (var n in nodes)
        {
            if (!appearsAsTo.Contains(n))
                startNodes.Add(n);
        }

        if (startNodes.Count == 0)
        {
            int minN = int.MaxValue;
            foreach (var n in nodes)
            {
                if (n < minN) minN = n;
            }

            startNodes.Add(minN);
        }

        var bestDist = new Dictionary<int, float>();
        foreach (var n in nodes)
            bestDist[n] = float.NegativeInfinity;

        foreach (var s in startNodes)
            bestDist[s] = 0f;

        int vCount = nodes.Count;
        int relaxPasses = Mathf.Max(vCount, 1);
        for (int i = 0; i < relaxPasses; i++)
        {
            foreach (var (seg, from, to, len) in edgeList)
            {
                if (bestDist[from] <= float.NegativeInfinity * 0.5f) continue;
                float candidate = bestDist[from] + len;
                if (candidate > bestDist[to])
                    bestDist[to] = candidate;
            }
        }

        foreach (var (seg, from, to, len) in edgeList)
        {
            float startD = bestDist.TryGetValue(from, out float d) && d > float.NegativeInfinity * 0.5f ? d : 0f;
            _raceDistanceAtSegmentStart[seg] = startD;
        }
    }

    private void CalculateRankings()
    {
        _calcTeamProgress.Clear();
        _calcTeamMaxCheckpoint.Clear();

        foreach (var tracker in _trackers)
        {
            int team = tracker.TeamNumber;
            if (team == PhotonTeamManager.TeamNone) continue;

            if (!_calcTeamProgress.TryGetValue(team, out float lead) || tracker.Progress > lead)
                _calcTeamProgress[team] = tracker.Progress;

            if (!_calcTeamMaxCheckpoint.TryGetValue(team, out int maxCp) || tracker.CheckpointsPassed > maxCp)
                _calcTeamMaxCheckpoint[team] = tracker.CheckpointsPassed;
        }

        int previousFinishCount = _finishOrder.Count;
        RaceRankingCalculator.Compute(
            _calcTeamProgress,
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