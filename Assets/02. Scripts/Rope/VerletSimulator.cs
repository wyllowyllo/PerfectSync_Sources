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
    private readonly int _subSteps;
    private readonly float _compliance;
    private readonly float[] _lambdas;
    private readonly LayerMask _obstacleLayer;
    private readonly LayerMask _dynamicObstacleLayer;
    private readonly SerializableDictionary<LayerMask, float> _frictionMap;
    
    private readonly HashSet<Collider> _overlappingColliders = new();
    public HashSet<Collider> OverlappingColliders => _overlappingColliders;
    
    private readonly Collider[] _overlapBuffer = new Collider[8];
    private static readonly Vector3 Gravity = new Vector3(0, -9.81f, 0);
    
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
    public VerletSimulator(int nodeCount, float totalLength, int constraintIterations, Vector3 startPosition, LayerMask obstacleLayer, LayerMask dynamicObstacleLayer, SerializableDictionary<LayerMask, float> frictionMap, int subSteps = 3, float compliance = 0f)
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
        _subSteps = Mathf.Max(1, subSteps);
        _compliance = Mathf.Max(0f, compliance);
        _lambdas = new float[nodeCount - 1];
        _obstacleLayer = obstacleLayer;
        _dynamicObstacleLayer = dynamicObstacleLayer;
        _frictionMap = frictionMap;
        
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

        float subDt = deltaTime / _subSteps;

        for (int step = 0; step < _subSteps; step++)
        {
            ApplyVerletIntegrator(subDt);

            // XPBD: 각 서브스텝 시작 시 라그랑주 승수 초기화
            System.Array.Clear(_lambdas, 0, _lambdas.Length);

            for (int i = 0; i < _constraintIterations; i++)
            {
                ApplyDistanceConstraints(subDt);
                ApplyCollisionConstraints();
            }
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
    /// XPBD 기반 거리 제약 솔버.
    /// compliance(α)와 Lagrange multiplier(λ)를 사용하여 dt에 독립적인 강성 제어를 수행합니다.
    /// α = 0이면 완전 강체, α가 클수록 탄성적으로 동작합니다.
    /// </summary>
    private void ApplyDistanceConstraints(float subDt)
    {
        // α̃ = α / dt² (시간 스케일링된 유연도)
        float alphaTilde = _compliance / (subDt * subDt);

        for (int i = 0; i < _nodes.Length - 1; i++)
        {
            ref var nodeA = ref _nodes[i];
            ref var nodeB = ref _nodes[i + 1];

            Vector3 diff = nodeA.CurrentPosition - nodeB.CurrentPosition;
            float currentDistance = diff.magnitude;

            if (currentDistance < 1e-7f) continue;

            // 제약 값: C = 현재 거리 - 기본 거리
            float C = currentDistance - _nodeDistance;

            // 역질량: 고정된 노드는 0 (무한 질량), 자유 노드는 1
            float w1 = nodeA.IsPinned ? 0f : 1f;
            float w2 = nodeB.IsPinned ? 0f : 1f;
            float wSum = w1 + w2;

            if (wSum < 1e-7f) continue;

            // Δλ = -(C + α̃ · λ) / (w₁ + w₂ + α̃)
            float deltaLambda = -(C + alphaTilde * _lambdas[i]) / (wSum + alphaTilde);
            _lambdas[i] += deltaLambda;

            // 위치 보정: 역질량에 비례하여 각 노드를 이동
            Vector3 direction = diff / currentDistance;
            _nodes[i].CurrentPosition += direction * (deltaLambda * w1);
            _nodes[i + 1].CurrentPosition -= direction * (deltaLambda * w2);
        }
    }
    
    /// <summary>
    /// 장애물 충돌을 계산하여 밀어내기
    /// </summary>
    private void ApplyCollisionConstraints()
    {
        const float NodeRadius = 0.15f; // 고무줄 두께에 맞춰 조절

        for (int i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i].IsPinned) continue;

            Vector3 prevPos = _nodes[i].PreviousPosition;
            Vector3 currPos = _nodes[i].CurrentPosition;
            Vector3 delta = currPos - prevPos;
            float distanceToMove = delta.magnitude;

            // 1단계: SphereCast 경로 추적 — 이전→현재 이동 경로상의 고정 장애물 충돌
            if (distanceToMove > 0.001f)
            {
                Vector3 direction = delta / distanceToMove;

                // 표면에서 쏘면 무시되는 현상 방지를 위해 시작점을 살짝 뒤로 잡음
                Vector3 safePrevPos = prevPos - (direction * 0.01f);
                float safeDistance = distanceToMove + 0.01f;

                if (Physics.SphereCast(safePrevPos, NodeRadius, direction, out RaycastHit hit, safeDistance, _obstacleLayer))
                {
                    float friction = GetFrictionForLayer(hit.collider.gameObject.layer);

                    // 장애물 표면으로 밀어냄
                    _nodes[i].CurrentPosition = hit.point + hit.normal * (NodeRadius + 0.005f);

                    // 벽 표면을 따라 미끄러지는 속도만 남기기
                    Vector3 currentVelocity = _nodes[i].CurrentPosition - prevPos;
                    Vector3 slideVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);

                    _nodes[i].PreviousPosition = _nodes[i].CurrentPosition - (slideVelocity * friction);

                    _nodes[i].IsTouchingObstacle = true;
                    continue;
                }
            }

            // 2단계: OverlapSphere 관통 보정 — 거리 제약 보정 후 벽 안으로 밀려난 경우 복구
            int staticCount = Physics.OverlapSphereNonAlloc(_nodes[i].CurrentPosition, NodeRadius, _overlapBuffer, _obstacleLayer);

            for (int j = 0; j < staticCount; j++)
            {
                Collider obstacle = _overlapBuffer[j];
                Vector3 closestPoint = obstacle.ClosestPoint(_nodes[i].CurrentPosition);
                float penetrationDistance = Vector3.Distance(_nodes[i].CurrentPosition, closestPoint);

                if (penetrationDistance < NodeRadius)
                {
                    Vector3 pushDirection = (_nodes[i].CurrentPosition - closestPoint).normalized;
                    if (pushDirection == Vector3.zero) pushDirection = Vector3.up;

                    float friction = GetFrictionForLayer(obstacle.gameObject.layer);

                    _nodes[i].CurrentPosition = closestPoint + pushDirection * (NodeRadius + 0.005f);

                    Vector3 currentVelocity = _nodes[i].CurrentPosition - _nodes[i].PreviousPosition;
                    Vector3 slideVelocity = Vector3.ProjectOnPlane(currentVelocity, pushDirection);
                    _nodes[i].PreviousPosition = _nodes[i].CurrentPosition - (slideVelocity * friction);

                    _nodes[i].IsTouchingObstacle = true;
                }
            }

            // 3단계: 동적 장애물(플레이어) 충돌 처리
            int dynamicCount = Physics.OverlapSphereNonAlloc(_nodes[i].CurrentPosition, NodeRadius + 0.05f, _overlapBuffer, _dynamicObstacleLayer);

            for (int j = 0; j < dynamicCount; j++)
            {
                Collider obstacle = _overlapBuffer[j];
                _overlappingColliders.Add(obstacle);

                Vector3 closestPoint = obstacle.ClosestPoint(_nodes[i].CurrentPosition);
                float penetrationDistance = Vector3.Distance(_nodes[i].CurrentPosition, closestPoint);

                if (penetrationDistance < NodeRadius)
                {
                    Vector3 pushDirection = (_nodes[i].CurrentPosition - closestPoint).normalized;
                    if (pushDirection == Vector3.zero) pushDirection = Vector3.up;

                    _nodes[i].CurrentPosition = closestPoint + (pushDirection * NodeRadius);
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
        
        for (int i = startIndex; i > 0 && i < _nodes.Length - 1; i += iterationStep)
        {
            Vector3 position = _nodes[i].CurrentPosition;
            Vector3 difference = position - targetPosition;
        
            if (difference.sqrMagnitude < 0.05f) continue;

            bool isTouching = _nodes[i].IsTouchingObstacle;
        
            if (isTouching)
            {
                return difference.normalized;
            }
        }
        
        // 부딪힌 장애물이 없을 때 처리
        int oppositeEndIndex = isStartAnchor ? _nodes.Length - 1 : 0;
        return (_nodes[oppositeEndIndex].CurrentPosition - targetPosition).normalized;
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
