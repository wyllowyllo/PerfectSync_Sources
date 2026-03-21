using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class ActiveRagdollForce : MonoBehaviour
    {
        [SerializeField] private Rigidbody _pelvisRb;
        [SerializeField] private float _uprightTorqueStrength = 5f;
        [SerializeField] private float _dampingFactor = 2f;

        private bool _isActive;

        private void FixedUpdate()
        {
            if (!_isActive || _pelvisRb == null) return;

            // 현재 골반의 up 벡터와 월드 up의 차이만큼 기립 토크 적용.
            Vector3 currentUp = _pelvisRb.rotation * Vector3.up;
            Vector3 torqueAxis = Vector3.Cross(currentUp, Vector3.up);

            _pelvisRb.AddTorque(torqueAxis * _uprightTorqueStrength, ForceMode.Acceleration);
            _pelvisRb.AddTorque(-_pelvisRb.angularVelocity * _dampingFactor, ForceMode.Acceleration);
        }

        public void SetActive(bool active)
        {
            _isActive = active;
        }
    }
}
