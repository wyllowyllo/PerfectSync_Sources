using System;
using System.Collections;
using InGame.Player.Animation;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    [RequireComponent(typeof(RagdollPhysicsToggle), typeof(RagdollRecovery))]
    public class RagdollController : MonoBehaviour, IRagdoll
    {
        [Header("Thresholds")]
        [SerializeField] private float _stumbleThreshold = 4f;
        [SerializeField] private float _ragdollThreshold = 8f;

        [Header("Stumble")]
        [SerializeField] private float _minStumbleDuration = 0.3f;
        [SerializeField] private float _maxStumbleDuration = 0.8f;
        [SerializeField] private float _stumbleDurationPerImpulse = 0.08f;
        [SerializeField] private float _stumblePushMultiplier = 0.3f;
        [SerializeField] private float _stumbleWobbleMultiplier = 2f;

        [Header("Ragdoll")]
        [SerializeField] private float _minRagdollDuration = 0.5f;
        [SerializeField] private float _maxRagdollDuration = 3.0f;
        [SerializeField] private float _settleVelocity = 0.5f;
        [SerializeField] private float _impactRadius = 2.0f;

        [Header("Recovery")]
        [SerializeField, Range(0f, 1f)] private float _reImpactMultiplier = 0.6f;

        [Header("Instability")]
        [SerializeField] private float _instabilityDecayRate = 4f;

        private RagdollPhysicsToggle _physicsToggle;
        private RagdollRecovery _recovery;
        private UpperBodyPhysics _upperBodyPhysics;
        private PlayerAnimation _animation;
        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactTransfer _impactTransfer;
        private Coroutine _activeCoroutine;
        private float _instability;

        public ERagdollState CurrentState => _currentState;
        public bool IsRagdollActive => _currentState != ERagdollState.Animated;

        /// <summary>
        /// 래그돌 뼈가 물리를 구동 중인지 (캡슐이 kinematic).
        /// Stumble은 캡슐이 여전히 활성이므로 포함하지 않음.
        /// </summary>
        public bool IsPhysicsRagdoll => _currentState == ERagdollState.Ragdoll ||
                                        _currentState == ERagdollState.BlendToAnim ||
                                        _currentState == ERagdollState.Dead;

        public event Action<Vector3, Quaternion, bool> OnRecoveryDataReady;

        private void Awake()
        {
            _physicsToggle = GetComponent<RagdollPhysicsToggle>();
            _recovery = GetComponent<RagdollRecovery>();
            _upperBodyPhysics = GetComponent<UpperBodyPhysics>();
            _animation = GetComponent<PlayerAnimation>();
        }

        private void Start()
        {
            _impactTransfer = new RagdollImpactTransfer(_physicsToggle.RagdollRigidbodies, _impactRadius);
        }

        private void Update()
        {
            if (_instability > 0f)
                _instability = Mathf.Max(0f, _instability - _instabilityDecayRate * Time.deltaTime);
        }

        public void OnHitImpact(Vector3 impulse, Vector3 hitPoint, Vector3 torqueVector)
        {
            var impact = new ImpactData(impulse, hitPoint);
            float effectiveMagnitude = impact.Magnitude + _instability;

            // BlendToAnim 중 재충격 → 래그돌 복귀 (낮은 임계값)
            if (_currentState == ERagdollState.BlendToAnim &&
                effectiveMagnitude >= _ragdollThreshold * _reImpactMultiplier)
            {
                _recovery.CancelBlending();
                EnterRagdoll(impact, torqueVector);
                return;
            }

            // 래그돌 중 추가 충격 → 타이머 리셋 + 추가 힘
            if (_currentState == ERagdollState.Ragdoll)
            {
                StopActiveCoroutine();
                _impactTransfer.ApplyAdditionalImpact(impact, torqueVector);
                _activeCoroutine = StartCoroutine(RecoveryCoroutine());
                return;
            }

            // 풀 래그돌 임계값
            if (effectiveMagnitude >= _ragdollThreshold)
            {
                EnterRagdoll(impact, torqueVector);
                return;
            }

            // Stumble 임계값
            if (impact.Magnitude >= _stumbleThreshold && _currentState != ERagdollState.Dead)
            {
                EnterStumble(impact);
                return;
            }

            // 약한 충격 → 상체 흔들림
            if (_upperBodyPhysics != null && _currentState == ERagdollState.Animated)
                _upperBodyPhysics.AddImpulse(impulse);
        }

        #region Stumble

        private void EnterStumble(ImpactData impact)
        {
            StopActiveCoroutine();
            _currentState = ERagdollState.Stumble;
            _instability += impact.Magnitude;

            // 캡슐에 밀림 적용
            Rigidbody capsuleRb = _physicsToggle.CapsuleRigidbody;
            Vector3 pushDir = impact.Impulse.normalized;
            capsuleRb.AddForce(pushDir * impact.Magnitude * _stumblePushMultiplier, ForceMode.Impulse);

            // 상체 강한 흔들림
            if (_upperBodyPhysics != null)
                _upperBodyPhysics.AddImpulse(impact.Impulse * _stumbleWobbleMultiplier);

            _animation.Stumble();

            float duration = Mathf.Clamp(
                impact.Magnitude * _stumbleDurationPerImpulse,
                _minStumbleDuration,
                _maxStumbleDuration);
            _activeCoroutine = StartCoroutine(StumbleCoroutine(duration));
        }

        private IEnumerator StumbleCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            if (_currentState == ERagdollState.Stumble)
            {
                _currentState = ERagdollState.Animated;
                _animation.ClearStumbleState();
            }

            _activeCoroutine = null;
        }

        #endregion

        #region Ragdoll

        private void EnterRagdoll(ImpactData impact, Vector3 torqueVector)
        {
            ERagdollState prevState = _currentState;
            StopActiveCoroutine();

            _currentState = ERagdollState.Ragdoll;
            _instability = 0f;

            _recovery.StartRagdollOverride(_physicsToggle.Bones);

            // BlendToAnim에서 재진입 시 RB를 현재 본 위치로 동기화
            if (prevState == ERagdollState.BlendToAnim)
                SyncRagdollRbsToBones();

            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactTransfer.TransferImpact(impact, inheritedVelocity, torqueVector);

            SetUpperBodyActive(false);
            _activeCoroutine = StartCoroutine(RecoveryCoroutine());
        }

        private IEnumerator RecoveryCoroutine()
        {
            yield return new WaitForSeconds(_minRagdollDuration);

            // 속도 기반 정지 감지 (최대 시간까지)
            float elapsed = _minRagdollDuration;
            while (elapsed < _maxRagdollDuration && !IsRagdollSettled())
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            if (_currentState == ERagdollState.Ragdoll)
            {
                _currentState = ERagdollState.BlendToAnim;

                bool isFaceUp = _recovery.PrepareRecovery();
                OnRecoveryDataReady?.Invoke(transform.position, transform.rotation, isFaceUp);

                _physicsToggle.Deactivate();

                Rigidbody capsuleRb = _physicsToggle.CapsuleRigidbody;
                capsuleRb.linearVelocity = Vector3.zero;
                capsuleRb.angularVelocity = Vector3.zero;

                _animation.GetUp(isFaceUp);
                _recovery.StartBlending(OnRecoveryComplete);
            }

            _activeCoroutine = null;
        }

        private bool IsRagdollSettled()
        {
            var rbs = _physicsToggle.RagdollRigidbodies;

            // 힙(루트 뼈)의 수직 속도가 크면 아직 낙하 중
            if (Mathf.Abs(rbs[0].linearVelocity.y) > _settleVelocity)
                return false;

            float totalSqrSpeed = 0f;
            for (int i = 0; i < rbs.Count; i++)
                totalSqrSpeed += rbs[i].linearVelocity.sqrMagnitude;

            return totalSqrSpeed / rbs.Count < _settleVelocity * _settleVelocity;
        }

        private void SyncRagdollRbsToBones()
        {
            var bones = _physicsToggle.Bones;
            for (int i = 0; i < bones.Count; i++)
            {
                bones[i].Rb.position = bones[i].Transform.position;
                bones[i].Rb.rotation = bones[i].Transform.rotation;
            }
        }

        #endregion

        #region Recovery

        public void ApplyRemoteRecovery(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (_currentState != ERagdollState.Ragdoll) return;

            StopActiveCoroutine();
            _currentState = ERagdollState.BlendToAnim;

            _recovery.PrepareRecoveryWithOverride(rootPos, rootRot);
            _physicsToggle.Deactivate();

            var capsuleRb = _physicsToggle.CapsuleRigidbody;
            capsuleRb.linearVelocity = Vector3.zero;
            capsuleRb.angularVelocity = Vector3.zero;

            _animation.GetUp(isFaceUp);
            _recovery.StartBlending(OnRecoveryComplete);
        }

        private void OnRecoveryComplete()
        {
            _currentState = ERagdollState.Animated;
            _animation.ClearGetUpState();
            SetUpperBodyActive(true);
        }

        #endregion

        public void EnterDead()
        {
            StopActiveCoroutine();
            _currentState = ERagdollState.Dead;
            _recovery.StartRagdollOverride(_physicsToggle.Bones);
            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactTransfer.TransferImpact(new ImpactData(Vector3.zero, transform.position), inheritedVelocity, Vector3.zero);
            SetUpperBodyActive(false);
        }

        /// 래그돌/Stumble을 즉시 종료하고 Animated로 강제 회복.
        /// Dead 상태는 영구이므로 회복하지 않는다.
        public void ForceRecover()
        {
            if (_currentState == ERagdollState.Dead) return;

            ERagdollState prevState = _currentState;
            StopActiveCoroutine();
            _currentState = ERagdollState.Animated;
            _instability = 0f;

            if (prevState == ERagdollState.Ragdoll || prevState == ERagdollState.BlendToAnim)
            {
                _recovery.CancelBlending();
                _physicsToggle.Deactivate();
            }

            var capsuleRb = _physicsToggle.CapsuleRigidbody;
            capsuleRb.linearVelocity = Vector3.zero;
            capsuleRb.angularVelocity = Vector3.zero;

            _animation.ClearGetUpState();
            _animation.ClearStumbleState();
            SetUpperBodyActive(true);
        }

        private void SetUpperBodyActive(bool active)
        {
            if (_upperBodyPhysics != null)
                _upperBodyPhysics.SetActive(active);
        }

        private void StopActiveCoroutine()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
        }
    }
}
