using System;
using System.Collections;
using InGame.Obstacle;
using UnityEngine;

/// <summary>
/// 펀칭 장애물. 부모(Root)는 transform.position으로 이동하며 Rigidbody가 없으므로,
/// 자식인 이 오브젝트의 kinematic Rigidbody는 부모 transform 계층을 통해 자동 추적된다.
/// 펀칭 애니메이션 시에만 MovePosition으로 직접 이동하여 정확한 충돌 속도를 보장한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PopupObstacle : MonoBehaviour, ITrap, IRecoilSource
{
    [Header("Movement Settings")]
    [Tooltip("장애물이 도달할 목표 지점")]
    [SerializeField] private Transform _targetTransform;

    [Tooltip("튀어나올 때 소요 시간")]
    [SerializeField] private float _popupDuration = 0.05f;

    [Tooltip("복귀할 때 소요 시간")]
    [SerializeField] private float _retractDuration = 1.0f;

    private Rigidbody _rigidbody;
    private Vector3 _startLocalPosition;
    private bool _isActionProcess;

    public event Action OnResetComplete;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
        _startLocalPosition = transform.localPosition;
    }

    #region ITrap

    public void Activate()
    {
        if (_isActionProcess) return;
        StartCoroutine(MoveRoutine(_targetTransform.localPosition, _popupDuration, true));
    }

    public void Reset()
    {
        if (_isActionProcess) return;
        StartCoroutine(MoveRoutine(_startLocalPosition, _retractDuration, false));
    }

    #endregion

    #region IRecoilSource

    public bool IsRotational => false;

    public Vector3 GetRecoilDirection()
    {
        Transform parent = transform.parent;
        if (!parent) return Vector3.forward;

        Vector3 startWorld = parent.TransformPoint(_startLocalPosition);
        return -(_targetTransform.position - startWorld).normalized;
    }

    #endregion

    #region Internal

    private IEnumerator MoveRoutine(Vector3 targetLocal, float duration, bool isAttacking)
    {
        _isActionProcess = true;
        float elapsedTime = 0f;
        Vector3 fromLocal = transform.localPosition;
        Transform parent = transform.parent;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = isAttacking ? Mathf.Sin(t * Mathf.PI * 0.5f) : t * t;

            Vector3 nextLocal = Vector3.Lerp(fromLocal, targetLocal, easedT);
            _rigidbody.MovePosition(parent.TransformPoint(nextLocal));
            yield return null;
        }

        _rigidbody.MovePosition(parent.TransformPoint(targetLocal));
        _isActionProcess = false;

        if (!isAttacking)
            OnResetComplete?.Invoke();
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_targetTransform == null) return;

        Gizmos.color = Color.red;
        Vector3 targetWorldPos = _targetTransform.position;

        Gizmos.DrawLine(transform.position, targetWorldPos);
        Gizmos.matrix = Matrix4x4.TRS(targetWorldPos, transform.rotation, transform.localScale);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}
