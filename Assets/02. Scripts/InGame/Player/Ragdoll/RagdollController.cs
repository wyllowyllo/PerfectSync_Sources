using System;
using System.Collections;
using InGame.Player.Animation;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    [RequireComponent(typeof(RagdollPhysicsToggle), typeof(RagdollRecovery))]
    public class RagdollController : MonoBehaviour, IRagdoll
    {
        [Header("Ragdoll Settings")]
        [SerializeField] private float _ragdollThreshold = 8f;
        [SerializeField] private float _minRagdollDuration = 0.5f;
        [SerializeField] private float _maxRagdollDuration = 2.0f;
        [SerializeField] private float _durationPerImpulse = 0.1f;

        private RagdollPhysicsToggle _physicsToggle;
        private RagdollRecovery _recovery;
        private UpperBodyPhysics _upperBodyPhysics;
        private PlayerAnimation _animation;
        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactTransfer _impactTransfer;
        private Coroutine _activeCoroutine;

        public ERagdollState CurrentState => _currentState;
        public bool IsRagdollActive => _currentState != ERagdollState.Animated;

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
            _impactTransfer = new RagdollImpactTransfer(_physicsToggle.RagdollRigidbodies);
        }

        public void OnHitImpact(Vector3 impulse, Vector3 hitPoint, Vector3 torqueVector)
        {
            var impact = new ImpactData(impulse, hitPoint);

            if (impact.Magnitude >= _ragdollThreshold)
            {
                EnterRagdoll(impact, torqueVector);
                return;
            }

            if (_upperBodyPhysics != null)
                _upperBodyPhysics.AddImpulse(impulse);
        }

        private void EnterRagdoll(ImpactData impact, Vector3 torqueVector)
        {
            StopActiveCoroutine();

            _currentState = ERagdollState.Ragdoll;

            _recovery.StartRagdollOverride(_physicsToggle.Bones);

            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactTransfer.TransferImpact(impact, inheritedVelocity, torqueVector);

            SetUpperBodyActive(false);

            float duration = Mathf.Clamp(impact.Magnitude * _durationPerImpulse, _minRagdollDuration, _maxRagdollDuration);
            _activeCoroutine = StartCoroutine(RecoveryCoroutine(duration));
        }

        private IEnumerator RecoveryCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);

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

        private void OnRecoveryComplete()
        {
            _currentState = ERagdollState.Animated;
            _animation.ClearGetUpState();
            SetUpperBodyActive(true);
        }

        private void SetUpperBodyActive(bool active)
        {
            if (_upperBodyPhysics != null)
                _upperBodyPhysics.SetActive(active);
        }

        /// 래그돌을 즉시 종료하고 Animated 상태로 강제 회복한다.
        /// 자연 회복(RecoveryCoroutine)과 달리 블렌딩/기상 애니메이션을 건너뛴다.
        /// Dead 상태는 영구 상태이므로 회복하지 않는다.
        public void ForceRecover()
        {
            if (_currentState == ERagdollState.Dead) return;
            StopActiveCoroutine();
            _currentState = ERagdollState.Animated;
            _physicsToggle.Deactivate();
            var capsuleRb = _physicsToggle.CapsuleRigidbody;
            capsuleRb.linearVelocity = Vector3.zero;
            capsuleRb.angularVelocity = Vector3.zero;
            _animation.ClearGetUpState();
            SetUpperBodyActive(true);
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
