using System;
using System.Collections.Generic;
using UnityEngine;

namespace Player.Ragdoll
{
    public class RagdollRecovery : MonoBehaviour
    {
        private const float RayOriginUpOffset = 0.5f;
        private const float MinDirectionSqrMagnitude = 0.001f;

        [SerializeField] private float _blendDuration = 0.5f;
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        private IReadOnlyList<Bone> _boneRbPairs;
        private Action _onComplete;

        private Vector3[] _bonePositionSnapshot;
        private Quaternion[] _boneRotationSnapshot;
        private float _blendTimer;
        private bool _isBlending;
        private bool _isOverridingRagdoll;

        public bool IsRecovering => _isBlending;

        // 래그돌 진입 시 호출. 매 LateUpdate에서 RB→본 복사 시작.
        public void StartRagdollOverride(IReadOnlyList<Bone> boneRbPairs)
        {
            _boneRbPairs = boneRbPairs;
            _isOverridingRagdoll = true;
            _isBlending = false;
        }

        // 복구 준비. RB에서 스냅샷 캡처, transform.position으로 루트 정렬, isFaceUp 반환.
        // Deactivate() 이전에 호출할 것.
        public bool PrepareRecovery()
        {
            CaptureSnapshot();
            AlignRootToPose();

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

        private void CaptureSnapshot()
        {
            _bonePositionSnapshot = new Vector3[_boneRbPairs.Count];
            _boneRotationSnapshot = new Quaternion[_boneRbPairs.Count];

            for (int i = 0; i < _boneRbPairs.Count; i++)
            {
                _bonePositionSnapshot[i] = _boneRbPairs[i].Rb.position;
                _boneRotationSnapshot[i] = _boneRbPairs[i].Rb.rotation;
            }
        }

        private void AlignRootToPose()
        {
            // 캡슐 위치를 힙 스냅샷 기준으로 보정.
            Vector3 hipsPos = _bonePositionSnapshot[0];
            float groundY = GetGroundY(hipsPos);
            transform.position = new Vector3(hipsPos.x, groundY, hipsPos.z);

            // 루트 회전을 힙 스냅샷 기준으로 즉시 적용.
            Vector3 hipsForward = _boneRotationSnapshot[0] * Vector3.forward;
            hipsForward.y = 0f;

            if (hipsForward.sqrMagnitude > MinDirectionSqrMagnitude)
                transform.rotation = Quaternion.LookRotation(hipsForward);
        }

        private void LateUpdate()
        {
            // 래그돌 오버라이드: RB 포즈로 본 덮어쓰기.
            if (_isOverridingRagdoll)
            {
                for (int i = 0; i < _boneRbPairs.Count; i++)
                {
                    _boneRbPairs[i].Transform.position = _boneRbPairs[i].Rb.position;
                    _boneRbPairs[i].Transform.rotation = _boneRbPairs[i].Rb.rotation;
                }

                return;
            }

            // 래그돌 스냅샷 → GetUp 애니메이션 포즈 블렌딩.
            if (!_isBlending) return;

            _blendTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_blendTimer / _blendDuration);

            if (t < 1f)
            {
                for (int i = 0; i < _boneRbPairs.Count; i++)
                {
                    Transform bone = _boneRbPairs[i].Transform;
                    bone.position = Vector3.Lerp(_bonePositionSnapshot[i], bone.position, t);
                    bone.rotation = Quaternion.Slerp(_boneRotationSnapshot[i], bone.rotation, t);
                }

                return;
            }
            _isBlending = false;
            _onComplete?.Invoke();
        }

        private float GetGroundY(Vector3 origin)
        {
            Vector3 rayOrigin = origin + Vector3.up * RayOriginUpOffset;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundLayer))
                return hit.point.y;

            return origin.y;
        }
    }
}
