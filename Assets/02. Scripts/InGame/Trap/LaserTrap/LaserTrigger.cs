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

    public event Action OnPlayerDetected;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
    }

    private void Update()
    {
        if (!_isActive) return;

        Vector3 startPos = _pointA.position;
        Vector3 endPos = _pointB.position;
        Vector3 direction = (endPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, endPos);

        _lineRenderer.SetPosition(0, startPos);

        if (Physics.Raycast(startPos, direction, out var hit, distance, _detectionLayer))
        {
            _lineRenderer.SetPosition(1, hit.point);

            if (hit.collider.CompareTag("Player"))
            {
                OnPlayerDetected?.Invoke();
            }
        }
        else
        {
            _lineRenderer.SetPosition(1, endPos);
        }
    }
    
    public void SetLaserActive(bool active)
    {
        _isActive = active;
        _lineRenderer.enabled = active; 
    }
}
