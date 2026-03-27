using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 베를레 적분을 사용하여 로프의 물리적인 움직임을 시뮬레이션합니다.
/// </summary>
public class VerletSimulator : IRopePhysics
{
    private readonly VerletNode[] _nodes;
    private readonly float _nodeDistance;
    private readonly int _constraintIterations;
    private readonly LayerMask _obstacleLayer;
    private readonly LayerMask _dynamicObstacleLayer;
    private readonly SerializableDictionary<LayerMask, float> _frictionMap;
    
    private readonly HashSet<Collider> _overlappingColliders = new();
    public HashSet<Collider> OverlappingColliders => _overlappingColliders;
    
    private readonly float _nodeRadius;
    private readonly float _collisionMargin;
    private readonly int _collisionStride;
    private readonly Collider[] _overlapBuffer = new Collider[8];
    private static readonly Vector3 Gravity = new Vector3(0, -9.81f, 0);

    // 감김점 경로가 설정되어 있는지 여부
    private bool _hasWrapWaypoints;
    // 현재 적용 중인 감김 경로점 배열 (앵커 포함)
    private Vector3[] _wrapWaypoints;
    
    /// <summary>
    /// 물리 시뮬레이터를 초기화하고 노드 배열을 사전 할당합니다.
    /// </summary>
    /// <param name="nodeCount">로프를 구성하는 점의 총 개수</param>
    /// <param name="totalLength">로프의 안정적인 기본 길이</param>
    /// <param name="constraintIterations">물리 제약 조건 반복 횟수</param>
    /// <param name="startPosition">생성 시 시작 좌표 (이후 일직선으로 배치됨)</param>
    /// <param name="obstacleLayer">충돌을 감지할 장애물의 레이어 마스크</param>
    /// <param name="dynamicObstacleLayer">충돌을 감지할 움직이는 장애물의 레이어 마스크</param>
    /// <exception cref="ArgumentOutOfRangeException">노드 개수나 길이가 유효하지 않을 때 발생</exception>
    public VerletSimulator(
        int nodeCount, float totalLength, int constraintIterations, Vector3 startPosition,
        LayerMask obstacleLayer, LayerMask dynamicObstacleLayer,
        SerializableDictionary<LayerMask, float> frictionMap,
        float nodeRadius = 0.15f, float collisionMargin = 0.025f, int collisionStride = 2)
    {
        if (nodeCount < 2)
            throw new ArgumentOutOfRangeException(nameof(nodeCount), "로프를 구성하기 위해 노드는 최소 2개 이상 필요합니다.");
        if (totalLength <= 0f)
            throw new ArgumentOutOfRangeException(nameof(totalLength), "로프의 길이는 0보다 커야 합니다.");
        if (constraintIterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(constraintIterations), "반복 횟수는 최소 1 이상이어야 합니다.");

        _nodes = new VerletNode[nodeCount];
        _nodeDistance = totalLength / (nodeCount - 1);
        _constraintIterations = constraintIterations;
        _obstacleLayer = obstacleLayer;
        _dynamicObstacleLayer = dynamicObstacleLayer;
        _frictionMap = frictionMap;
        _nodeRadius = nodeRadius;
        _collisionMargin = collisionMargin;
        _collisionStride = Mathf.Max(1, collisionStride);

        for (int i = 0; i < nodeCount; i++)
        {
            Vector3 position = startPosition + Vector3.right * (_nodeDistance * i);
            _nodes[i] = new VerletNode(position);
        }
    }
    
    public void Simulate(float deltaTime)
    {
        _overlappingColliders.Clear();
        
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].IsTouchingObstacle = false;
        }
        
        ApplyVerletIntegrator(deltaTime);
        
        for (int i = 0; i < _constraintIterations; i++)
        {
            ApplyDistanceConstraints(reverse: i % 2 == 1);
            ApplyCollisionConstraints();
        }
    }

    /// <summary>
    /// 베를레 적분 연산
    /// </summary>
    private void ApplyVerletIntegrator(float deltaTime)
    {
        const float Damping = 0.98f;
        var gravity = Gravity * (deltaTime * deltaTime);
        
        for (int i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i].IsPinned) continue;

            // (현재 위치 = 현재 + (현재 - 이전))
            var velocity = _nodes[i].CurrentPosition - _nodes[i].PreviousPosition;
            _nodes[i].PreviousPosition = _nodes[i].CurrentPosition;
            _nodes[i].CurrentPosition += velocity * Damping;
            
            // 중력 적용 = (가속도 * dt * dt)
            _nodes[i].CurrentPosition += gravity;
        }
    }

    /// <summary>
    /// 탄성을 계산하여 거리 유지. 교대 방향 순회로 편향을 제거합니다.
    /// </summary>
    private void ApplyDistanceConstraints(bool reverse)
    {
        int start = reverse ? _nodes.Length - 2 : 0;
        int end = reverse ? -1 : _nodes.Length - 1;
        int step = reverse ? -1 : 1;

        for (int i = start; i != end; i += step)
        {
            ref var nodeA = ref _nodes[i];
            ref var nodeB = ref _nodes[i + 1];

            var currentDistance = Vector3.Distance(in nodeA.CurrentPosition, in nodeB.CurrentPosition);
            var error = currentDistance - _nodeDistance;

            Vector3 direction = (nodeA.CurrentPosition - nodeB.CurrentPosition).normalized;
            Vector3 correction = direction * error;

            // 두 점이 핀으로 고정되지 않았다면 반반씩 이동시켜 거리 조절
            if (!nodeA.IsPinned) _nodes[i].CurrentPosition -= correction * 0.5f;
            if (!nodeB.IsPinned) _nodes[i + 1].CurrentPosition += correction * 0.5f;
        }
    }
    
    /// <summary>
    /// 장애물 충돌을 계산하여 밀어내기.
    /// stride 기반으로 매 N번째 노드만 검사하여 성능을 최적화합니다.
    /// 고속 이동 시 Linecast로 관통을 방지합니다.
    /// </summary>
    private void ApplyCollisionConstraints()
    {
        float safeRadius = _nodeRadius + _collisionMargin;

        for (int i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i].IsPinned) continue;

            // stride 기반 스킵: WrapPin이나 장애물 접촉 노드는 항상 검사
            bool mustCheck = _nodes[i].IsWrapPin || _nodes[i].IsTouchingObstacle;
            if (!mustCheck && i % _collisionStride != 0) continue;

            Vector3 prevPos = _nodes[i].PreviousPosition;
            Vector3 currPos = _nodes[i].CurrentPosition;
            Vector3 delta = currPos - prevPos;
            float distanceToMove = delta.magnitude;

            // 고정 장애물에 부딪힐 때 충돌 처리
            if (distanceToMove > 0.001f)
            {
                Vector3 direction = delta / distanceToMove;

                // 표면에서 쏘면 무시되는 현상 방지를 위해 시작점을 살짝 뒤로 잡음
                Vector3 safePrevPos = prevPos - (direction * 0.01f);
                float safeDistance = distanceToMove + 0.01f;

                if (Physics.SphereCast(safePrevPos, _nodeRadius, direction, out RaycastHit hit, safeDistance, _obstacleLayer))
                {
                    float friction = GetFrictionForLayer(hit.collider.gameObject.layer);

                    // 장애물 표면으로 밀어냄
                    _nodes[i].CurrentPosition = hit.point + hit.normal * safeRadius;

                    // 벽 표면을 따라 미끄러지는 속도만 남기기
                    Vector3 currentVelocity = _nodes[i].CurrentPosition - prevPos;
                    Vector3 slideVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);

                    _nodes[i].PreviousPosition = _nodes[i].CurrentPosition - (slideVelocity * friction);

                    _nodes[i].IsTouchingObstacle = true;
                    continue;
                }

                // 고속 이동 시 Linecast로 관통 방지 (SphereCast를 빠져나간 경우)
                if (distanceToMove > safeRadius)
                {
                    if (Physics.Linecast(prevPos, currPos, out RaycastHit lineHit, _obstacleLayer))
                    {
                        _nodes[i].CurrentPosition = lineHit.point + lineHit.normal * safeRadius;
                        _nodes[i].PreviousPosition = _nodes[i].CurrentPosition;
                        _nodes[i].IsTouchingObstacle = true;
                        continue;
                    }   
                }
            }

            // 다른 플레이어가 지나갈 때 충돌 처리
            int count = Physics.OverlapSphereNonAlloc(_nodes[i].CurrentPosition, _nodeRadius + 0.05f, _overlapBuffer, _dynamicObstacleLayer);

            for (int j = 0; j < count; j++)
            {
                Collider obstacle = _overlapBuffer[j];
                _overlappingColliders.Add(obstacle);

                Vector3 closestPoint = obstacle.ClosestPoint(_nodes[i].CurrentPosition);
                float penetrationDistance = Vector3.Distance(_nodes[i].CurrentPosition, closestPoint);

                if (penetrationDistance < _nodeRadius)
                {
                    Vector3 pushDirection = (_nodes[i].CurrentPosition - closestPoint).normalized;
                    if (pushDirection == Vector3.zero) pushDirection = Vector3.up;

                    _nodes[i].CurrentPosition = closestPoint + (pushDirection * _nodeRadius);
                    _nodes[i].IsTouchingObstacle = true;
                }
            }
        }
    }
    
    public void GetNodePositions(ref Vector3[] buffer)
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            buffer[i] = _nodes[i].CurrentPosition;
        }
    }

    public float GetCurrentTension() 
    {
        float totalDistance = 0;
        for (int i = 0; i < _nodes.Length - 1; i++)
        {
            totalDistance += Vector3.Distance(_nodes[i].CurrentPosition, _nodes[i+1].CurrentPosition);
        }
        var baseLength = _nodeDistance * (_nodes.Length - 1);
        return totalDistance / baseLength;
    }
    
    public void SetNodePosition(int index, Vector3 position)
    {
        if (index < 0 || index >= _nodes.Length) return;
        _nodes[index].CurrentPosition = position;
        _nodes[index].IsPinned = true;
    }
    
    public Vector3 CalculatePullingDirection(Vector3 targetPosition, bool isStartAnchor)
    {
        int startIndex = isStartAnchor ? 1 : _nodes.Length - 2;
        int iterationStep = isStartAnchor ? 1 : -1;

        // 1순위: 감김 고정점(WrapPin)을 먼저 탐색
        for (int i = startIndex; i > 0 && i < _nodes.Length - 1; i += iterationStep)
        {
            if (!_nodes[i].IsWrapPin) continue;

            Vector3 difference = _nodes[i].CurrentPosition - targetPosition;
            if (difference.sqrMagnitude < 0.05f) continue;
            return difference.normalized;
        }

        // 2순위: 장애물에 닿은 노드 탐색 (기존 로직)
        for (int i = startIndex; i > 0 && i < _nodes.Length - 1; i += iterationStep)
        {
            Vector3 position = _nodes[i].CurrentPosition;
            Vector3 difference = position - targetPosition;

            if (difference.sqrMagnitude < 0.05f) continue;

            if (_nodes[i].IsTouchingObstacle)
            {
                return difference.normalized;
            }
        }

        // 부딪힌 장애물이 없을 때 처리
        int oppositeEndIndex = isStartAnchor ? _nodes.Length - 1 : 0;
        return (_nodes[oppositeEndIndex].CurrentPosition - targetPosition).normalized;
    }

    /// <summary>
    /// 감김점 경로를 설정하고, 노드를 재배치합니다.
    /// waypoints 배열은 [AnchorA, WP0, WP1, ..., AnchorB] 형태입니다.
    /// </summary>
    public void SetWrapWaypoints(Vector3[] waypoints)
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            ClearWrapPins();
            _hasWrapWaypoints = false;
            _wrapWaypoints = null;
            return;
        }

        _wrapWaypoints = waypoints;
        int wrapPointCount = waypoints.Length - 2; // 앵커 제외

        if (wrapPointCount <= 0)
        {
            ClearWrapPins();
            _hasWrapWaypoints = false;
            return;
        }

        _hasWrapWaypoints = true;

        // 모든 노드의 WrapPin 초기화
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].IsWrapPin = false;
            // 앵커(양 끝)가 아닌 노드의 Pin 상태를 해제하여 재배치 가능하게 함
            if (i > 0 && i < _nodes.Length - 1)
                _nodes[i].IsPinned = false;
        }

        // 자유 구간 수 = wrapPointCount + 1
        int freeSegments = wrapPointCount + 1;
        int freeNodes = _nodes.Length - 2 - wrapPointCount; // 앵커 2개 + 감김점 제외

        if (freeNodes < freeSegments)
        {
            // 노드가 부족하면 감김점만이라도 배치
            freeNodes = 0;
        }

        // 각 자유 구간에 노드를 비례 배분
        float[] segmentLengths = new float[freeSegments];
        float totalFreeLength = 0f;
        for (int s = 0; s < freeSegments; s++)
        {
            segmentLengths[s] = Vector3.Distance(waypoints[s], waypoints[s + 1]);
            totalFreeLength += segmentLengths[s];
        }

        // 노드 배치: 앵커(0) → [구간0 노드들] → WrapPin → [구간1 노드들] → ... → 앵커(N-1)
        int nodeIndex = 1; // 0은 앵커A
        for (int s = 0; s < freeSegments; s++)
        {
            // 이 구간에 할당되는 자유 노드 수
            int nodesInSegment;
            if (freeNodes <= 0 || totalFreeLength < 0.001f)
            {
                nodesInSegment = 0;
            }
            else if (s == freeSegments - 1)
            {
                // 마지막 구간에 나머지 전부 할당
                nodesInSegment = freeNodes;
            }
            else
            {
                nodesInSegment = Mathf.RoundToInt((segmentLengths[s] / totalFreeLength) * freeNodes);
                nodesInSegment = Mathf.Max(0, Mathf.Min(nodesInSegment, freeNodes));
            }

            Vector3 segStart = waypoints[s];
            Vector3 segEnd = waypoints[s + 1];

            // 자유 노드를 구간 내에 균등 배치
            for (int n = 0; n < nodesInSegment; n++)
            {
                if (nodeIndex >= _nodes.Length - 1) break;

                float t = (float)(n + 1) / (nodesInSegment + 1);
                Vector3 pos = Vector3.Lerp(segStart, segEnd, t);
                _nodes[nodeIndex].CurrentPosition = pos;
                _nodes[nodeIndex].PreviousPosition = pos;
                _nodes[nodeIndex].IsPinned = false;
                _nodes[nodeIndex].IsWrapPin = false;
                nodeIndex++;
            }

            freeNodes -= nodesInSegment;

            // 구간 끝에 감김점 Pin 배치 (마지막 구간 제외 — 마지막은 앵커B)
            if (s < freeSegments - 1 && nodeIndex < _nodes.Length - 1)
            {
                _nodes[nodeIndex].CurrentPosition = waypoints[s + 1];
                _nodes[nodeIndex].PreviousPosition = waypoints[s + 1];
                _nodes[nodeIndex].IsPinned = true;
                _nodes[nodeIndex].IsWrapPin = true;
                nodeIndex++;
            }
        }
    }

    private void ClearWrapPins()
    {
        for (int i = 1; i < _nodes.Length - 1; i++)
        {
            if (_nodes[i].IsWrapPin)
            {
                _nodes[i].IsPinned = false;
                _nodes[i].IsWrapPin = false;
            }
        }
    }
    
    /// <summary>
    /// 레이어 정수값을 통해 딕셔너리에 설정된 LayerMask 마찰력을 찾아 반환합니다.
    /// </summary>
    private float GetFrictionForLayer(int layer)
    {
        const float defaultFriction = 0f;

        LayerMask mask = 1 << layer;
        return _frictionMap.GetValueOrDefault(mask, defaultFriction);
    }
}
