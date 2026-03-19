using UnityEngine;

namespace PlayerSystem.Ragdoll
{
    public class UpperBodyPhysics : MonoBehaviour
    {
        [SerializeField] private Transform _spineTransform;
        [SerializeField] private float _spring = 80f;
        [SerializeField] private float _damper = 8f;
        [SerializeField] private float _impulseMultiplier = 3f;

        private Rigidbody _spineRb;
        private Vector3 _angularVelocity;
        private Quaternion _rotationOffset = Quaternion.identity;
        private bool _isActive = true;

        private void Awake()
        {
            _spineRb = _spineTransform.GetComponent<Rigidbody>();
        }

        private void LateUpdate()
        {
            if (!_isActive) return;

            _rotationOffset.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            if (axis.sqrMagnitude < 0.001f)
            {
                axis = Vector3.up;
                angle = 0f;
            }

            Vector3 springTorque = -(axis.normalized * (angle * Mathf.Deg2Rad)) * _spring;
            Vector3 dampTorque = -_angularVelocity * _damper;
            _angularVelocity += (springTorque + dampTorque) * Time.deltaTime;

            float speed = _angularVelocity.magnitude;
            if (speed > 0.001f)
            {
                Quaternion delta = Quaternion.AngleAxis(speed * Mathf.Rad2Deg * Time.deltaTime, _angularVelocity / speed);
                _rotationOffset = delta * _rotationOffset;
            }

            _spineTransform.localRotation *= _rotationOffset;
        }

        public void AddImpulse(Vector3 worldImpulse)
        {
            if (!_isActive) return;

            Vector3 localDir = _spineTransform.InverseTransformDirection(worldImpulse);
            _angularVelocity += new Vector3(-localDir.z, 0f, localDir.x) * _impulseMultiplier;
        }

        public void SetActive(bool active)
        {
            _isActive = active;

            if (!active)
            {
                if (_spineRb != null)
                    _spineRb.isKinematic = true;

                _angularVelocity = Vector3.zero;
                _rotationOffset = Quaternion.identity;
            }
        }
    }
}
