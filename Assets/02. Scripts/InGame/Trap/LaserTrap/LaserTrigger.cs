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
    private bool _isActive = true;
    private bool _detectionEnabled = true;

    // LineRenderer를 local space로 운용하여 LaserTrigger.transform의 부모 체인을
    // 그대로 따르게 한다. 프리팹 구조상 이 컴포넌트가 Spike 하위에 있어 plat 이동
    // 및 DestroyableObstacle blink로 LineRenderer가 재활성화되어도 stale 월드 좌표
    // 대신 현재 부모 기준 올바른 위치로 렌더된다.
    private Vector3 _localPointA;
    private Vector3 _localPointB;

    public event Action OnPlayerDetected;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.useWorldSpace = false;

        _localPointA = transform.InverseTransformPoint(_pointA.position);
        _localPointB = transform.InverseTransformPoint(_pointB.position);

        _lineRenderer.SetPosition(0, _localPointA);
        _lineRenderer.SetPosition(1, _localPointB);
    }

    public void SetDetectionEnabled(bool enabled)
    {
        _detectionEnabled = enabled;
    }

    private void Update()
    {
        if (!_isActive || !_detectionEnabled) return;

        Vector3 startPos = _pointA.position;
        Vector3 endPos = _pointB.position;

        Vector3 direction = (endPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, endPos);

        if (Physics.Raycast(startPos, direction, out var hit, distance, _detectionLayer))
        {
            _lineRenderer.SetPosition(1, transform.InverseTransformPoint(hit.point));

            if (hit.collider.CompareTag("Player"))
            {
                OnPlayerDetected?.Invoke();
            }
        }
        else
        {
            _lineRenderer.SetPosition(1, _localPointB);
        }
    }

    public void SetLaserActive(bool active)
    {
        _isActive = active;
        _lineRenderer.enabled = active;
        if (!active)
        {
            // blink로 LineRenderer가 잠시 재활성화될 때 이전 hit 절단 상태가 남지
            // 않도록 full beam local 좌표로 복원.
            _lineRenderer.SetPosition(1, _localPointB);
        }
    }
}
