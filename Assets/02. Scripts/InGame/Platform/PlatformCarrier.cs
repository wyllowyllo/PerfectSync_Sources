using System.Collections.Generic;
using Core;
using UnityEngine;

namespace InGame.Race.Platform
{
    /// <summary>
    /// 이동/회전하는 플랫폼 위의 Rigidbody를 함께 운반.
    /// PlayerMovement 이후 실행되어 velocity 주입 방식으로 동작하며,
    /// 물리 보간(interpolation)과 자연스럽게 조화.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrderConstants.PlatformCarrier)]
    public class PlatformCarrier : MonoBehaviour
    {
        [Header("Centrifugal Force")]
        [SerializeField] private bool _applyCentrifugalForce = true;
        [SerializeField] private float _centrifugalMultiplier = 1f;

        private static readonly Dictionary<Rigidbody, PlatformCarrier> _riderToCarrier = new();

        private readonly List<Rigidbody> _riders = new();
        private Vector3 _prevPosition;
        private Quaternion _prevRotation;
        private Vector3 _angularVelocity;
        private Vector3 _currentPivot;

        /// <summary>
        /// 특정 Rigidbody가 현재 플랫폼 위에 있다면 해당 플랫폼의 회전 정보를 반환.
        /// BodyMovementSynchronizer 등 외부 시스템에서 원형 외삽에 활용.
        /// </summary>
        public static bool TryGetMotionForRider(Rigidbody rb, out Vector3 angularVelocity, out Vector3 pivot)
        {
            if (_riderToCarrier.TryGetValue(rb, out var carrier))
            {
                angularVelocity = carrier._angularVelocity;
                pivot = carrier._currentPivot;
                return true;
            }
            angularVelocity = Vector3.zero;
            pivot = Vector3.zero;
            return false;
        }

        private void OnEnable()
        {
            _prevPosition = transform.position;
            _prevRotation = transform.rotation;
        }

        private void OnDisable()
        {
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                if (_riders[i] != null)
                    _riderToCarrier.Remove(_riders[i]);
            }
            _riders.Clear();
        }

        private void FixedUpdate()
        {
            Vector3 curPos = transform.position;
            Quaternion curRot = transform.rotation;

            Vector3 deltaPos = curPos - _prevPosition;
            Quaternion deltaRot = curRot * Quaternion.Inverse(_prevRotation);

            bool hasMoved = deltaPos.sqrMagnitude > 1e-8f;
            bool hasRotated = Quaternion.Angle(deltaRot, Quaternion.identity) > 0.01f;

            // 각속도·피벗 캐싱 (OnPhotonSerializeView는 Update에서 호출되므로 FixedUpdate 값 사용).
            if (hasRotated)
            {
                deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
                _angularVelocity = axis * (angleDeg * Mathf.Deg2Rad / Time.fixedDeltaTime);
            }
            else
            {
                _angularVelocity = Vector3.zero;
            }
            _currentPivot = curPos;

            if (hasMoved || hasRotated)
                CarryRiders(deltaPos, deltaRot, hasRotated);

            _prevPosition = curPos;
            _prevRotation = curRot;
        }

        private void CarryRiders(Vector3 deltaPos, Quaternion deltaRot, bool hasRotated)
        {
            float dt = Time.fixedDeltaTime;

            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                if (_riders[i] == null)
                {
                    _riderToCarrier.Remove(_riders[i]);
                    _riders.RemoveAt(i);
                    continue;
                }

                Rigidbody rb = _riders[i];

                if (hasRotated)
                {
                    Vector3 offset = rb.position - _prevPosition;
                    Vector3 rotatedPos = deltaRot * offset + transform.position;
                    rb.linearVelocity += (rotatedPos - rb.position) / dt;

                    if (_applyCentrifugalForce)
                        ApplyCentrifugalForce(rb, deltaRot);
                }
                else
                {
                    rb.linearVelocity += deltaPos / dt;
                }
            }
        }

        private void ApplyCentrifugalForce(Rigidbody rb, Quaternion deltaRot)
        {
            deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
            float angularSpeed = angleDeg * Mathf.Deg2Rad / Time.fixedDeltaTime;

            Vector3 toRider = rb.position - transform.position;
            Vector3 radial = toRider - Vector3.Project(toRider, axis);
            float radius = radial.magnitude;

            if (radius < 0.01f) return;

            // F = m * w^2 * r
            float force = rb.mass * angularSpeed * angularSpeed * radius * _centrifugalMultiplier;
            rb.AddForce(radial.normalized * force, ForceMode.Force);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Rigidbody rb = collision.rigidbody;
            if (rb != null && !rb.isKinematic && !_riders.Contains(rb))
            {
                _riders.Add(rb);
                _riderToCarrier[rb] = this;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            Rigidbody rb = collision.rigidbody;
            if (rb != null)
            {
                _riders.Remove(rb);
                _riderToCarrier.Remove(rb);
            }
        }
    }
}
