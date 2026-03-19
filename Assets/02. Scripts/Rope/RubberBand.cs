using UnityEngine;

/// <summary>
/// 두 플레이어 사이에 연결되는 물리 기반 고무줄을 제어하는 메인 컴포넌트입니다.
/// 시뮬레이션(FixedUpdate)과 렌더링(LateUpdate)의 생명주기를 관리합니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RubberBand : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("고무줄의 양 끝에 묶일 타겟")]
    [SerializeField] private Transform _targetA;
    [SerializeField] private Transform _targetB;

    [Header("Physical Properties")]
    [Tooltip("고무줄을 구성하는 정점의 개수")]
    [SerializeField, Range(5, 50)] private int _nodeCount = 20;
    
    [Tooltip("고무줄의 기본 길이")]
    [SerializeField] private float _baseLength = 5f;
    
    [Tooltip("물리 제약 조건 반복 횟수 (높을수록 뻣뻣해지지만 연산량 증가)")]
    [SerializeField, Range(1, 10)] private int _constraintIterations = 5;
    
    [Header("Tube Settings")]
    [Tooltip("원통 단면의 각 수 (8=팔각형)")]
    [SerializeField, Range(3, 32)] private int _sides = 8; 

    [Tooltip("렌더링 색상")]
    [SerializeField] private Gradient _color;
    
    [Header("Physics Interaction")]
    [Tooltip("플레이어 A의 Rigidbody")]
    [SerializeField] private Rigidbody _rigidbodyA;
    [Tooltip("플레이어 B의 Rigidbody")]
    [SerializeField] private Rigidbody _rigidbodyB;

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
    private float _currentTension = 1f;
    private bool _isPlayerOverlapping;

    private void Awake()
    {
        var meshFilter = GetComponent<MeshFilter>();
        
        _renderer = new TubeRenderer(meshFilter, _sides, _color, _nodeCount);
        
        _nodeBuffer = new Vector3[_nodeCount];
        
        _simulator = new VerletSimulator(
            _nodeCount, 
            _baseLength, 
            _constraintIterations, 
            _targetA.position,
            _obstacleLayer,
            _playerLayer,
            _layerFrictionSettings
        );
    }
    
    private void FixedUpdate()
    {
        SyncAnchorPositions();
        _simulator.Simulate(Time.fixedDeltaTime);
        _currentTension = _simulator.GetCurrentTension();
        ApplyElasticForceToPlayers();
        CheckOverlapSnapEvent();
    }

    private void LateUpdate()
    {
        _simulator.GetNodePositions(ref _nodeBuffer);
        
        var tension = Mathf.Max(1f, _currentTension);
        var thickness = _thickness / tension;
        _renderer.RenderRope(_nodeBuffer, thickness);
    }

    /// <summary>
    /// 고무줄의 시작점과 끝점을 플레이어의 위치에 동기화합니다.
    /// </summary>
    private void SyncAnchorPositions()
    {
        _simulator.SetNodePosition(0, _targetA.position);
        _simulator.SetNodePosition(_nodeCount - 1, _targetB.position);
    }
    
    /// <summary>
    /// 고무줄이 늘어난 길이에 비례하여 두 타겟의 Rigidbody에 당기는 힘을 가합니다.
    /// </summary>
    private void ApplyElasticForceToPlayers()
    {
        if (_currentTension <= 1.5f) return;
        
        // 훅의 법칙 F = k * x^e - c * x.dot
        var stretch = _currentTension - 1.0f;
        var springForce = Mathf.Pow(stretch, _elasticCurve) * _springConstant;

        // 장력의 방향 계산
        var pullDirectionA = _simulator.CalculatePullingDirection(_targetA.position, true);
        var pullDirectionB = _simulator.CalculatePullingDirection(_targetB.position, false);

        // y축 장력 제한
        pullDirectionA = new Vector3(pullDirectionA.x, pullDirectionA.y * 0.2f, pullDirectionA.z).normalized;
        pullDirectionB = new Vector3(pullDirectionB.x, pullDirectionB.y * 0.2f, pullDirectionB.z).normalized;
        
        // 댐핑 계산
        Vector3 relativeVelocity = _rigidbodyB.linearVelocity - _rigidbodyA.linearVelocity;
        Vector3 planarRelativeVel = new Vector3(relativeVelocity.x, 0f, relativeVelocity.z);
    
        float separationSpeed = Vector3.Dot(planarRelativeVel, -pullDirectionA); 

        float dampingForce = 0f;
        if (separationSpeed < 0) 
        {
            dampingForce = separationSpeed * _dampingConstant;
        }

        float finalForce = Mathf.Max(0f, springForce + dampingForce);

        // 작용-반작용의 법칙
        _rigidbodyA.AddForce(pullDirectionA * finalForce, ForceMode.Force);
        _rigidbodyB.AddForce(pullDirectionB * finalForce, ForceMode.Force);
    }
    
    private void CheckOverlapSnapEvent()
    {
        var previous = _isPlayerOverlapping;
        _isPlayerOverlapping = _simulator.IsOverlappingDynamicObstacle;
        
        if (!previous || _isPlayerOverlapping) return;
        
        Debug.Log("<color=orange>[고무줄 스냅 감지]</color> 플레이어 기절!");
    }
}
