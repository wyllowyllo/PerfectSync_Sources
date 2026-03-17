using System;
using System.Collections;
using UnityEngine;

namespace Player.Ragdoll
{
    public class RagdollRecovery : MonoBehaviour
    {
        [Header("Spring")]
        [SerializeField] private float _springForce = 150f;
        [SerializeField] private float _damping = 20f;

        [Header("Lift")]
        [SerializeField] private float _liftForce = 80f;

        [Header("Recovery")]
        [SerializeField] private float _recoveryDuration = 1.2f;
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        private Rigidbody[] _ragdollRbs;
        private Quaternion[] _targetLocalRotations;
        private float _standingHipsHeight;
        private Rigidbody _capsuleRb;
        private Action _onComplete;
        private float _recoveryTimer;
        private bool _isRecovering;
        private bool _initialized;

        public bool IsRecovering => _isRecovering;

        public void Initialize(Rigidbody[] ragdollRbs)
        {
            _ragdollRbs = ragdollRbs;
            StartCoroutine(CaptureTargetPoseAfterFrame());
        }

        private IEnumerator CaptureTargetPoseAfterFrame()
        {
            // Animator가 첫 프레임 평가를 완료할 때까지 대기.
            yield return null;

            _targetLocalRotations = new Quaternion[_ragdollRbs.Length];
            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                _targetLocalRotations[i] = _ragdollRbs[i].transform.localRotation;
            }

            // 서있을 때 힙 높이 저장 (지면 기준).
            Transform hips = _ragdollRbs[0].transform;
            float groundY = GetGroundY(hips.position);
            _standingHipsHeight = hips.position.y - groundY;

            _initialized = true;
        }

        public void StartRecovery(Rigidbody capsuleRb, Action onComplete)
        {
            _capsuleRb = capsuleRb;
            _onComplete = onComplete;
            _recoveryTimer = 0f;
            _isRecovering = true;

            SnapCapsuleToHips();
        }

        private void FixedUpdate()
        {
            if (!_isRecovering || !_initialized)
                return;

            _recoveryTimer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_recoveryTimer / _recoveryDuration);

            // 스프링 강도를 점진적으로 증가.
            float currentSpring = Mathf.Lerp(0f, _springForce, t);

            // 각 본에 스프링 토크 적용.
            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                if (_ragdollRbs[i] == null)
                    continue;

                ApplySpringTorque(_ragdollRbs[i], _targetLocalRotations[i], currentSpring);
            }

            // 힙을 서있는 높이로 끌어올림.
            ApplyHipsLift(currentSpring);

            // 캡슐을 힙 위치에 추적.
            SnapCapsuleToHips();

            if (t >= 1f)
            {
                _isRecovering = false;
                _onComplete?.Invoke();
            }
        }

        private void ApplySpringTorque(Rigidbody rb, Quaternion targetLocalRot, float spring)
        {
            Transform bone = rb.transform;
            Quaternion rotDiff = targetLocalRot * Quaternion.Inverse(bone.localRotation);

            rotDiff.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
                angle -= 360f;

            if (axis.sqrMagnitude < 0.001f || Mathf.Abs(angle) < 0.1f)
                return;

            // 로컬 축을 월드 스페이스로 변환.
            Vector3 worldAxis = bone.parent != null
                ? bone.parent.TransformDirection(axis.normalized)
                : axis.normalized;

            Vector3 torque = worldAxis * (angle * Mathf.Deg2Rad * spring)
                             - rb.angularVelocity * _damping;

            rb.AddTorque(torque, ForceMode.Acceleration);
        }

        private void ApplyHipsLift(float spring)
        {
            Rigidbody hipsRb = _ragdollRbs[0];
            Transform hips = hipsRb.transform;

            float groundY = GetGroundY(hips.position);
            float targetY = groundY + _standingHipsHeight;
            float heightDiff = targetY - hips.position.y;

            // 수직 스프링 힘.
            float liftAccel = heightDiff * _liftForce - hipsRb.linearVelocity.y * _damping;
            hipsRb.AddForce(Vector3.up * liftAccel, ForceMode.Acceleration);
        }

        private void SnapCapsuleToHips()
        {
            Transform hips = _ragdollRbs[0].transform;
            Vector3 hipsPos = hips.position;
            float groundY = GetGroundY(hipsPos);

            _capsuleRb.MovePosition(new Vector3(hipsPos.x, groundY, hipsPos.z));

            Vector3 hipsForward = hips.rotation * Vector3.forward;
            hipsForward.y = 0f;
            if (hipsForward.sqrMagnitude > 0.001f)
                _capsuleRb.MoveRotation(Quaternion.LookRotation(hipsForward));
        }

        private float GetGroundY(Vector3 origin)
        {
            Vector3 rayOrigin = origin + Vector3.up * 0.5f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundLayer))
                return hit.point.y;

            return origin.y;
        }
    }
}
