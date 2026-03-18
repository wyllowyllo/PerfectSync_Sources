using System;
using UnityEngine;

namespace Player.Ragdoll
{
    public class RagdollRecovery : MonoBehaviour
    {
        [SerializeField] private float _blendDuration = 0.5f;
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        private Transform[] _bones;
        private Rigidbody[] _ragdollRbs;
        private Action _onComplete;

        private Vector3[] _bonePositionSnapshot;
        private Quaternion[] _boneRotationSnapshot;
        private float _blendTimer;
        private bool _isBlending;
        private bool _isOverridingRagdoll;

        public bool IsRecovering => _isBlending;

        // 래그돌 진입 시 호출. 매 LateUpdate에서 RB→본 복사 시작.
        public void StartRagdollOverride(Transform[] bones, Rigidbody[] ragdollRbs)
        {
            _bones = bones;
            _ragdollRbs = ragdollRbs;
            _isOverridingRagdoll = true;
            _isBlending = false;
        }

        // 복구 준비. RB에서 스냅샷 캡처, transform.position으로 루트 정렬, isFaceUp 반환.
        // Deactivate() 이전에 호출할 것.
        public bool PrepareRecovery()
        {
            _bonePositionSnapshot = new Vector3[_bones.Length];
            _boneRotationSnapshot = new Quaternion[_bones.Length];

            for (int i = 0; i < _bones.Length; i++)
            {
                _bonePositionSnapshot[i] = _ragdollRbs[i].position;
                _boneRotationSnapshot[i] = _ragdollRbs[i].rotation;
            }

            // 캡슐 위치를 힙 스냅샷 기준으로 보정.
            Vector3 hipsPos = _bonePositionSnapshot[0];
            float groundY = GetGroundY(hipsPos);
            transform.position = new Vector3(hipsPos.x, groundY, hipsPos.z);

            // 루트 회전을 힙 스냅샷 기준으로 즉시 적용.
            Vector3 hipsForward = _boneRotationSnapshot[0] * Vector3.forward;
            hipsForward.y = 0f;

            if (hipsForward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(hipsForward);

            _isOverridingRagdoll = false;

            // 스냅샷 rotation에서 faceUp 판별.
            bool isFaceUp = (_boneRotationSnapshot[0] * Vector3.forward).y > 0f;
            return isFaceUp;
        }

        // Deactivate() + PlayGetUp() 이후 호출. 블렌드 시작.
        public void StartBlending(Action onComplete)
        {
            _onComplete = onComplete;
            _blendTimer = 0f;
            _isBlending = true;
        }

        private void LateUpdate()
        {
            // 래그돌 오버라이드: RB 포즈로 본 덮어쓰기.
            if (_isOverridingRagdoll)
            {
                for (int i = 0; i < _bones.Length; i++)
                {
                    _bones[i].position = _ragdollRbs[i].position;
                    _bones[i].rotation = _ragdollRbs[i].rotation;
                }

                return;
            }

            // 래그돌 스냅샷 → GetUp 애니메이션 포즈 블렌딩.
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
