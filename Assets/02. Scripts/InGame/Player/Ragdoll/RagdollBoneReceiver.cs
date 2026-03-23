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
        private const float MinIntervalThreshold = 0.001f;
        private const float IntervalSmoothingFactor = 0.5f;

        public bool IsActive => _isActive;

        public void Activate()
        {
            _isActive = true;
            _hasSnapshot = false;
            _receiveInterval = DefaultReceiveInterval;

            // 단일 계층: 본을 kinematic으로 설정하여 BoneReceiver가 위치를 직접 제어.
            // 스켈레톤 분리는 RagdollStateMachine이 담당.
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
                if (interval > MinIntervalThreshold)
                    _receiveInterval = Mathf.Lerp(_receiveInterval, interval, IntervalSmoothingFactor);
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

            // 단일 계층: 본 = 비주얼. 네트워크 데이터를 직접 본에 적용하면 메시가 따라감.
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

            // RootBody를 pelvis 위치로 이동 (카메라 추적용).
            // 스켈레톤이 분리되어 있으므로 rootBody 이동이 본에 영향을 주지 않음.
            if (_rootBody != null && count > 0)
                _rootBody.MovePosition(_ragdollRig.PelvisTransform.position);
        }
    }
}
