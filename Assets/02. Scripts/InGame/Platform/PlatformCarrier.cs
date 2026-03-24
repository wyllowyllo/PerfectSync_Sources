using System.Collections.Generic;
using UnityEngine;

namespace InGame.Race.Platform
{
    /// <summary>
    /// 이동/회전하는 플랫폼 위의 Rigidbody를 함께 운반.
    /// RotationScript 등 어떤 이동/회전 방식과도 조합 가능.
    /// 플랫폼의 기존 Collider로 충돌 감지 (Trigger 불필요).
    /// </summary>
    [DefaultExecutionOrder(1)]
    public class PlatformCarrier : MonoBehaviour
    {
        private readonly List<Rigidbody> _riders = new();
        private Vector3 _prevPosition;
        private Quaternion _prevRotation;

        private void OnEnable()
        {
            _prevPosition = transform.position;
            _prevRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            Vector3 curPos = transform.position;
            Quaternion curRot = transform.rotation;

            Vector3 deltaPos = curPos - _prevPosition;
            Quaternion deltaRot = curRot * Quaternion.Inverse(_prevRotation);

            bool hasMoved = deltaPos.sqrMagnitude > 1e-8f;
            bool hasRotated = Quaternion.Angle(deltaRot, Quaternion.identity) > 0.01f;

            if (hasMoved || hasRotated)
                CarryRiders(deltaPos, deltaRot, hasRotated);

            _prevPosition = curPos;
            _prevRotation = curRot;
        }

        private void CarryRiders(Vector3 deltaPos, Quaternion deltaRot, bool hasRotated)
        {
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                if (_riders[i] == null)
                {
                    _riders.RemoveAt(i);
                    continue;
                }

                Rigidbody rb = _riders[i];

                if (hasRotated)
                {
                    Vector3 offset = rb.position - _prevPosition;
                    rb.position = deltaRot * offset + transform.position;
                    rb.MoveRotation(deltaRot * rb.rotation);
                }
                else
                {
                    rb.position += deltaPos;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            Rigidbody rb = collision.rigidbody;
            if (rb != null && !rb.isKinematic && !_riders.Contains(rb))
                _riders.Add(rb);
        }

        private void OnCollisionExit(Collision collision)
        {
            Rigidbody rb = collision.rigidbody;
            if (rb != null)
                _riders.Remove(rb);
        }
    }
}
