using System;
using InGame.Obstacle;
using UnityEngine;

/// <summary>
/// 펀칭 장애물. FixedUpdate 상태 머신 기반.
/// 부모(Root)의 transform 계층을 통해 이동하며,
/// 공격/복귀 시 MovePosition으로 충돌 속도를 보장한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PopupObstacle : MonoBehaviour, ITrap, IDestroyDirectionProvider
{
    private enum State { Idle, Attacking, Holding, Retracting }

    [Header("Movement Settings")]
    [Tooltip("장애물이 도달할 목표 지점")]
    [SerializeField] private Transform _targetTransform;

    [Tooltip("튀어나올 때 소요 시간")]
    [SerializeField] private float _popupDuration = 0.05f;

    [Tooltip("복귀할 때 소요 시간")]
    [SerializeField] private float _retractDuration = 1.0f;

    private Rigidbody _rigidbody;
    private Vector3 _startLocalPosition;
    private State _state = State.Idle;
    private float _elapsed;
    private Vector3 _fromLocal;
    private Vector3 _toLocal;
    private float _duration;

    // 공격 방향 캐싱 (파괴 시 넉백 방향 제공용).
    private Vector3 _attackDirectionLocal;

    public event Action OnResetComplete;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
        _startLocalPosition = transform.localPosition;

        if (_targetTransform != null)
            _attackDirectionLocal = (_targetTransform.localPosition - _startLocalPosition).normalized;
    }

    private void OnEnable()
    {
        _state = State.Idle;
        _elapsed = 0f;
    }

    #region ITrap

    public void Activate()
    {
        if (_state != State.Idle) return;

        _fromLocal = _startLocalPosition;
        _toLocal = _targetTransform.localPosition;
        _duration = _popupDuration;
        _elapsed = 0f;
        _state = State.Attacking;
    }

    public void Reset()
    {
        if (_state != State.Holding) return;

        _fromLocal = transform.localPosition;
        _toLocal = _startLocalPosition;
        _duration = _retractDuration;
        _elapsed = 0f;
        _state = State.Retracting;
    }

    #endregion

    #region IDestroyDirectionProvider

    public Vector3 GetDestroyDirection()
    {
        Transform parent = transform.parent;
        return parent != null
            ? parent.TransformDirection(_attackDirectionLocal)
            : _attackDirectionLocal;
    }

    #endregion

    #region Internal

    private void FixedUpdate()
    {
        if (_state == State.Idle || _state == State.Holding) return;

        _elapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        bool isAttacking = _state == State.Attacking;
        float easedT = isAttacking ? Mathf.Sin(t * Mathf.PI * 0.5f) : t * t;

        Vector3 nextLocal = Vector3.Lerp(_fromLocal, _toLocal, easedT);
        Transform parent = transform.parent;
        if (parent != null)
            _rigidbody.MovePosition(parent.TransformPoint(nextLocal));

        if (t < 1f) return;

        if (isAttacking)
        {
            _state = State.Holding;
        }
        else
        {
            _state = State.Idle;
            OnResetComplete?.Invoke();
        }
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
