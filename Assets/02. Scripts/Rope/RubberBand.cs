using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 두 플레이어 사이에 연결되는 물리 기반 고무줄을 제어하는 메인 컴포넌트입니다.
/// 시뮬레이션(FixedUpdate)과 렌더링(LateUpdate)의 생명주기를 관리합니다.
/// Authority(Host)는 물리력 적용 + 충돌 판정을 수행하고,
/// Remote(Guest)는 시각적 시뮬레이션만 실행합니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RubberBand : MonoBehaviour
{
    [Header("Physical Properties")]
    [Tooltip("고무줄을 구성하는 정점의 개수")]
    [SerializeField, Range(5, 50)] private int _nodeCount = 20;

    [Tooltip("고무줄의 기본 길이")]
    [SerializeField] private float _baseLength = 5f;

    [Tooltip("물리 제약 조건 반복 횟수 (높을수록 뻣뻣해지지만 연산량 증가)")]
    [SerializeField, Range(1, 10)] private int _constraintIterations = 5;

    [Tooltip("물리 서브스텝 수 (높을수록 고속 이동 시 안정적이지만 연산량 증가)")]
    [SerializeField, Range(1, 5)] private int _subSteps = 3;

    [Header("Tube Settings")]
    [Tooltip("원통 단면의 각 수 (8=팔각형)")]
    [SerializeField, Range(3, 32)] private int _sides = 8;

    [Tooltip("물리 노드 사이의 보간 분할 수 (높을수록 부드러운 곡선)")]
    [SerializeField, Range(1, 5)] private int _interpolationSegments = 3;

    [Tooltip("렌더링 색상")]
    [SerializeField] private Gradient _color;

    [Header("Advanced Physics")]
    [Tooltip("고무줄의 탄성 계수")]
    [SerializeField] private float _springConstant = 50f;

    [Tooltip("감쇠 계수: 서로 가까워질 때 브레이크를 걸어주는 힘")]
    [SerializeField] private float _dampingConstant = 15f;

    [Tooltip("탄성 곡선: F = kx^ 에서 x의 지수")]
    [SerializeField, Range(1f, 3f)] private float _elasticCurve = 2f;

    [Header("Visual Properties")]
    [Tooltip("렌더링되는 고무줄의 굵기")]
    [SerializeField] private float _thickness = 0.1f;

    [Header("Collision Support")]
    [Tooltip("장애물로 인식할 레이어")]
    [SerializeField] private LayerMask _obstacleLayer;

    [Tooltip("플레이어 레이어")]
    [SerializeField] private LayerMask _playerLayer;

    [Space]
    [Tooltip("레이어별 마찰력 설정")]
    [SerializeField] private SerializableDictionary<LayerMask, float> _layerFrictionSettings;

    private IRopePhysics _simulator;
    private IRopeRenderer _renderer;

    private Vector3[] _nodeBuffer;
    private float _currentTension;

    // 동적 바인딩 타겟 (물리 바디의 Transform).
    private Transform _targetA;
    private Transform _targetB;
    private Rigidbody _rigidbodyA;
    private Rigidbody _rigidbodyB;

    // 물리 바디 로컬 공간 기준 앵커 오프셋.
    private Vector3 _anchorOffsetA;
    private Vector3 _anchorOffsetB;

    private bool _isAuthority;
    private bool _initialized;

    // 충돌 통과 판정용.
    private readonly HashSet<Collider> _previousColliders = new();
    private readonly Dictionary<Collider, int> _enterSides = new();

    public event Action<Collider> OnCrossingDetected;
    public float CurrentTension => _currentTension;

    private void Awake()
    {
        var meshFilter = GetComponent<MeshFilter>();
        _renderer = new TubeRenderer(meshFilter, _sides, _color, _nodeCount, _interpolationSegments);
        _nodeBuffer = new Vector3[_nodeCount];
    }

    /// <summary>
    /// 고무줄의 양 끝 타겟과 Rigidbody를 동적으로 바인딩합니다.
    /// targetA/B는 물리 바디(RootBody)의 Transform이며, offsetA/B는 앵커의 로컬 오프셋입니다.
    /// Guest는 rigidbody를 null로 전달합니다 (물리력 미적용).
    /// </summary>
    public void BindTargets(Transform targetA, Transform targetB,
        Rigidbody rigidbodyA, Rigidbody rigidbodyB,
        Vector3 offsetA = default, Vector3 offsetB = default)
    {
        _targetA = targetA;
        _targetB = targetB;
        _rigidbodyA = rigidbodyA;
        _rigidbodyB = rigidbodyB;
        _anchorOffsetA = offsetA;
        _anchorOffsetB = offsetB;
    }

    /// <summary>
    /// Host/Guest 역할을 설정합니다.
    /// Authority(Host)만 물리력 적용과 충돌 판정을 수행합니다.
    /// </summary>
    public void SetAuthority(bool isAuthority)
    {
        _isAuthority = isAuthority;
    }

    /// <summary>
    /// 시뮬레이터를 현재 타겟 위치 기준으로 재초기화합니다.
    /// 모드 전환 시 잔여 장력을 제거하기 위해 호출합니다.
    /// </summary>
    public void ResetSimulator()
    {
        Vector3 startPos = _targetA.TransformPoint(_anchorOffsetA);
        _simulator = new VerletSimulator(
            _nodeCount,
            _baseLength,
            _constraintIterations,
            startPos,
            _obstacleLayer,
            _playerLayer,
            _layerFrictionSettings,
            _subSteps
        );
        _currentTension = 0f;
        _previousColliders.Clear();
        _enterSides.Clear();
        _initialized = true;
    }

    private void FixedUpdate()
    {
        if (!_initialized) return;

        SyncAnchorPositions();
        _simulator.Simulate(Time.fixedDeltaTime);
        _currentTension = _simulator.GetCurrentTension();

        // Host만 물리력 적용 + 충돌 판정.
        if (_isAuthority)
        {
            ApplyElasticForceToPlayers();
            CheckCrossingEvents();
        }
    }

    private void LateUpdate()
    {
        if (!_initialized) return;

        _simulator.GetNodePositions(ref _nodeBuffer);

        var tension = Mathf.Max(1f, _currentTension);
        var thickness = _thickness / tension;
        _renderer.RenderRope(_nodeBuffer, thickness);
    }

    private void OnDisable()
    {
        _initialized = false;
    }

    /// <summary>
    /// 고무줄의 시작점과 끝점을 플레이어의 물리 바디 위치 + 앵커 오프셋으로 동기화합니다.
    /// </summary>
    private void SyncAnchorPositions()
    {
        Vector3 worldA = _targetA.TransformPoint(_anchorOffsetA);
        Vector3 worldB = _targetB.TransformPoint(_anchorOffsetB);
        _simulator.SetNodePosition(0, worldA);
        _simulator.SetNodePosition(_nodeCount - 1, worldB);
    }

    /// <summary>
    /// 고무줄이 늘어난 길이에 비례하여 두 타겟의 Rigidbody에 당기는 힘을 가합니다.
    /// </summary>
    private void ApplyElasticForceToPlayers()
    {
        if (_currentTension <= 1.5f) return;
        if (_rigidbodyA == null || _rigidbodyB == null) return;

        // 훅의 법칙 F = k * x^e - c * x.dot
        var stretch = _currentTension - 1.0f;
        var springForce = Mathf.Pow(stretch, _elasticCurve) * _springConstant;

        // 장력의 방향 계산 (물리 바디 기준 앵커 월드 위치 사용).
        Vector3 anchorPosA = _targetA.TransformPoint(_anchorOffsetA);
        Vector3 anchorPosB = _targetB.TransformPoint(_anchorOffsetB);
        var pullDirectionA = _simulator.CalculatePullingDirection(anchorPosA, true);
        var pullDirectionB = _simulator.CalculatePullingDirection(anchorPosB, false);

        // y축 장력 제한.
        pullDirectionA = new Vector3(pullDirectionA.x, pullDirectionA.y * 0.2f, pullDirectionA.z).normalized;
        pullDirectionB = new Vector3(pullDirectionB.x, pullDirectionB.y * 0.2f, pullDirectionB.z).normalized;

        // 댐핑 계산.
        Vector3 relativeVelocity = _rigidbodyB.linearVelocity - _rigidbodyA.linearVelocity;
        Vector3 planarRelativeVel = new Vector3(relativeVelocity.x, 0f, relativeVelocity.z);

        float separationSpeed = Vector3.Dot(planarRelativeVel, -pullDirectionA);

        float dampingForce = 0f;
        if (separationSpeed < 0)
        {
            dampingForce = separationSpeed * _dampingConstant;
        }

        float finalForce = Mathf.Max(0f, springForce + dampingForce);

        // 작용-반작용의 법칙.
        _rigidbodyA.AddForce(pullDirectionA * finalForce, ForceMode.Force);
        _rigidbodyB.AddForce(pullDirectionB * finalForce, ForceMode.Force);
    }

    /// <summary>
    /// 제 3의 타겟이 고무줄을 통과했는 지를 체크합니다.
    /// 통과 감지 시 OnCrossingDetected 이벤트를 발행합니다.
    /// </summary>
    private void CheckCrossingEvents()
    {
        // 시뮬레이터가 이번 프레임에 수집한 해시셋.
        var currentColliders = _simulator.OverlappingColliders;

        Vector3 anchorPosA = _targetA.TransformPoint(_anchorOffsetA);
        Vector3 anchorPosB = _targetB.TransformPoint(_anchorOffsetB);

        // OnColliderEnter.
        foreach (var coll in currentColliders)
        {
            if (_previousColliders.Contains(coll)) continue;

            // 처음 닿은 순간의 방향을 기록.
            _enterSides[coll] = CCW_XZ(anchorPosA, anchorPosB, coll.transform.position);
        }

        // OnColliderExit.
        foreach (var coll in _previousColliders)
        {
            if (currentColliders.Contains(coll)) continue;

            if (!_enterSides.TryGetValue(coll, out var enterSide)) continue;

            // 떨어진 순간의 방향 계산.
            var exitSide = CCW_XZ(anchorPosA, anchorPosB, coll.transform.position);

            // 진입 방향과 탈출 방향이 다르면 통과 판정.
            if (enterSide != exitSide)
            {
                OnCrossingDetected?.Invoke(coll);
            }

            // 검사 끝났으니 기록 삭제.
            _enterSides.Remove(coll);
        }

        // 다음 프레임 비교를 위해 덮어쓰기.
        _previousColliders.Clear();
        _previousColliders.UnionWith(currentColliders);
    }

    /// <summary>
    /// 선분 AB를 기준으로 점 C가 서있는 방향을 계산합니다.
    /// </summary>
    private static int CCW_XZ(Vector3 a, Vector3 b, Vector3 c)
    {
        var crossProduct = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        return crossProduct > 0 ? 1 : -1;
    }
}
