using System;
using System.Collections.Generic;
using InGame.Obstacle;
using InGame.Player;
using UnityEngine;

/// <summary>
/// 펀칭 장애물. FixedUpdate 상태 머신 기반으로 MovePosition 이동만 담당.
/// 플레이어 히트는 Hammer와 동일하게 표준 경로 — HitDetector.OnCollisionEnter →
/// ObstacleHit.TryComputeKnockback → ObstacleHitProfile — 로 위임한다.
///
/// Spike는 Hammer와 달리 두 가지 보정이 필요하다:
/// 1. 공격 중에만 ObstacleHit을 활성화 — 정적 상태(Idle/Holding/Retracting)에서
///    플레이어가 단순 접촉만 해도 launch되는 것을 막는다.
/// 2. 첫 contact 후 IgnoreCollision damping — Spike는 translation 이동이라
///    한 번 hit 후에도 ragdoll bones 위로 계속 통과하며 depenetration이 누적
///    되어 "깔림"이 발생한다. 첫 contact의 OnCollisionEnter는 정상적으로
///    발생시켜 Hammer와 동일한 hit을 적용하고, 이후 추가 contact만 차단한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PopupObstacle : MonoBehaviour, ITrap
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
    private Collider _selfCollider;
    private ObstacleHit _obstacleHit;
    private Vector3 _startLocalPosition;
    private State _state = State.Idle;
    private float _elapsed;
    private Vector3 _fromLocal;
    private Vector3 _toLocal;
    private float _duration;

    private readonly HashSet<int> _hitDetectorIds = new();
    private readonly List<(Collider mine, Collider other)> _ignoredPairs = new();

    public event Action OnActivated;
    public event Action OnResetComplete;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
        _selfCollider = GetComponent<Collider>();
        _obstacleHit = GetComponent<ObstacleHit>();
        _startLocalPosition = transform.localPosition;

        SetHitActive(false);
    }

    private void OnEnable()
    {
        _state = State.Idle;
        _elapsed = 0f;
        SetHitActive(false);
    }

    private void OnDisable()
    {
        SetHitActive(false);
        RestoreIgnoredCollisions();
    }

    #region ITrap

    public void Activate()
    {
        if (_state != State.Idle) return;

        // 안전: 이전 사이클 잔여 IgnoreCollision 정리.
        RestoreIgnoredCollisions();

        _fromLocal = _startLocalPosition;
        _toLocal = _targetTransform.localPosition;
        _duration = _popupDuration;
        _elapsed = 0f;
        _state = State.Attacking;

        SetHitActive(true);
        OnActivated?.Invoke();
    }

    public void Reset()
    {
        if (_state == State.Idle || _state == State.Retracting) return;

        SetHitActive(false);
        RestoreIgnoredCollisions();

        _fromLocal = transform.localPosition;
        _toLocal = _startLocalPosition;
        _duration = _retractDuration;
        _elapsed = 0f;
        _state = State.Retracting;
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
            SetHitActive(false);
        }
        else
        {
            _state = State.Idle;
            OnResetComplete?.Invoke();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_state != State.Attacking) return;
        if (_selfCollider == null) return;

        var hitDetector = collision.collider.GetComponentInParent<HitDetector>();
        if (hitDetector == null) return;

        int id = hitDetector.GetInstanceID();
        if (!_hitDetectorIds.Add(id)) return;

        // 이 플레이어 hierarchy의 모든 콜라이더(본체 + ragdoll bones)와의
        // 추가 충돌 차단. 첫 contact의 OnCollisionEnter는 이미 player 측
        // HitDetector에서 정상 처리되었으므로, 이후의 depenetration push만
        // 막아 Spike가 ragdoll body 위를 통과하면서 누적되는 "깔림"을 방지.
        var playerColliders = hitDetector.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < playerColliders.Length; i++)
        {
            var pc = playerColliders[i];
            if (pc == null || pc == _selfCollider) continue;
            Physics.IgnoreCollision(_selfCollider, pc, true);
            _ignoredPairs.Add((_selfCollider, pc));
        }
    }

    private void SetHitActive(bool active)
    {
        if (_obstacleHit != null)
            _obstacleHit.enabled = active;
    }

    private void RestoreIgnoredCollisions()
    {
        for (int i = 0; i < _ignoredPairs.Count; i++)
        {
            var (mine, other) = _ignoredPairs[i];
            if (mine != null && other != null)
                Physics.IgnoreCollision(mine, other, false);
        }
        _ignoredPairs.Clear();
        _hitDetectorIds.Clear();
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
