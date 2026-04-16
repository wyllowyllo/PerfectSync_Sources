using System.Collections.Generic;
using Core;
using UnityEngine;

namespace InGame.Race.Platform
{
    // 이동/회전하는 플랫폼 위의 Rigidbody를 함께 운반.
    // PlayerMovement 이후 실행되어 position 보정 방식으로 동작하며,
    // velocity 영역은 PlayerMovement에 위임하여 도메인 충돌을 방지.
    [DefaultExecutionOrder(ExecutionOrderConstants.PlatformCarrier)]
    public class PlatformCarrier : MonoBehaviour
    {
        [Header("Centrifugal Force")]
        [SerializeField] private bool _applyCentrifugalForce = true;
        [SerializeField] private float _centrifugalMultiplier = 1f;

        [Header("Carry Filter")]
        [SerializeField] private LayerMask _excludedLayers;

        private static readonly Dictionary<Rigidbody, PlatformCarrier> s_riderToCarrier = new();

        private readonly List<Rigidbody> _riders = new();
        private readonly Dictionary<Rigidbody, Vector3> _riderCarryVelocity = new();
        private Vector3 _prevPosition;
        private Quaternion _prevRotation;
        private Vector3 _angularVelocity;
        private Vector3 _currentPivot;

        #region Unity Lifecycle

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
                    s_riderToCarrier.Remove(_riders[i]);
            }
            _riders.Clear();
            _riderCarryVelocity.Clear();
        }

        private void FixedUpdate()
        {
            Vector3 curPos = transform.position;
            Quaternion curRot = transform.rotation;

            Vector3 deltaPos = curPos - _prevPosition;
            Quaternion deltaRot = curRot * Quaternion.Inverse(_prevRotation);

            bool hasMoved = deltaPos.sqrMagnitude > 1e-8f;
            bool hasRotated = Quaternion.Angle(deltaRot, Quaternion.identity) > 0.01f;

            // 각속도/피벗 캐싱 (OnPhotonSerializeView는 Update에서 호출되므로 FixedUpdate 값 사용).
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
            else
                ClearCarryVelocities();

            _prevPosition = curPos;
            _prevRotation = curRot;
        }

        #endregion

        #region Public Static API

        // 라이더의 플랫폼 회전 정보와 운반 속도를 반환. 네트워크 원형 외삽에 활용.
        public static bool TryGetMotionForRider(Rigidbody rb,
            out Vector3 angularVelocity, out Vector3 pivot, out Vector3 carryVelocity)
        {
            if (s_riderToCarrier.TryGetValue(rb, out var carrier))
            {
                angularVelocity = carrier._angularVelocity;
                pivot = carrier._currentPivot;
                carrier._riderCarryVelocity.TryGetValue(rb, out carryVelocity);
                return true;
            }
            angularVelocity = Vector3.zero;
            pivot = Vector3.zero;
            carryVelocity = Vector3.zero;
            return false;
        }

        // 외부에서 라이더를 강제 제거. 리스폰/텔레포트 시 팬텀 라이더 방지.
        public static void ForceRemoveRider(Rigidbody rb)
        {
            if (rb == null) return;
            if (!s_riderToCarrier.TryGetValue(rb, out var carrier)) return;

            carrier._riderCarryVelocity.Remove(rb);
            carrier._riders.Remove(rb);
            s_riderToCarrier.Remove(rb);
        }

        #endregion

        #region Private Methods

        private void CarryRiders(Vector3 deltaPos, Quaternion deltaRot, bool hasRotated)
        {
            float dt = Time.fixedDeltaTime;

            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                if (_riders[i] == null)
                {
                    s_riderToCarrier.Remove(_riders[i]);
                    _riders.RemoveAt(i);
                    continue;
                }

                Rigidbody rb = _riders[i];
                if (rb.isKinematic) continue;

                Vector3 prevPos = rb.position;

                if (hasRotated)
                {
                    Vector3 offset = rb.position - _prevPosition;
                    rb.position = deltaRot * offset + transform.position;
                }
                else
                {
                    rb.position += deltaPos;
                }

                // 네트워크 동기화용 운반 속도 기록 (물리에는 미적용).
                _riderCarryVelocity[rb] = (rb.position - prevPos) / dt;

                if (hasRotated && _applyCentrifugalForce)
                    ApplyCentrifugalForce(rb, deltaRot);
            }
        }

        private void ClearCarryVelocities()
        {
            foreach (var rb in _riders)
            {
                if (rb != null)
                    _riderCarryVelocity[rb] = Vector3.zero;
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

            float force = rb.mass * angularSpeed * angularSpeed * radius * _centrifugalMultiplier;
            rb.AddForce(radial.normalized * force, ForceMode.Force);
        }

        #endregion

        #region Collision Callbacks

        private void OnCollisionEnter(Collision collision)
        {
            Rigidbody rb = collision.rigidbody;
            if (rb == null || rb.isKinematic || _riders.Contains(rb)) return;
            if ((_excludedLayers & (1 << collision.gameObject.layer)) != 0) return;

            _riders.Add(rb);
            s_riderToCarrier[rb] = this;
        }

        private void OnCollisionExit(Collision collision)
        {
            Rigidbody rb = collision.rigidbody;
            if (rb != null && _riders.Remove(rb))
            {
                // 이탈 시 플랫폼 관성 전달.
                if (_riderCarryVelocity.TryGetValue(rb, out var carry) && !rb.isKinematic)
                    rb.linearVelocity += carry;

                _riderCarryVelocity.Remove(rb);
                if (s_riderToCarrier.TryGetValue(rb, out var c) && c == this)
                    s_riderToCarrier.Remove(rb);
            }
        }

        #endregion
    }
}
