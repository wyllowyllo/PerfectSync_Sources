using System;
using UnityEngine;

namespace Player.Ragdoll
{
    public class RagdollRecovery : MonoBehaviour
    {
        [SerializeField] private float _blendDuration = 0.5f;
        [SerializeField] private float _mecanimTransitionWaitTime = 0.05f;
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        private Transform[] _bones;
        private Animator _animator;
        private Action _onComplete;

        private Vector3[] _bonePositionSnapshot;
        private Quaternion[] _boneRotationSnapshot;
        private float _blendTimer;
        private bool _isBlending;
        private bool _waitingForMecanim;
        private float _mecanimWaitTimer;

        public bool IsRecovering => _isBlending || _waitingForMecanim;

        public bool DetectFaceUp(Transform hipsRoot)
        {
            return hipsRoot.forward.y > 0f;
        }

        public void StartRecovery(Transform[] bones, Animator animator, Rigidbody capsuleRb, Transform hipsRoot, Action onComplete)
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

            Vector3 hipsForward = hipsRoot.rotation * Vector3.forward;
            hipsForward.y = 0f;
            if (hipsForward.sqrMagnitude > 0.001f)
                capsuleRb.rotation = Quaternion.LookRotation(hipsForward);

            // 잔여 속도 제거.
            capsuleRb.linearVelocity = Vector3.zero;
            capsuleRb.angularVelocity = Vector3.zero;

            _blendTimer = 0f;
            _isBlending = false;
            _waitingForMecanim = true;
            _mecanimWaitTimer = 0f;
        }

        private void LateUpdate()
        {
            // Phase 1: Mecanim 전환 대기 — 스냅샷 포즈 고정.
            if (_waitingForMecanim)
            {
                _mecanimWaitTimer += Time.deltaTime;

                for (int i = 0; i < _bones.Length; i++)
                {
                    _bones[i].position = _bonePositionSnapshot[i];
                    _bones[i].rotation = _boneRotationSnapshot[i];
                }

                if (_mecanimWaitTimer >= _mecanimTransitionWaitTime)
                {
                    _waitingForMecanim = false;
                    _isBlending = true;
                }

                return;
            }

            // Phase 2: 래그돌 스냅샷 → GetUp 애니메이션 포즈 블렌딩.
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
