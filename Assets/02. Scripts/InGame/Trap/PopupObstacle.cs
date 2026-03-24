using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PopupObstacle : MonoBehaviour, ITrap
{
    [Header("Movement Settings")]
    [Tooltip("장애물이 도달할 목표 지점")]
    [SerializeField] private Transform _targetTransform;
    
    [Tooltip("튀어나올 때 소요 시간")]
    [SerializeField] private float _popupDuration = 0.05f; 

    [Tooltip("복귀할 때 소요 시간")]
    [SerializeField] private float _retractDuration = 1.0f;

    private Rigidbody _rigidbody;
    private Vector3 _startWorldPosition;
    private Vector3 _targetWorldPosition;
    private bool _isActionProcess;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true; 

        _startWorldPosition = transform.position;
        _targetWorldPosition = _targetTransform.position;
    }

    public void Activate()
    {
        if (_isActionProcess) return;
        StartCoroutine(MoveRoutine(_targetWorldPosition, _popupDuration, true));
    }

    public void Reset()
    {
        if (_isActionProcess) return;
        StartCoroutine(MoveRoutine(_startWorldPosition, _retractDuration, false));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition, float duration, bool isAttacking)
    {
        _isActionProcess = true;
        float elapsedTime = 0f;
        
        Vector3 currentPosition = _rigidbody.position; 

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float time = elapsedTime / duration;
            float easedTime = isAttacking ? Mathf.Sin(time * Mathf.PI * 0.5f) : time * time;

            Vector3 nextPosition = Vector3.Lerp(currentPosition, targetPosition, easedTime);
            _rigidbody.MovePosition(nextPosition);
            
            yield return null;
        }

        _rigidbody.MovePosition(targetPosition);
        _isActionProcess = false;
    }
    
#if UNITY_EDITOR
    // 유니티 에디터에서 목표 위치를 출력
    private void OnDrawGizmosSelected()
    {
        if (_targetTransform == null) return;
        
        Gizmos.color = Color.red;
        
        Vector3 targetWorldPos = Application.isPlaying 
            ? _targetWorldPosition 
            : _targetTransform.position;
            
        Gizmos.DrawLine(transform.position, targetWorldPos);

        Gizmos.matrix = Matrix4x4.TRS(targetWorldPos, transform.rotation, transform.localScale);
        
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
#endif
