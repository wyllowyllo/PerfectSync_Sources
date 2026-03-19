using System.Collections;
using InGame.Player._02._Domain.Animation;
using InGame.Player._02._Domain.Data;
using InGame.Player._02._Domain.Ragdoll;
using UnityEngine;

namespace InGame.Player._03._Manager
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

        private void Awake()
        {
            _physicsToggle = GetComponent<RagdollPhysicsToggle>();
            _recovery = GetComponent<RagdollRecovery>();
            _upperBodyPhysics = GetComponent<UpperBodyPhysics>();
            _animation = GetComponent<PlayerAnimation>();
            _impactTransfer = new RagdollImpactTransfer(_physicsToggle.RagdollRigidbodies);
        }

        public void OnHitImpact(Vector3 impulse, Vector3 hitPoint)
        {
            var impact = new ImpactData(impulse, hitPoint);

            if (impact.Magnitude >= _ragdollThreshold)
            {
                EnterRagdoll(impact);
                return;
            }

            if (_upperBodyPhysics != null)
                _upperBodyPhysics.AddImpulse(impulse);
        }

        private void EnterRagdoll(ImpactData impact)
        {
            StopActiveCoroutine();

            _currentState = ERagdollState.Ragdoll;

            _recovery.StartRagdollOverride(_physicsToggle.Bones);

            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactTransfer.TransferImpact(impact, inheritedVelocity);

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

                // Deactivate 전에 RB에서 스냅샷 캡처 + 루트 정렬.
                bool isFaceUp = _recovery.PrepareRecovery();

                _physicsToggle.Deactivate();

                Rigidbody capsuleRb = _physicsToggle.CapsuleRigidbody;
                capsuleRb.linearVelocity = Vector3.zero;
                capsuleRb.angularVelocity = Vector3.zero;

                _animation.GetUp(isFaceUp);
                _recovery.StartBlending(OnRecoveryComplete);
            }

            _activeCoroutine = null;
        }

        public void EnterDead()
        {
            StopActiveCoroutine();
            _currentState = ERagdollState.Dead;
            _recovery.StartRagdollOverride(_physicsToggle.Bones);
            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactTransfer.TransferImpact(new ImpactData(Vector3.zero, transform.position), inheritedVelocity);
            SetUpperBodyActive(false);
            // RecoveryCoroutine 시작 안 함 — Dead는 영구 유지.
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
