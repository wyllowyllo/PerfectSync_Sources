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

        private int _pelvisIndex;

        private const float DefaultReceiveInterval = 0.1f;
        private const float MaxInterpolationInterval = 0.2f;
        private const float MaxExtrapolationTime = 0.15f;
        private const float MinIntervalThreshold = 0.001f;
        private const float IntervalSmoothingFactor = 0.5f;

        public bool IsReceiving => _isReceiving;

        private void Start()
        {
            CachePelvisIndex();
        }

        private void CachePelvisIndex()
        {
            IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;
            Transform pelvis = _ragdollRig.PelvisTransform;
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] == pelvis)
                {
                    _pelvisIndex = i;
                    return;
                }
            }
        }

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
            // 외삽 위치가 아닌 마지막 확정 스냅샷으로 본을 복원.
            // BlendToAnim 전환 시 SnapshotRagdollPoses()가 올바른 포즈를 캡처하도록 보장.
            if (_hasSnapshot)
            {
                IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;
                int count = Mathf.Min(bones.Count, _currentSnapshot.BonePositions.Length);
                for (int i = 0; i < count; i++)
                {
                    bones[i].position = _currentSnapshot.BonePositions[i];
                    bones[i].rotation = _currentSnapshot.BoneRotations[i];
                }
            }

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
                // 보간 구간: 펠비스 로컬 공간에서 보간.
                // 월드 공간 독립 보간 시 빠른 회전에서 본 간 직선 경로가 달라
                // 골격 거리가 깨지는 문제를 방지.
                Vector3 fromPelvisPos = _interpFromPositions[_pelvisIndex];
                Vector3 toPelvisPos = _currentSnapshot.BonePositions[_pelvisIndex];
                Quaternion fromPelvisRot = _interpFromRotations[_pelvisIndex];
                Quaternion toPelvisRot = _currentSnapshot.BoneRotations[_pelvisIndex];

                Vector3 pelvisPos = Vector3.Lerp(fromPelvisPos, toPelvisPos, t);
                Quaternion pelvisRot = Quaternion.Slerp(fromPelvisRot, toPelvisRot, t);
                Quaternion fromPelvisInv = Quaternion.Inverse(fromPelvisRot);
                Quaternion toPelvisInv = Quaternion.Inverse(toPelvisRot);

                for (int i = 0; i < count; i++)
                {
                    bones[i].rotation = Quaternion.Slerp(
                        _interpFromRotations[i],
                        _currentSnapshot.BoneRotations[i],
                        t);

                    if (i == _pelvisIndex)
                    {
                        bones[i].position = pelvisPos;
                        continue;
                    }

                    Vector3 fromLocal = fromPelvisInv * (_interpFromPositions[i] - fromPelvisPos);
                    Vector3 toLocal = toPelvisInv * (_currentSnapshot.BonePositions[i] - toPelvisPos);
                    bones[i].position = pelvisPos + pelvisRot * Vector3.Lerp(fromLocal, toLocal, t);
                }
            }
            else if (_hasVelocity)
            {
                // 외삽 구간: 펠비스 속도로 전체 스켈레톤을 일체 이동.
                // 수신측은 kinematic이라 관절 구속이 없으므로, 본별 독립 외삽 시
                // 빠른 회전/비행에서 골격이 발산(치즈 현상). 펠비스 기준 통일 이동으로 방지.
                float extraTime = Mathf.Min(elapsed - interval, MaxExtrapolationTime);
                Vector3 pelvisDelta = _estimatedVelocities[_pelvisIndex] * extraTime
                    + 0.5f * Physics.gravity * (extraTime * extraTime);

                for (int i = 0; i < count; i++)
                {
                    bones[i].position = _currentSnapshot.BonePositions[i] + pelvisDelta;
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
