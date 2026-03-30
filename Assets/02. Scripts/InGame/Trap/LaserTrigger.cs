using System;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserTrigger : MonoBehaviour
{
    [Header("Laser Nodes")]
    [Tooltip("레이저 시작 지점")]
    [SerializeField] private Transform _pointA;
    [Tooltip("레이저 끝 지점")]
    [SerializeField] private Transform _pointB;
    
    [Tooltip("레이저가 부딪힐 레이어")]
    [SerializeField] private LayerMask _detectionLayer; 
    
    private LineRenderer _lineRenderer;
    private Vector3 _startPosition;
    private Vector3 _endPosition;
    private Vector3 _direction;
    private float _distance;
    private bool _isActive = true;
    
    public event Action OnPlayerDetected;
    
    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;

        _startPosition = _pointA.position;
        _endPosition = _pointB.position;
        _direction = (_endPosition - _startPosition).normalized;
        _distance = Vector3.Distance(_startPosition, _endPosition);
        
        _lineRenderer.SetPosition(0, _startPosition);
        _lineRenderer.SetPosition(1, _endPosition);
    }

    private void Update()
    {
        if (!_isActive) return;
        
        if (Physics.Raycast(_startPosition, _direction, out var hit, _distance, _detectionLayer))
        {
            // 벽이나 플레이어에 부딪혔다면, 레이저의 끝점을 그 부딪힌 표면 좌표로 끊어버립니다.
            _lineRenderer.SetPosition(1, hit.point);

            // 부딪힌 대상이 플레이어인지 판별
            if (hit.collider.CompareTag("Player"))
            {
                // 플레이어 감지 이벤트 호출
                OnPlayerDetected?.Invoke();
            }
        }
        else
        {
            // 아무것도 가로막는 게 없다면 레이저 마저 발사
            _lineRenderer.SetPosition(1, _endPosition);
        }
    }
    
    public void SetLaserActive(bool active)
    {
        _isActive = active;
        _lineRenderer.enabled = active; 
    }
}
