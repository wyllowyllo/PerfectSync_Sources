using System.Collections;
using Player.Controller;
using Player.Controller.Ability;
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

        private RagdollPhysicsToggle _physicsToggle;
        private RagdollRecovery _recovery;
        private UpperBodyPhysics _upperBodyPhysics;
        private JumpAbility _jumpAbility;
        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactTransfer _impactTransfer;
        private Coroutine _activeCoroutine;

        public ERagdollState CurrentState => _currentState;

        private void Awake()
        {
            _physicsToggle = GetComponent<RagdollPhysicsToggle>();
            _recovery = GetComponent<RagdollRecovery>();
            _upperBodyPhysics = GetComponent<UpperBodyPhysics>();
            _jumpAbility = GetComponent<JumpAbility>();
            _impactTransfer = new RagdollImpactTransfer(_physicsToggle.RagdollRigidbodies);
            _recovery.Initialize(_physicsToggle.RagdollRigidbodies);
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
            EnterRagdoll(impact);
        }

        private void EnterRagdoll(ImpactData impact)
        {
            StopActiveCoroutine();

            _currentState = ERagdollState.Ragdoll;

            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactTransfer.TransferImpact(impact, inheritedVelocity);

            float duration = Mathf.Clamp(impact.Magnitude * _durationPerImpulse, _minRagdollDuration, _maxRagdollDuration);
            _activeCoroutine = StartCoroutine(WaitThenRecover(duration));
        }

        private IEnumerator WaitThenRecover(float duration)
        {
            yield return new WaitForSeconds(duration);

            if (_currentState == ERagdollState.Ragdoll)
            {
                _currentState = ERagdollState.BlendToAnim;

                // 물리 유지한 채 스프링 복귀 시작.
                if (_upperBodyPhysics != null)
                    _upperBodyPhysics.SetActive(false);

                _recovery.StartRecovery(_physicsToggle.CapsuleRigidbody, OnRecoveryComplete);
            }

            _activeCoroutine = null;
        }

        private void OnRecoveryComplete()
        {
            // 스프링 복귀 완료 → 래그돌 물리 비활성화.
            _physicsToggle.Deactivate();
            _currentState = ERagdollState.Animated;
            _jumpAbility.ResetState();

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
