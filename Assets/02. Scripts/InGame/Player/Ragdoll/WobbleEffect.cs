using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class WobbleEffect : MonoBehaviour
    {
        [SerializeField] private Transform _spineTransform;

        [Header("Spring-Damper (Impact Response)")]
        [SerializeField] private float _spring = 80f;
        [SerializeField] private float _damper = 8f;
        [SerializeField] private float _impulseMultiplier = 3f;

        [Header("Perlin Noise (Constant Wobble)")]
        [SerializeField] private float _noiseAmplitude = 0.5f;
        [SerializeField] private float _noiseFrequency = 2f;
        [SerializeField] private float _speedNoiseScale = 0.3f;

        [Header("Safety")]
        [SerializeField] private float _maxDeflectionAngle = 15f;

        private Vector3 _angularVelocity;
        private Quaternion _rotationOffset = Quaternion.identity;
        private bool _isActive = true;
        private float _locomotionSpeed;
        private float _noiseTimeX;
        private float _noiseTimeZ;

        private void Awake()
        {
            _noiseTimeX = Random.Range(0f, 100f);
            _noiseTimeZ = Random.Range(100f, 200f);
        }

        private void LateUpdate()
        {
            if (!_isActive) return;

            ApplySpringDamper();
            ApplyNoise();
            ClampDeflection();
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
                _angularVelocity = Vector3.zero;
                _rotationOffset = Quaternion.identity;
            }
        }

        public void SetLocomotionSpeed(float speed)
        {
            _locomotionSpeed = speed;
        }

        private void ApplySpringDamper()
        {
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
                Quaternion delta = Quaternion.AngleAxis(
                    speed * Mathf.Rad2Deg * Time.deltaTime,
                    _angularVelocity / speed);
                _rotationOffset = delta * _rotationOffset;
            }
        }

        private void ApplyNoise()
        {
            float noiseScale = _noiseAmplitude + _locomotionSpeed * _speedNoiseScale;
            if (noiseScale < 0.001f) return;

            float dt = Time.deltaTime * _noiseFrequency;
            _noiseTimeX += dt;
            _noiseTimeZ += dt;

            // Perlin noise 기반 미세 흔들림. -1~1 범위로 변환.
            float noiseX = (Mathf.PerlinNoise(_noiseTimeX, 0f) - 0.5f) * 2f * noiseScale;
            float noiseZ = (Mathf.PerlinNoise(_noiseTimeZ, 0f) - 0.5f) * 2f * noiseScale;

            _angularVelocity.x += noiseX * Time.deltaTime;
            _angularVelocity.z += noiseZ * Time.deltaTime;
        }

        private void ClampDeflection()
        {
            _rotationOffset.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            if (Mathf.Abs(angle) > _maxDeflectionAngle)
            {
                _rotationOffset = Quaternion.AngleAxis(
                    Mathf.Sign(angle) * _maxDeflectionAngle,
                    axis.sqrMagnitude > 0.001f ? axis.normalized : Vector3.up);
            }
        }
    }
}
