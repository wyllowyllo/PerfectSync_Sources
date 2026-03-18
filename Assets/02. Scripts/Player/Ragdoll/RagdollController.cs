using System.Collections;
using Player.Controller;
using UnityEngine;

namespace Player.Ragdoll
{
    [RequireComponent(typeof(RagdollPhysicsToggle), typeof(RagdollRecovery))]
    public class RagdollController : MonoBehaviour, IRagdoll
    {
        [Header("Ragdoll Settings")]
        [SerializeField] private float _ragdollThreshold = 8f;
        [SerializeField] private float _minRagdollDuration = 0.5f;
        [SerializeField] private float _maxRagdollDuration = 2.0f;
        [SerializeField] private float _durationPerImpulse = 0.1f;
        [SerializeField] private float _diveRagdollDuration = 0.15f;

        private RagdollPhysicsToggle _physicsToggle;
        private RagdollRecovery _recovery;
        private UpperBodyPhysics _upperBodyPhysics;
        private PlayerAnimation anim;
        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactTransfer _impactTransfer;
        private Coroutine _activeCoroutine;

        public ERagdollState CurrentState => _currentState;

        private void Awake()
        {
            _physicsToggle = GetComponent<RagdollPhysicsToggle>();
            _recovery = GetComponent<RagdollRecovery>();
            _upperBodyPhysics = GetComponent<UpperBodyPhysics>();
            anim = GetComponent<PlayerAnimation>();
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

        public void ForceRagdoll()
        {
            var impulse = _physicsToggle.CapsuleRigidbody.linearVelocity.normalized * _ragdollThreshold;
            var impact = new ImpactData(impulse, _physicsToggle.CapsuleRigidbody.position);
            EnterRagdoll(impact, _diveRagdollDuration);
        }

        private void EnterRagdoll(ImpactData impact, float? overrideDuration = null)
        {
            StopActiveCoroutine();

            _currentState = ERagdollState.Ragdoll;

            _recovery.StartRagdollOverride(_physicsToggle.RagdollBones, _physicsToggle.RagdollRigidbodies);

            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactTransfer.TransferImpact(impact, inheritedVelocity);

            if (_upperBodyPhysics != null)
                _upperBodyPhysics.SetActive(false);

            float duration = overrideDuration ?? Mathf.Clamp(impact.Magnitude * _durationPerImpulse, _minRagdollDuration, _maxRagdollDuration);
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

                anim.PlayGetUp(isFaceUp);
                _recovery.StartBlending(OnRecoveryComplete);
            }

            _activeCoroutine = null;
        }

        private void OnRecoveryComplete()
        {
            _currentState = ERagdollState.Animated;
            anim.ClearGetUpState();

            if (_upperBodyPhysics != null)
                _upperBodyPhysics.SetActive(true);
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
