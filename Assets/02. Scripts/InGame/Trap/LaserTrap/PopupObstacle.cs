using System;
using System.Collections.Generic;
using InGame.Obstacle;
using InGame.Player;
using UnityEngine;

/// <summary>
/// 펀칭 장애물. FixedUpdate 상태 머신 기반.
/// 부모(Root)의 transform 계층을 통해 이동하며,
/// 공격 시 MovePosition으로 충돌을 유지하면서 OverlapSphere + IgnoreCollision으로
/// 물리 push 없이 히트를 적용한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
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

    [Header("Hit Detection")]
    [Tooltip("히트 감지 대상 레이어 (CharacterBody)")]
    [SerializeField] private LayerMask _playerLayer;

    [Tooltip("히트 감지 반경")]
    [SerializeField] private float _detectionRadius = 1.0f;

    private Rigidbody _rigidbody;
    private Collider _collider;
    private ObstacleHit _obstacleHit;
    private Vector3 _startLocalPosition;
    private State _state = State.Idle;
    private float _elapsed;
    private Vector3 _fromLocal;
    private Vector3 _toLocal;
    private float _duration;

    // 히트 감지용.
    private Vector3 _previousWorldPosition;
    private const float HitCooldown = 0.5f;
    private const int MaxOverlapResults = 4;
    private readonly Collider[] _overlapBuffer = new Collider[MaxOverlapResults];
    private readonly Dictionary<int, float> _hitCooldowns = new();
    private readonly List<(Collider mine, Collider player)> _ignoredPairs = new();

    public event Action OnResetComplete;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.isKinematic = true;
        _collider = GetComponent<Collider>();
        _obstacleHit = GetComponent<ObstacleHit>();
        _startLocalPosition = transform.localPosition;
        _previousWorldPosition = transform.position;
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
        _previousWorldPosition = transform.position;
        _state = State.Attacking;
    }

    public void Reset()
    {
        if (_state == State.Idle || _state == State.Retracting) return;

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

        if (isAttacking)
            DetectHits();

        _previousWorldPosition = transform.position;

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

    private void DetectHits()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, _detectionRadius, _overlapBuffer, _playerLayer);

        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (col == null) continue;

            int id = col.GetInstanceID();
            if (_hitCooldowns.TryGetValue(id, out float lastTime)
                && Time.time - lastTime < HitCooldown)
                continue;

            var hitDetector = col.GetComponentInParent<HitDetector>();
            if (hitDetector == null) continue;

            _hitCooldowns[id] = Time.time;

            // 히트 적용.
            Vector3 contactPoint = col.ClosestPoint(transform.position);
            Vector3 spikeVelocity = (transform.position - _previousWorldPosition) / Time.fixedDeltaTime;
            ApplyHit(hitDetector, contactPoint, spikeVelocity);

            // 물리 push 방지: 이 플레이어와 충돌 무시.
            IgnoreCollisionWith(col);
        }
    }

    private void ApplyHit(HitDetector hitDetector, Vector3 contactPoint, Vector3 spikeVelocity)
    {
        HitData hit;

        if (_obstacleHit != null && _obstacleHit.Profile != null)
        {
            var profile = _obstacleHit.Profile;
            Vector3 knockback = profile.ComputeKnockback(contactPoint, spikeVelocity, transform);
            Vector3 torque = profile.ComputeTorque(knockback.magnitude);
            hit = new HitData(knockback, contactPoint, torque, profile.Response);
        }
        else
        {
            Vector3 direction = spikeVelocity.sqrMagnitude > 0.001f
                ? spikeVelocity.normalized
                : transform.forward;
            Vector3 knockback = direction * spikeVelocity.magnitude;
            Vector3 torque = HitData.ComputeRandomTorque(knockback.magnitude);
            hit = new HitData(knockback, contactPoint, torque, EHitResponse.Default);
        }

        hitDetector.ApplyExternalHit(hit);
    }

    private void IgnoreCollisionWith(Collider playerCollider)
    {
        if (_collider == null || playerCollider == null) return;

        var playerColliders = playerCollider.GetComponentInParent<HitDetector>()
            ?.GetComponentsInChildren<Collider>(true);
        if (playerColliders == null) return;

        foreach (var pc in playerColliders)
        {
            if (pc == null) continue;
            Physics.IgnoreCollision(_collider, pc, true);
            _ignoredPairs.Add((_collider, pc));
        }
    }

    private void RestoreIgnoredCollisions()
    {
        foreach (var (mine, player) in _ignoredPairs)
        {
            if (mine != null && player != null)
                Physics.IgnoreCollision(mine, player, false);
        }
        _ignoredPairs.Clear();
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

        // Detection radius.
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
#endif
}
