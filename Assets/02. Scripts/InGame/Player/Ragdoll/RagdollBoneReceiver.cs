using System.Collections.Generic;
using Core;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    [DefaultExecutionOrder(ExecutionOrderConstants.RagdollBoneReceiver)]
    public class RagdollBoneReceiver : MonoBehaviour
    {
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private Rigidbody _rootBody;

        private RagdollBoneSnapshot _currentSnapshot;
        private float _lastReceiveTime;
        private float _receiveInterval;
        private bool _isReceiving;
        private bool _hasSnapshot;

        // 보간 시작점 (현재 시각적 위치) 및 추정 속도 — 사전 할당 버퍼.
        private Vector3[] _interpFromPositions;
        private Quaternion[] _interpFromRotations;
        private Vector3[] _estimatedVelocities;
        private bool _hasVelocity;

        private const float DefaultReceiveInterval = 0.1f;
        private const float MaxInterpolationInterval = 0.2f;
        private const float MaxExtrapolationTime = 0.15f;
        private const float MinIntervalThreshold = 0.001f;
        private const float IntervalSmoothingFactor = 0.5f;

        public bool IsReceiving => _isReceiving;

        public void StartReceiving()
        {
            _isReceiving = true;
            _hasSnapshot = false;
            _hasVelocity = false;
            _receiveInterval = DefaultReceiveInterval;

            // 단일 계층: 본을 kinematic으로 설정하여 BoneReceiver가 위치를 직접 제어.
            // 스켈레톤 분리는 RagdollStateMachine이 담당.
            _ragdollRig.ActivateKinematic();
        }

        public void StopReceiving()
        {
            _isReceiving = false;
            _hasSnapshot = false;
            _hasVelocity = false;
        }

        public void ApplySnapshot(RagdollBoneSnapshot snapshot)
        {
            if (!_isReceiving) return;

            int snapshotCount = snapshot.BonePositions.Length;
            IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;
            int boneCount = Mathf.Min(bones.Count, snapshotCount);
            EnsureBuffers(boneCount);

            float now = Time.time;

            if (_hasSnapshot)
            {
                float interval = now - _lastReceiveTime;
                if (interval > MinIntervalThreshold)
                {
                    _receiveInterval = Mathf.Lerp(_receiveInterval, interval, IntervalSmoothingFactor);

                    // 연속 네트워크 스냅샷 간 위치 변화량으로 본별 속도 추정.
                    int velCount = Mathf.Min(snapshotCount, _currentSnapshot.BonePositions.Length);
                    float invDt = 1f / interval;
                    for (int i = 0; i < velCount; i++)
                    {
                        _estimatedVelocities[i] =
                            (snapshot.BonePositions[i] - _currentSnapshot.BonePositions[i]) * invDt;
                    }
                    _hasVelocity = true;
                }
            }

            // 현재 시각적 본 위치를 보간 시작점으로 캡처.
            // 외삽 중 새 스냅샷 도착 시 스냅백(역방향 점프) 방지.
            for (int i = 0; i < boneCount; i++)
            {
                _interpFromPositions[i] = bones[i].position;
                _interpFromRotations[i] = bones[i].rotation;
            }

            _currentSnapshot = snapshot;
            _lastReceiveTime = now;
            _hasSnapshot = true;
        }

        private void LateUpdate()
        {
            if (!_isReceiving || !_hasSnapshot) return;

            IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;

            float elapsed = Time.time - _lastReceiveTime;
            float interval = Mathf.Min(_receiveInterval, MaxInterpolationInterval);

            int count = Mathf.Min(bones.Count, _currentSnapshot.BonePositions.Length);
            if (_interpFromPositions == null || count > _interpFromPositions.Length)
                return;

            float t = elapsed / interval;

            if (t <= 1f)
            {
                // 보간 구간: 시각적 시작점 → 현재 스냅샷.
                for (int i = 0; i < count; i++)
                {
                    bones[i].position = Vector3.Lerp(
                        _interpFromPositions[i],
                        _currentSnapshot.BonePositions[i],
                        t);
                    bones[i].rotation = Quaternion.Slerp(
                        _interpFromRotations[i],
                        _currentSnapshot.BoneRotations[i],
                        t);
                }
            }
            else if (_hasVelocity)
            {
                // 외삽 구간: 추정 속도 + 중력으로 다음 스냅샷 도착까지 예측.
                float extraTime = Mathf.Min(elapsed - interval, MaxExtrapolationTime);
                Vector3 gravityDelta = 0.5f * Physics.gravity * (extraTime * extraTime);

                for (int i = 0; i < count; i++)
                {
                    bones[i].position = _currentSnapshot.BonePositions[i]
                        + _estimatedVelocities[i] * extraTime
                        + gravityDelta;
                    bones[i].rotation = _currentSnapshot.BoneRotations[i];
                }
            }

            // RootBody를 pelvis 위치로 이동 (카메라 추적용).
            // 스켈레톤이 분리되어 있으므로 rootBody 이동이 본에 영향을 주지 않음.
            if (_rootBody != null && count > 0)
                _rootBody.MovePosition(_ragdollRig.PelvisTransform.position);
        }

        private void EnsureBuffers(int count)
        {
            if (_interpFromPositions == null || _interpFromPositions.Length < count)
            {
                _interpFromPositions = new Vector3[count];
                _interpFromRotations = new Quaternion[count];
                _estimatedVelocities = new Vector3[count];
            }
        }
    }
}
