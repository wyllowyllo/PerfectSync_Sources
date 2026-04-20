using Photon.Pun;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RaceProgressTracker : MonoBehaviour
{
    public int TeamNumber { get; private set; } = PhotonTeamManager.TeamNone;
    public int CheckpointsPassed { get; private set; }
    public float Progress { get; private set; }

    public void SetTeam(int teamNumber)
    {
        TeamNumber = teamNumber;
    }

    private void OnEnable()
    {
        if (RaceRankingManager.Instance != null)
            RaceRankingManager.Instance.RegisterTracker(this);

        if (TeamNumber != PhotonTeamManager.TeamNone) return;
        if (!PhotonNetwork.InRoom || InGameManager.Instance == null) return;

        var pv = GetComponentInParent<Photon.Pun.PhotonView>();
        if (pv != null && pv.Owner != null)
            SetTeam(PhotonTeamManager.GetTeamRaw(pv.Owner));
        else
            SetTeam(PhotonTeamManager.GetLocalTeamRaw());
    }

    private void OnDisable()
    {
        if (RaceRankingManager.Instance != null)
            RaceRankingManager.Instance.UnregisterTracker(this);
    }

    public void PassCheckpoint(int index)
    {
        if (index <= CheckpointsPassed) return;

        CheckpointsPassed = index;
    }

    private void Update()
    {
        if (RaceRankingManager.Instance == null) return;

        var allSegments = RaceRankingManager.Instance.RaceSegments;
        if (allSegments == null || allSegments.Length == 0)
        {
            Progress = CheckpointsPassed;
            return;
        }

        float bestT = 0f;
        float bestDist = float.MaxValue;
        RaceSegment bestSegment = null;

        float3 worldPos = transform.position;

        foreach (var segment in allSegments)
        {
            if (segment == null) continue;

            var container = segment.SplineContainer;
            if (container == null || container.Spline == null) continue;

            float3 localPos = container.transform.InverseTransformPoint(worldPos);

            SplineUtility.GetNearestPoint(
                container.Spline,
                localPos,
                out _,
                out float t
            );

            float3 splineLocalPoint = SplineUtility.EvaluatePosition(container.Spline, t);
            float3 splineWorldPoint = container.transform.TransformPoint(splineLocalPoint);
            float dist = math.distance(worldPos, splineWorldPoint);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestT = t;
                bestSegment = segment;
            }
        }

        if (bestSegment == null)
        {
            Progress = CheckpointsPassed;
            return;
        }

        Progress = RaceRankingManager.Instance.GetDistanceAlongRace(bestSegment, bestT);
    }
}
