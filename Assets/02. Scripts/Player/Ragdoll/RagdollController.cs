using System.Collections;
using UnityEngine;

namespace Player.Ragdoll
{
    public class RagdollController : MonoBehaviour, IRagdollInput
    {
        [Header("References")]
        [SerializeField] private RagdollPhysicsToggle _physicsToggle;
        [SerializeField] private RagdollRecovery _recovery;
        [SerializeField] private UpperBodyPhysics _upperBodyPhysics;

        [Header("Ragdoll Settings")]
        [SerializeField] private float _ragdollThreshold = 8f;
        [SerializeField] private float _minRagdollDuration = 0.5f;
        [SerializeField] private float _maxRagdollDuration = 2.0f;
        [SerializeField] private float _durationPerImpulse = 0.1f;

        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactApplier _impactApplier;
        private Coroutine _activeCoroutine;

        public ERagdollState CurrentState => _currentState;

        private void Awake()
        {
            _impactApplier = new RagdollImpactApplier(_physicsToggle.RagdollRigidbodies);
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

            Vector3 inheritedVelocity = _physicsToggle.CapsuleRigidbody.linearVelocity;
            _physicsToggle.Activate();
            _impactApplier.Apply(impact, inheritedVelocity);

            float duration = Mathf.Clamp(
                impact.Magnitude * _durationPerImpulse,
                _minRagdollDuration,
                _maxRagdollDuration);
            _activeCoroutine = StartCoroutine(WaitThenRecover(duration));
        }

        private IEnumerator WaitThenRecover(float duration)
        {
            yield return new WaitForSeconds(duration);

            if (_currentState == ERagdollState.Ragdoll)
            {
                _currentState = ERagdollState.BlendToAnim;
                _physicsToggle.Deactivate();

                // BlendToAnim 동안 UpperBodyPhysics 비활성화.
                if (_upperBodyPhysics != null)
                    _upperBodyPhysics.SetActive(false);

                _recovery.StartRecovery(
                    _physicsToggle.RagdollBones,
                    _physicsToggle.Animator,
                    _physicsToggle.CapsuleRigidbody,
                    OnRecoveryComplete);
            }

            _activeCoroutine = null;
        }

        private void OnRecoveryComplete()
        {
            _currentState = ERagdollState.Animated;

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
