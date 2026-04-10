using System.Collections.Generic;
using Core;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    /// <summary>
    /// 더블 버퍼 보간: 확정된 네트워크 스냅샷 간 보간으로 drift/snap 제거.
    /// 첫 스냅샷만 시각 위치→스냅샷 보간 (애니메이션→래그돌 전환용).
    /// 이후: from(이전 확정 스냅샷) → to(현재 확정 스냅샷).
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

        // 본별 운동 추정 (외삽 + Hermite 보간용).
        private Vector3[] _estimatedVelocities;  // to 시점의 펠비스/본 속도
        private Quaternion[] _estimatedAngularDeltas;
        private Vector3 _fromPelvisVelocity;     // from 시점의 펠비스 속도 (Hermite 탄젠트)
        private bool _hasMotionEstimate;

        private int _pelvisIndex;
        private int[] _parentBoneIndex;
        private float[] _maxBoneDistance;

        private const float DefaultReceiveInterval = 0.034f; // 30Hz SerializationRate 기준
        private const float MaxInterpolationInterval = 0.2f;
        private const float MaxExtrapolationTime = 0.15f;
        private const float MinIntervalThreshold = 0.001f;
        private const float IntervalSmoothingFactor = 0.5f;
        private const float BoneDistanceTolerance = 1.5f;

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
        // GetComponentsInChildren 순서 = 계층 순회 순서이므로 부모가 자식보다 앞.
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

            // 단일 계층: 본을 kinematic으로 설정하여 BoneReceiver가 위치를 직접 제어.
            // 스켈레톤 분리는 RagdollStateMachine이 담당.
            _ragdollRig.ActivateKinematic();
        }

        public void StopReceiving()
        {
            // 외삽 위치가 아닌 마지막 확정 스냅샷으로 본을 복원.
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
                // 첫 스냅샷: 현재 시각 위치를 from, 스냅샷을 to로 설정.
                // 애니메이션 포즈 → 래그돌 포즈 전환을 보간으로 부드럽게 처리.
                for (int i = 0; i < boneCount; i++)
                {
                    _fromPositions[i] = bones[i].position;
                    _fromRotations[i] = bones[i].rotation;
                }
                System.Array.Copy(snapshot.BonePositions, _toPositions, boneCount);
                System.Array.Copy(snapshot.BoneRotations, _toRotations, boneCount);
                _activeBoneCount = boneCount;
                _toArrivalTime = now;
                _snapshotCount = 1;
                return;
            }

            // 연속 스냅샷 간 속도·각속도 추정 (확정 스냅샷 기반, drift 없음).
            float interval = now - _toArrivalTime;
            if (interval > MinIntervalThreshold)
            {
                _receiveInterval = Mathf.Lerp(_receiveInterval, interval, IntervalSmoothingFactor);

                // Hermite: 이전 to 속도를 새 from 속도로 보존.
                _fromPelvisVelocity = _hasMotionEstimate
                    ? _estimatedVelocities[_pelvisIndex]
                    : Vector3.zero;

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

            // 더블 버퍼 교대: 이전 to → 새 from (배열 swap, 할당 없음).
            var tmpP = _fromPositions;
            _fromPositions = _toPositions;
            _toPositions = tmpP;

            var tmpR = _fromRotations;
            _fromRotations = _toRotations;
            _toRotations = tmpR;

            // 새 스냅샷을 to에 복사.
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

            float elapsed = Time.time - _toArrivalTime;
            float interval = Mathf.Min(_receiveInterval, MaxInterpolationInterval);

            int count = Mathf.Min(bones.Count, _activeBoneCount);
            if (_fromPositions == null || count > _fromPositions.Length)
                return;

            float t = elapsed / interval;

            if (t <= 1f)
            {
                // 보간 구간: 펠비스 로컬 공간에서 from→to 보간.
                // from/to 모두 확정된 스냅샷이므로 drift에 의한 보정 snap 없음.
                InterpolateInPelvisSpace(bones, count, t);
            }
            else if (_hasMotionEstimate)
            {
                // 외삽 구간 (패킷 유실 시에만 진입 — 정상 수신 시 t≤1.0).
                // 펠비스 속도로 스켈레톤 일체 이동 + 본별 각속도 연속.
                float extraTime = Mathf.Min(elapsed - interval, MaxExtrapolationTime);
                float extraFraction = extraTime / Mathf.Max(interval, MinIntervalThreshold);

                Vector3 pelvisDelta = _estimatedVelocities[_pelvisIndex] * extraTime
                    + 0.5f * Physics.gravity * (extraTime * extraTime);

                Quaternion toPelvisInv = Quaternion.Inverse(_toRotations[_pelvisIndex]);

                // 펠비스 외삽.
                Quaternion pelvisAngStep = Quaternion.SlerpUnclamped(
                    Quaternion.identity, _estimatedAngularDeltas[_pelvisIndex], extraFraction);
                Quaternion pelvisRot = pelvisAngStep * _toRotations[_pelvisIndex];
                Vector3 pelvisPos = _toPositions[_pelvisIndex] + pelvisDelta;

                bones[_pelvisIndex].position = pelvisPos;
                bones[_pelvisIndex].rotation = pelvisRot;

                // 자식 본: 부모 회전 기반 FK 외삽 (골격 구조 유지).
                for (int i = 0; i < count; i++)
                {
                    if (i == _pelvisIndex) continue;

                    // 각속도 외삽.
                    Quaternion angStep = Quaternion.SlerpUnclamped(
                        Quaternion.identity, _estimatedAngularDeltas[i], extraFraction);
                    bones[i].rotation = angStep * _toRotations[i];

                    int parentIdx = _parentBoneIndex[i];
                    if (parentIdx >= 0 && parentIdx < count)
                    {
                        // FK: 부모 기준 로컬 오프셋을 부모의 외삽 회전으로 재계산.
                        Quaternion parentToInv = Quaternion.Inverse(_toRotations[parentIdx]);
                        Vector3 localOffset = parentToInv *
                            (_toPositions[i] - _toPositions[parentIdx]);
                        bones[i].position = bones[parentIdx].position +
                            bones[parentIdx].rotation * localOffset;
                    }
                    else
                    {
                        // 부모 없는 루트 본: 펠비스 기준 이동.
                        Vector3 localOffset = toPelvisInv *
                            (_toPositions[i] - _toPositions[_pelvisIndex]);
                        bones[i].position = pelvisPos + pelvisRot * localOffset;
                    }
                }
            }

            // 보간/외삽 후 부모-자식 본 거리가 허용치를 초과하면 클램핑.
            // kinematic 본은 관절 구속이 없으므로 골격 늘어남 방지용 안전망.
            EnforceBoneDistances(bones, count);

            // RootBody를 pelvis 위치로 이동 (카메라 추적용).
            // 스켈레톤이 분리되어 있으므로 rootBody 이동이 본에 영향을 주지 않음.
            if (_rootBody != null && count > 0)
                _rootBody.MovePosition(_ragdollRig.PelvisTransform.position);
        }

        private void InterpolateInPelvisSpace(IReadOnlyList<Transform> bones, int count, float t)
        {
            Vector3 fromPelvisPos = _fromPositions[_pelvisIndex];
            Vector3 toPelvisPos = _toPositions[_pelvisIndex];
            Quaternion fromPelvisRot = _fromRotations[_pelvisIndex];
            Quaternion toPelvisRot = _toRotations[_pelvisIndex];

            // 펠비스: Hermite 스플라인 보간 (속도 정보 활용 시).
            // 선형 보간은 직선만 가능하지만, Hermite는 낙하 포물선·방향 전환을 자연스럽게 재현.
            Vector3 pelvisPos;
            if (_hasMotionEstimate)
            {
                float dt = Mathf.Min(_receiveInterval, MaxInterpolationInterval);
                pelvisPos = CubicHermite(
                    fromPelvisPos, _fromPelvisVelocity * dt,
                    toPelvisPos, _estimatedVelocities[_pelvisIndex] * dt, t);
            }
            else
            {
                pelvisPos = Vector3.Lerp(fromPelvisPos, toPelvisPos, t);
            }

            Quaternion pelvisRot = Quaternion.Slerp(fromPelvisRot, toPelvisRot, t);
            Quaternion fromPelvisInv = Quaternion.Inverse(fromPelvisRot);
            Quaternion toPelvisInv = Quaternion.Inverse(toPelvisRot);

            for (int i = 0; i < count; i++)
            {
                bones[i].rotation = Quaternion.Slerp(
                    _fromRotations[i], _toRotations[i], t);

                if (i == _pelvisIndex)
                {
                    bones[i].position = pelvisPos;
                    continue;
                }

                Vector3 fromLocal = fromPelvisInv * (_fromPositions[i] - fromPelvisPos);
                Vector3 toLocal = toPelvisInv * (_toPositions[i] - toPelvisPos);
                bones[i].position = pelvisPos + pelvisRot * Vector3.Lerp(fromLocal, toLocal, t);
            }
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

        /// <summary>
        /// 3차 Hermite 스플라인: 양 끝점의 위치·속도(탄젠트)를 만족하는 매끄러운 곡선.
        /// 중력 낙하, 방향 전환 등 비선형 궤적을 선형 보간보다 정확히 재현.
        /// </summary>
        private static Vector3 CubicHermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0
                 + (t3 - 2f * t2 + t) * m0
                 + (-2f * t3 + 3f * t2) * p1
                 + (t3 - t2) * m1;
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
