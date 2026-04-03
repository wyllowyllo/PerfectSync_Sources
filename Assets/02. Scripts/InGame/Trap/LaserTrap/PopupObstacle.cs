using System.Collections;
using InGame.Obstacle;
using UnityEngine;

/// <summary>
/// 펀칭 장애물. 런타임에 부모에서 분리(unparent)하여 독립 Rigidbody로 동작한다.
/// MovingObstacle의 오프셋을 매 FixedUpdate마다 읽어 플랫폼 이동을 추적하고,
/// 팝업 애니메이션은 로컬 오프셋만 갱신하여 FixedUpdate가 최종 위치를 적용한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DefaultExecutionOrder(1)]
public class PopupObstacle : MonoBehaviour, ITrap, IRecoilSource
{
    [Header("Platform")]
    [Tooltip("이동 플랫폼 (MovingObstacle). 정지형이면 비워둬도 됨")]
    [SerializeField] private MovingObstacle _platform;

    [Header("Movement Settings")]
    [Tooltip("장애물이 도달할 목표 지점 (원래 부모 기준 로컬)")]
    [SerializeField] private Transform _targetTransform;

    [Tooltip("튀어나올 때 소요 시간")]
    [SerializeField] private float _popupDuration = 0.05f;

    [Tooltip("복귀할 때 소요 시간")]
    [SerializeField] private float _retractDuration = 1.0f;

    private Rigidbody _rigidbody;

    // 원래 부모 기준 캐싱 (unparent 전에 저장)
    private Vector3 _parentInitialScale;
    private Quaternion _parentInitialRotation;
    private Vector3 _fallbackBasePosition; // _platform이 없을 때 사용

    // 로컬 오프셋 (부모 로컬 공간 기준)
    private Vector3 _startLocalPosition;
    private Vector3 _targetLocalPosition;
    private Vector3 _currentLocalPosition;

    private bool _isActionProcess;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;

        Transform parent = transform.parent;

        // 부모 정보 캐싱
        _parentInitialScale = parent ? parent.lossyScale : Vector3.one;
        _parentInitialRotation = parent ? parent.rotation : Quaternion.identity;
        _fallbackBasePosition = parent ? parent.position : transform.position;

        // 로컬 위치 캐싱 (부모 로컬 공간)
        _startLocalPosition = transform.localPosition;
        _targetLocalPosition = _targetTransform.localPosition;
        _currentLocalPosition = _startLocalPosition;

        // 부모에서 분리 — 중첩 Rigidbody 제거
        Vector3 worldScale = transform.lossyScale;
        transform.SetParent(null);
        transform.localScale = worldScale;
    }

    private void FixedUpdate()
    {
        _rigidbody.MovePosition(LocalToWorld(_currentLocalPosition));
    }

    #region ITrap

    public void Activate()
    {
        if (_isActionProcess) return;
        StartCoroutine(MoveRoutine(_targetLocalPosition, _popupDuration, true));
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
        Vector3 startWorld = LocalToWorld(_startLocalPosition);
        Vector3 targetWorld = LocalToWorld(_targetLocalPosition);
        return -(targetWorld - startWorld).normalized;
    }

    #endregion

    #region Internal

    /// <summary>
    /// 원래 부모 로컬 공간의 좌표를 현재 월드 좌표로 변환한다.
    /// basePosition + parentRotation * Scale(parentScale, localPos)
    /// </summary>
    private Vector3 LocalToWorld(Vector3 localPos)
    {
        Vector3 basePos = _platform
            ? _platform.InitialPosition + _platform.CurrentOffset
            : _fallbackBasePosition;

        return basePos + _parentInitialRotation * Vector3.Scale(_parentInitialScale, localPos);
    }

    private IEnumerator MoveRoutine(Vector3 targetLocal, float duration, bool isAttacking)
    {
        _isActionProcess = true;
        float elapsedTime = 0f;
        Vector3 fromLocal = _currentLocalPosition;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = isAttacking ? Mathf.Sin(t * Mathf.PI * 0.5f) : t * t;

            _currentLocalPosition = Vector3.Lerp(fromLocal, targetLocal, easedT);
            yield return null;
        }

        _currentLocalPosition = targetLocal;
        _isActionProcess = false;
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
