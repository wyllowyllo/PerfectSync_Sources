using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 두 앵커 사이 로프 경로에서 장애물 감김점(WrapPoint)을 관리합니다.
/// 직선 경로가 장애물에 막히면 접선점을 삽입하고, 로프가 들리면 자동으로 제거합니다.
/// MonoBehaviour가 아닌 순수 C# 클래스입니다.
/// </summary>
public class WrapPointTracker
{
    private readonly List<WrapPoint> _wrapPoints = new(8);
    private readonly LayerMask _obstacleLayer;
    private readonly float _wrapRadius;

    // 풀림 판정 히스테리시스 (라디안)
    private const float UnwrapThresholdRad = 5f * Mathf.Deg2Rad;

    // Linecast 버퍼
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

    // 외부에서 감김 변경을 감지하기 위한 버전 카운터
    private int _version;
    private int _lastReportedVersion;

    /// <summary>
    /// 앵커 포함 전체 경로점 배열 (AnchorA, WP0, WP1, ..., AnchorB)
    /// </summary>
    public Vector3[] Waypoints { get; private set; } = Array.Empty<Vector3>();

    /// <summary>
    /// 감긴 구간의 총 길이 (앵커 사이에서 감김점이 차지하는 거리)
    /// </summary>
    public float WrappedLength { get; private set; }

    /// <summary>
    /// 현재 감김점 개수
    /// </summary>
    public int WrapCount => _wrapPoints.Count;

    /// <summary>
    /// 감김 상태가 변경되었는지 여부. 읽으면 자동으로 리셋됩니다.
    /// </summary>
    public bool ConsumeChanged()
    {
        if (_version == _lastReportedVersion) return false;
        _lastReportedVersion = _version;
        return true;
    }

    /// <summary>
    /// 새 감김점이 생성되었을 때 호출되는 콜백 (위치 전달)
    /// </summary>
    public event Action<Vector3> OnWrapAdded;

    public WrapPointTracker(LayerMask obstacleLayer, float wrapRadius = 0.2f)
    {
        _obstacleLayer = obstacleLayer;
        _wrapRadius = wrapRadius;
    }

    public void Reset()
    {
        _wrapPoints.Clear();
        Waypoints = Array.Empty<Vector3>();
        WrappedLength = 0f;
        _version++;
    }

    /// <summary>
    /// 매 FixedUpdate마다 호출. 감김/풀림을 판정하고 Waypoints를 갱신합니다.
    /// </summary>
    public void Update(Vector3 anchorA, Vector3 anchorB)
    {
        DetectUnwraps(anchorA, anchorB);
        DetectNewWraps(anchorA, anchorB);
        RebuildWaypoints(anchorA, anchorB);
    }

    /// <summary>
    /// 기존 감김점 중 로프가 들린(각도 반전) 것을 제거합니다.
    /// 뒤에서부터 순회하여 인덱스 시프트 문제를 방지합니다.
    /// </summary>
    private void DetectUnwraps(Vector3 anchorA, Vector3 anchorB)
    {
        for (int i = _wrapPoints.Count - 1; i >= 0; i--)
        {
            var wp = _wrapPoints[i];

            // 장애물이 파괴된 경우 즉시 제거
            if (wp.Obstacle == null)
            {
                _wrapPoints.RemoveAt(i);
                _version++;
                continue;
            }

            Vector3 prev = (i > 0) ? _wrapPoints[i - 1].Position : anchorA;
            Vector3 next = (i < _wrapPoints.Count - 1) ? _wrapPoints[i + 1].Position : anchorB;

            // 감김점에서 이웃 두 점이 이루는 방향의 외적 (XZ 평면)
            Vector3 toPrev = prev - wp.Position;
            Vector3 toNext = next - wp.Position;
            float cross = toPrev.x * toNext.z - toPrev.z * toNext.x;
            int currentSide = cross > 0 ? 1 : -1;

            // 감김 방향이 반전되면 풀림
            if (currentSide != wp.WindingSide)
            {
                // 히스테리시스: 각도가 충분히 반전되어야 풀림 확정
                float angle = Mathf.Atan2(Mathf.Abs(cross), Vector3.Dot(toPrev, toNext));
                if (angle > UnwrapThresholdRad)
                {
                    _wrapPoints.RemoveAt(i);
                    _version++;
                }
            }
        }
    }

    /// <summary>
    /// 연속된 경로점 쌍 사이를 Linecast하여 장애물이 가로막으면 접선점을 삽입합니다.
    /// 한 프레임에 최대 1개의 새 감김점만 추가합니다 (안정성).
    /// </summary>
    private void DetectNewWraps(Vector3 anchorA, Vector3 anchorB)
    {
        int segmentCount = _wrapPoints.Count + 1;

        for (int seg = 0; seg < segmentCount; seg++)
        {
            Vector3 from = (seg == 0) ? anchorA : _wrapPoints[seg - 1].Position;
            Vector3 to = (seg == _wrapPoints.Count) ? anchorB : _wrapPoints[seg].Position;

            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance < 0.01f) continue;

            // 경로 구간 사이 Linecast
            if (!Physics.Linecast(from, to, out RaycastHit hit, _obstacleLayer)) continue;

            // 접선점 계산
            Vector3 tangentPoint = ComputeTangentPoint(hit.collider, from, to);
            if (tangentPoint == Vector3.zero) continue;

            // 감김 방향 결정
            Vector3 obstacleCenter = GetColliderCenterXZ(hit.collider);
            int windingSide = CCW_XZ(from, to, obstacleCenter);

            var newWrap = new WrapPoint(tangentPoint, hit.collider, windingSide);

            // 이미 같은 장애물에 감겨있는지 확인
            if (IsAlreadyWrapped(hit.collider)) continue;

            // 삽입 위치: 현재 세그먼트의 시작점 뒤
            _wrapPoints.Insert(seg, newWrap);
            _version++;
            OnWrapAdded?.Invoke(tangentPoint);

            // 한 프레임에 하나만 추가 (다음 프레임에 후속 감김 처리)
            return;
        }
    }

    /// <summary>
    /// 장애물의 형태에 따라 로프가 접할 접선점을 계산합니다.
    /// XZ 평면에 투영하여 2D 접선을 구한 뒤, 장애물의 Y 높이에 맞춰 복원합니다.
    /// </summary>
    private Vector3 ComputeTangentPoint(Collider collider, Vector3 from, Vector3 to)
    {
        Vector3 center = GetColliderCenterXZ(collider);
        float radius = GetColliderRadiusXZ(collider);

        if (radius <= 0f)
        {
            // Box 등 비원형 콜라이더: 가장 가까운 표면점 사용
            Vector3 midpoint = (from + to) * 0.5f;
            Vector3 closest = collider.ClosestPoint(midpoint);
            Vector3 normal = (closest - center).normalized;
            return closest + normal * _wrapRadius;
        }

        // 원형 콜라이더 (Capsule, Sphere, Cylinder): XZ 접선 계산
        float effectiveRadius = radius + _wrapRadius;

        // from에서 장애물 중심까지의 거리
        Vector2 fromXZ = new Vector2(from.x, from.z);
        Vector2 centerXZ = new Vector2(center.x, center.z);
        Vector2 toXZ = new Vector2(to.x, to.z);

        Vector2 delta = centerXZ - fromXZ;
        float dist = delta.magnitude;

        if (dist <= effectiveRadius)
        {
            // from이 장애물 내부에 있는 경우: ClosestPoint 폴백
            Vector3 closest = collider.ClosestPoint(from);
            Vector3 normal = (closest - center).normalized;
            return closest + normal * _wrapRadius;
        }

        // 접선각 계산
        float sinTheta = effectiveRadius / dist;
        sinTheta = Mathf.Clamp(sinTheta, -1f, 1f);
        float theta = Mathf.Asin(sinTheta);

        // 방향 결정: from→to 기준 장애물이 어느 쪽인지
        int side = CCW_XZ(from, to, center);

        // 접선 방향 회전 (side에 따라 시계/반시계)
        float baseAngle = Mathf.Atan2(delta.y, delta.x);
        float tangentAngle = baseAngle + (side > 0 ? -theta : theta);

        Vector2 tangentXZ = centerXZ + new Vector2(
            -Mathf.Cos(tangentAngle) * effectiveRadius,
            -Mathf.Sin(tangentAngle) * effectiveRadius
        );

        // 실제 접선점은 중심에서 effectiveRadius 거리
        Vector2 tangentDir = (tangentXZ - centerXZ).normalized;
        Vector2 finalXZ = centerXZ + tangentDir * effectiveRadius;

        // Y 좌표: from과 to의 보간 (경로상 비율로)
        float t = Vector2.Distance(fromXZ, finalXZ) / Vector2.Distance(fromXZ, toXZ);
        t = Mathf.Clamp01(t);
        float y = Mathf.Lerp(from.y, to.y, t);

        return new Vector3(finalXZ.x, y, finalXZ.y);
    }

    /// <summary>
    /// Waypoints 배열과 WrappedLength를 재구성합니다.
    /// </summary>
    private void RebuildWaypoints(Vector3 anchorA, Vector3 anchorB)
    {
        int totalPoints = _wrapPoints.Count + 2;

        if (Waypoints == null || Waypoints.Length != totalPoints)
            Waypoints = new Vector3[totalPoints];

        Waypoints[0] = anchorA;
        for (int i = 0; i < _wrapPoints.Count; i++)
        {
            Waypoints[i + 1] = _wrapPoints[i].Position;
        }
        Waypoints[totalPoints - 1] = anchorB;

        // 감긴 구간 길이 = 연속 감김점들 사이의 거리 합
        WrappedLength = 0f;
        if (_wrapPoints.Count >= 1)
        {
            // AnchorA ~ 첫 감김점
            WrappedLength += Vector3.Distance(anchorA, _wrapPoints[0].Position);

            // 감김점 간 거리
            for (int i = 0; i < _wrapPoints.Count - 1; i++)
            {
                WrappedLength += Vector3.Distance(_wrapPoints[i].Position, _wrapPoints[i + 1].Position);
            }

            // 마지막 감김점 ~ AnchorB
            WrappedLength += Vector3.Distance(_wrapPoints[_wrapPoints.Count - 1].Position, anchorB);
        }
    }

    private bool IsAlreadyWrapped(Collider collider)
    {
        for (int i = 0; i < _wrapPoints.Count; i++)
        {
            if (_wrapPoints[i].Obstacle == collider) return true;
        }
        return false;
    }

    /// <summary>
    /// 콜라이더의 XZ 평면 투영 반지름을 반환합니다.
    /// Box 등 비원형은 0을 반환하여 ClosestPoint 폴백을 사용합니다.
    /// </summary>
    private static float GetColliderRadiusXZ(Collider collider)
    {
        switch (collider)
        {
            case SphereCollider sphere:
            {
                Vector3 scale = sphere.transform.lossyScale;
                return sphere.radius * Mathf.Max(scale.x, scale.z);
            }
            case CapsuleCollider capsule:
            {
                Vector3 scale = capsule.transform.lossyScale;
                return capsule.direction switch
                {
                    0 => capsule.radius * Mathf.Max(scale.y, scale.z), // X축
                    1 => capsule.radius * Mathf.Max(scale.x, scale.z), // Y축 (수직 캡슐)
                    2 => capsule.radius * Mathf.Max(scale.x, scale.y), // Z축
                    _ => capsule.radius * scale.x
                };
            }
            default:
                return 0f; // Box, Mesh 등은 비원형으로 처리
        }
    }

    private static Vector3 GetColliderCenterXZ(Collider collider)
    {
        return collider.bounds.center;
    }

    private static int CCW_XZ(Vector3 a, Vector3 b, Vector3 c)
    {
        float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        return cross > 0 ? 1 : -1;
    }
}
