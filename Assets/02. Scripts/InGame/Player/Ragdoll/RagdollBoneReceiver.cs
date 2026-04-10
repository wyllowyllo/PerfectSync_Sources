using System.Collections.Generic;
using Core;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    /// <summary>
    /// 래그돌 본 네트워크 수신기.
    /// 펠비스: 예측(velocity+gravity) → SmoothDamp (BodyMovementSynchronizer와 동일 패턴).
    /// 자식 본: 펠비스 로컬 공간에서 더블 버퍼 보간.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrderConstants.RagdollBoneReceiver)]
    public class RagdollBoneReceiver : MonoBehaviour
    {
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private Rigidbody _rootBody;

        private float _toArrivalTime;
        private float _receiveInterval;
        private bool _isReceiving;
        private int _snapshotCount;
        private int _activeBoneCount;

        // 더블 버퍼: 확정된 스냅샷 쌍. 배열 swap으로 GC 할당 없이 재사용.
        private Vector3[] _fromPositions;
        private Quaternion[] _fromRotations;
        private Vector3[] _toPositions;
        private Quaternion[] _toRotations;

        // 본별 운동 추정.
        private Vector3[] _estimatedVelocities;
        private Quaternion[] _estimatedAngularDeltas;
        private bool _hasMotionEstimate;

        // 펠비스 SmoothDamp (BodyMovementSynchronizer와 동일 패턴).
        private Vector3 _pelvisSmoothVelocity;

        private int _pelvisIndex;
        private int[] _parentBoneIndex;
        private float[] _maxBoneDistance;

        private const float DefaultReceiveInterval = 0.034f; // 30Hz SerializationRate 기준
        private const float MaxInterpolationInterval = 0.2f;
        private const float MaxExtrapolationTime = 0.15f;
        private const float MinIntervalThreshold = 0.001f;
        private const float IntervalSmoothingFactor = 0.5f;
        private const float BoneDistanceTolerance = 1.5f;

        // BodyMovementSynchronizer: SmoothTime=0.08, SnapThreshold=5.
        private const float PelvisSmoothTime = 0.06f;
        private const float SnapThreshold = 5f;

        public bool IsReceiving => _isReceiving;

        private void Start()
        {
            CachePelvisIndex();
            CacheBoneChain();
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

        // 본 체인 부모-자식 관계 및 허용 거리 캐싱.
        private void CacheBoneChain()
        {
            IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;
            int count = bones.Count;
            _parentBoneIndex = new int[count];
            _maxBoneDistance = new float[count];

            var boneIndexMap = new Dictionary<Transform, int>(count);
            for (int i = 0; i < count; i++)
                boneIndexMap[bones[i]] = i;

            for (int i = 0; i < count; i++)
            {
                _parentBoneIndex[i] = -1;

                Transform ancestor = bones[i].parent;
                while (ancestor != null)
                {
                    if (boneIndexMap.TryGetValue(ancestor, out int parentIdx))
                    {
                        _parentBoneIndex[i] = parentIdx;
                        _maxBoneDistance[i] = Vector3.Distance(
                            bones[i].position, bones[parentIdx].position) * BoneDistanceTolerance;
                        break;
                    }

                    ancestor = ancestor.parent;
                }
            }
        }

        public void StartReceiving()
        {
            _isReceiving = true;
            _snapshotCount = 0;
            _hasMotionEstimate = false;
            _receiveInterval = DefaultReceiveInterval;
            _pelvisSmoothVelocity = Vector3.zero;

            _ragdollRig.ActivateKinematic();
        }

        public void StopReceiving()
        {
            // 마지막 확정 스냅샷으로 복원 (SmoothDamp 지연 없이 즉시).
            // BlendToAnim 전환 시 SnapshotRagdollPoses()가 올바른 포즈를 캡처하도록 보장.
            if (_snapshotCount >= 1 && _toPositions != null)
            {
                IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;
                int count = Mathf.Min(bones.Count, _activeBoneCount);
                for (int i = 0; i < count; i++)
                {
                    bones[i].position = _toPositions[i];
                    bones[i].rotation = _toRotations[i];
                }
            }

            _isReceiving = false;
            _snapshotCount = 0;
            _hasMotionEstimate = false;
        }

        public void ApplySnapshot(RagdollBoneSnapshot snapshot)
        {
            if (!_isReceiving) return;

            int snapshotBoneCount = snapshot.BonePositions.Length;
            IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;
            int boneCount = Mathf.Min(bones.Count, snapshotBoneCount);
            EnsureBuffers(boneCount);

            float now = Time.time;

            if (_snapshotCount == 0)
            {
                // 첫 스냅샷: 즉시 적용 + SmoothDamp 시드.
                System.Array.Copy(snapshot.BonePositions, _fromPositions, boneCount);
                System.Array.Copy(snapshot.BoneRotations, _fromRotations, boneCount);
                System.Array.Copy(snapshot.BonePositions, _toPositions, boneCount);
                System.Array.Copy(snapshot.BoneRotations, _toRotations, boneCount);

                for (int i = 0; i < boneCount; i++)
                {
                    bones[i].position = snapshot.BonePositions[i];
                    bones[i].rotation = snapshot.BoneRotations[i];
                }

                _pelvisSmoothVelocity = Vector3.zero;
                _activeBoneCount = boneCount;
                _toArrivalTime = now;
                _snapshotCount = 1;
                return;
            }

            // 연속 스냅샷 간 속도·각속도 추정.
            float interval = now - _toArrivalTime;
            if (interval > MinIntervalThreshold)
            {
                _receiveInterval = Mathf.Lerp(_receiveInterval, interval, IntervalSmoothingFactor);

                int velCount = Mathf.Min(boneCount, _activeBoneCount);
                float invDt = 1f / interval;
                for (int i = 0; i < velCount; i++)
                {
                    _estimatedVelocities[i] =
                        (snapshot.BonePositions[i] - _toPositions[i]) * invDt;
                    _estimatedAngularDeltas[i] =
                        snapshot.BoneRotations[i] * Quaternion.Inverse(_toRotations[i]);
                }
                _hasMotionEstimate = true;
            }

            // 더블 버퍼 교대 (배열 swap).
            var tmpP = _fromPositions;
            _fromPositions = _toPositions;
            _toPositions = tmpP;

            var tmpR = _fromRotations;
            _fromRotations = _toRotations;
            _toRotations = tmpR;

            System.Array.Copy(snapshot.BonePositions, _toPositions, boneCount);
            System.Array.Copy(snapshot.BoneRotations, _toRotations, boneCount);
            _activeBoneCount = boneCount;
            _toArrivalTime = now;
            _snapshotCount++;
        }

        private void LateUpdate()
        {
            if (!_isReceiving || _snapshotCount < 1) return;

            IReadOnlyList<Transform> bones = _ragdollRig.BoneTransforms;
            int count = Mathf.Min(bones.Count, _activeBoneCount);
            if (_fromPositions == null || count > _fromPositions.Length) return;

            float elapsed = Time.time - _toArrivalTime;
            float interval = Mathf.Min(_receiveInterval, MaxInterpolationInterval);

            // ── 1. 펠비스: 예측 타깃 → SmoothDamp ──
            // BodyMovementSynchronizer와 동일 패턴: velocity 외삽 + 중력 → SmoothDamp.
            // 새 스냅샷 도착 시 타깃만 갱신, SmoothDamp이 보정을 흡수 → 스냅 없음.
            Vector3 pelvisTarget;
            if (_hasMotionEstimate)
            {
                float predTime = Mathf.Min(elapsed, MaxExtrapolationTime);
                pelvisTarget = _toPositions[_pelvisIndex]
                    + _estimatedVelocities[_pelvisIndex] * predTime
                    + 0.5f * Physics.gravity * (predTime * predTime);
            }
            else
            {
                pelvisTarget = _toPositions[_pelvisIndex];
            }

            Vector3 currentPelvis = bones[_pelvisIndex].position;
            float dist = Vector3.Distance(currentPelvis, pelvisTarget);

            Vector3 pelvisPos;
            if (dist > SnapThreshold)
            {
                pelvisPos = pelvisTarget;
                _pelvisSmoothVelocity = _hasMotionEstimate
                    ? _estimatedVelocities[_pelvisIndex]
                    : Vector3.zero;
            }
            else
            {
                pelvisPos = Vector3.SmoothDamp(
                    currentPelvis, pelvisTarget,
                    ref _pelvisSmoothVelocity, PelvisSmoothTime,
                    Mathf.Infinity, Time.deltaTime);
            }

            bones[_pelvisIndex].position = pelvisPos;

            // ── 2. 본 회전 + 위치 (펠비스 로컬 공간) ──
            float t = elapsed / interval;

            Quaternion fromPelvisRot = _fromRotations[_pelvisIndex];
            Quaternion toPelvisRot = _toRotations[_pelvisIndex];
            Quaternion fromPelvisInv = Quaternion.Inverse(fromPelvisRot);
            Quaternion toPelvisInv = Quaternion.Inverse(toPelvisRot);

            if (t <= 1f)
            {
                // 보간 구간: Slerp 회전 + 펠비스 로컬 Lerp 위치.
                Quaternion pelvisRot = Quaternion.Slerp(fromPelvisRot, toPelvisRot, t);
                bones[_pelvisIndex].rotation = pelvisRot;

                for (int i = 0; i < count; i++)
                {
                    if (i == _pelvisIndex) continue;

                    bones[i].rotation = Quaternion.Slerp(
                        _fromRotations[i], _toRotations[i], t);

                    Vector3 fromLocal = fromPelvisInv * (_fromPositions[i] - _fromPositions[_pelvisIndex]);
                    Vector3 toLocal = toPelvisInv * (_toPositions[i] - _toPositions[_pelvisIndex]);
                    bones[i].position = pelvisPos + pelvisRot * Vector3.Lerp(fromLocal, toLocal, t);
                }
            }
            else if (_hasMotionEstimate)
            {
                // 외삽 구간: 각속도 연속 + FK 위치 재구성.
                float extraTime = Mathf.Min(elapsed - interval, MaxExtrapolationTime);
                float extraFraction = extraTime / Mathf.Max(interval, MinIntervalThreshold);

                Quaternion pelvisAngStep = Quaternion.SlerpUnclamped(
                    Quaternion.identity, _estimatedAngularDeltas[_pelvisIndex], extraFraction);
                Quaternion pelvisRot = pelvisAngStep * _toRotations[_pelvisIndex];
                bones[_pelvisIndex].rotation = pelvisRot;

                for (int i = 0; i < count; i++)
                {
                    if (i == _pelvisIndex) continue;

                    Quaternion angStep = Quaternion.SlerpUnclamped(
                        Quaternion.identity, _estimatedAngularDeltas[i], extraFraction);
                    bones[i].rotation = angStep * _toRotations[i];

                    int parentIdx = _parentBoneIndex[i];
                    if (parentIdx >= 0 && parentIdx < count)
                    {
                        Quaternion parentToInv = Quaternion.Inverse(_toRotations[parentIdx]);
                        Vector3 localOffset = parentToInv *
                            (_toPositions[i] - _toPositions[parentIdx]);
                        bones[i].position = bones[parentIdx].position +
                            bones[parentIdx].rotation * localOffset;
                    }
                    else
                    {
                        Vector3 localOffset = toPelvisInv *
                            (_toPositions[i] - _toPositions[_pelvisIndex]);
                        bones[i].position = pelvisPos + pelvisRot * localOffset;
                    }
                }
            }

            EnforceBoneDistances(bones, count);

            if (_rootBody != null && count > 0)
                _rootBody.MovePosition(pelvisPos);
        }

        private void EnforceBoneDistances(IReadOnlyList<Transform> bones, int count)
        {
            if (_parentBoneIndex == null) return;

            for (int i = 0; i < count; i++)
            {
                int parentIdx = _parentBoneIndex[i];
                if (parentIdx < 0) continue;

                Vector3 offset = bones[i].position - bones[parentIdx].position;
                float sqrDist = offset.sqrMagnitude;
                float maxDist = _maxBoneDistance[i];

                if (sqrDist > maxDist * maxDist)
                    bones[i].position = bones[parentIdx].position + offset * (maxDist / Mathf.Sqrt(sqrDist));
            }
        }

        private void EnsureBuffers(int count)
        {
            if (_fromPositions == null || _fromPositions.Length < count)
            {
                _fromPositions = new Vector3[count];
                _fromRotations = new Quaternion[count];
                _toPositions = new Vector3[count];
                _toRotations = new Quaternion[count];
                _estimatedVelocities = new Vector3[count];
                _estimatedAngularDeltas = new Quaternion[count];
            }
        }
    }
}
