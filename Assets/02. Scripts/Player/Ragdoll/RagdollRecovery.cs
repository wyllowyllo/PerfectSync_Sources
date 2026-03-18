using System;
using UnityEngine;

namespace Player.Ragdoll
{
    public class RagdollRecovery : MonoBehaviour
    {
        [SerializeField] private float _blendDuration = 0.3f;
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        private Transform[] _bones;
        private Animator _animator;
        private Action _onComplete;

        private Vector3[] _bonePositionSnapshot;
        private Quaternion[] _boneRotationSnapshot;
        private float _blendTimer;
        private bool _isBlending;

        public bool IsRecovering => _isBlending;

        public void StartRecovery(Transform[] bones, Animator animator, Rigidbody capsuleRb, Action onComplete)
        {
            _bones = bones;
            _animator = animator;
            _onComplete = onComplete;

            _bonePositionSnapshot = new Vector3[_bones.Length];
            _boneRotationSnapshot = new Quaternion[_bones.Length];

            for (int i = 0; i < _bones.Length; i++)
            {
                _bonePositionSnapshot[i] = _bones[i].position;
                _boneRotationSnapshot[i] = _bones[i].rotation;
            }

            // 캡슐 위치를 힙 위치 기준으로 보정.
            Vector3 hipsPos = _bones[0].position;
            float groundY = GetGroundY(hipsPos);
            capsuleRb.position = new Vector3(hipsPos.x, groundY, hipsPos.z);

            Vector3 hipsForward = _bones[0].rotation * Vector3.forward;
            hipsForward.y = 0f;
            if (hipsForward.sqrMagnitude > 0.001f)
                capsuleRb.rotation = Quaternion.LookRotation(hipsForward);

            // 잔여 속도 제거.
            capsuleRb.linearVelocity = Vector3.zero;
            capsuleRb.angularVelocity = Vector3.zero;

            _blendTimer = 0f;
            _isBlending = true;
        }

        private void LateUpdate()
        {
            if (!_isBlending) return;

            _blendTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_blendTimer / _blendDuration);

            if (t < 1f)
            {
                for (int i = 0; i < _bones.Length; i++)
                {
                    _bones[i].position = Vector3.Lerp(_bonePositionSnapshot[i], _bones[i].position, t);
                    _bones[i].rotation = Quaternion.Slerp(_boneRotationSnapshot[i], _bones[i].rotation, t);
                }
                return;
            }

            _isBlending = false;
            _onComplete?.Invoke();
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
