using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class RagdollBoneReceiver : MonoBehaviour, IRagdollBoneReceiver
    {
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private Rigidbody _rootBody;

        private RagdollBoneSnapshot _previousSnapshot;
        private RagdollBoneSnapshot _currentSnapshot;
        private float _lastReceiveTime;
        private float _receiveInterval;
        private bool _isActive;
        private bool _hasSnapshot;

        private const float DefaultReceiveInterval = 0.1f;
        private const float MaxInterpolationTime = 0.2f;

        public bool IsActive => _isActive;

        public void Activate()
        {
            _isActive = true;
            _hasSnapshot = false;
            _receiveInterval = DefaultReceiveInterval;

            _ragdollRig.ActivateKinematic();
        }

        public void Deactivate()
        {
            _isActive = false;
            _hasSnapshot = false;
        }

        public void ApplySnapshot(RagdollBoneSnapshot snapshot)
        {
            if (!_isActive) return;

            _previousSnapshot = _hasSnapshot ? _currentSnapshot : snapshot;
            _currentSnapshot = snapshot;

            float now = Time.time;
            if (_hasSnapshot)
            {
                float interval = now - _lastReceiveTime;
                if (interval > 0.001f)
                    _receiveInterval = Mathf.Lerp(_receiveInterval, interval, 0.5f);
            }

            _lastReceiveTime = now;
            _hasSnapshot = true;
        }

        private void LateUpdate()
        {
            if (!_isActive || !_hasSnapshot) return;

            IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;

            float elapsed = Time.time - _lastReceiveTime;
            float t = Mathf.Clamp01(
                elapsed / Mathf.Min(_receiveInterval, MaxInterpolationTime));

            int count = Mathf.Min(
                bones.Count,
                Mathf.Min(
                    _currentSnapshot.BonePositions.Length,
                    _previousSnapshot.BonePositions.Length));

            // 모든 본: world position + world rotation 보간.
            // 래그돌 물리는 각 Rigidbody를 독립적으로 이동시키므로
            // hierarchy 기반 local transform만으로는 정확한 재현 불가.
            for (int i = 0; i < count; i++)
            {
                bones[i].position = Vector3.Lerp(
                    _previousSnapshot.BonePositions[i],
                    _currentSnapshot.BonePositions[i],
                    t);
                bones[i].rotation = Quaternion.Slerp(
                    _previousSnapshot.BoneRotations[i],
                    _currentSnapshot.BoneRotations[i],
                    t);
            }

            // RootBody를 pelvis 위치로 이동.
            // AnimBody/VisualRoot가 RootBody의 자식이므로,
            // PoseTransfer에 매핑되지 않은 비주얼 본도 래그돌 근처에 위치.
            if (_rootBody != null && count > 0)
                _rootBody.MovePosition(bones[0].position);
        }
    }
}
