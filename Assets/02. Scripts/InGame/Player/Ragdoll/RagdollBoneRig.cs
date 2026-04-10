using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Ragdoll
{
    // 단일 계층 래그돌: 비주얼 스켈레톤 본에 직접 부착.
    // 별도 래그돌 오브젝트 없이, 같은 본의 Rigidbody/Collider를 kinematic 토글로 제어.
    public class RagdollRig : MonoBehaviour, IRagdollRig
    {
        [SerializeField] private Transform _pelvis;
        [FormerlySerializedAs("_physicsProfile")] [SerializeField] private CharacterSpeedLimits speedLimits;

        [Header("Extra Gravity")]
        [Tooltip("래그돌 상태에서 추가 하향 가속도 (m/s²). Unity 기본 중력에 더해짐.")]
        [SerializeField] private float _extraGravity = 15f;

        private Rigidbody _pelvisRb;
        private Rigidbody[] _ragdollRbs;
        private Collider[] _ragdollCols;
        private Transform[] _ragdollBoneTransforms;

        private bool _isPhysicsActive;

        private const int RagdollSolverIterations = 8;
        private const int RagdollSolverVelocityIterations = 2;

        // SO 미할당 시 폴백.
        private const float FallbackMaxBoneSpeed = 30f;
        private const float FallbackMaxInheritedSpeed = 20f;

        private float _maxBoneSpeed;
        private float _maxBoneSpeedSqr;
        private float _maxInheritedSpeed;

        public IReadOnlyList<Rigidbody> Rigidbodies => _ragdollRbs;
        public IReadOnlyList<Transform> BoneTransforms => _ragdollBoneTransforms;
        public Transform PelvisTransform => _pelvis;
        public CharacterSpeedLimits SpeedLimits => speedLimits;

        private void Awake()
        {
            _ragdollRbs = GetComponentsInChildren<Rigidbody>(true);
            _ragdollCols = GetComponentsInChildren<Collider>(true);
            _pelvisRb = _pelvis.GetComponent<Rigidbody>();

            _ragdollBoneTransforms = new Transform[_ragdollRbs.Length];
            for (int i = 0; i < _ragdollRbs.Length; i++)
                _ragdollBoneTransforms[i] = _ragdollRbs[i].transform;

            // SO에서 속도 제한 캐싱.
            if (speedLimits != null)
            {
                _maxBoneSpeed = speedLimits.MaxBoneSpeed;
                _maxBoneSpeedSqr = speedLimits.MaxBoneSpeedSqr;
                _maxInheritedSpeed = speedLimits.MaxInheritedSpeed;
            }
            else
            {
                _maxBoneSpeed = FallbackMaxBoneSpeed;
                _maxBoneSpeedSqr = FallbackMaxBoneSpeed * FallbackMaxBoneSpeed;
                _maxInheritedSpeed = FallbackMaxInheritedSpeed;
            }

            // 시작 시 애니메이션 모드: kinematic + 콜라이더 비활성.
            SetKinematic(true);
            SetCollidersEnabled(false);
        }

        // 물리 래그돌 활성화 (Authority).
        public void ActivatePhysics(Vector3 inheritedVelocity)
        {
            SetKinematic(false);
            SetCollidersEnabled(true);
            _isPhysicsActive = true;

            if (inheritedVelocity.sqrMagnitude > _maxInheritedSpeed * _maxInheritedSpeed)
                inheritedVelocity = inheritedVelocity.normalized * _maxInheritedSpeed;

            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                _ragdollRbs[i].solverIterations = RagdollSolverIterations;
                _ragdollRbs[i].solverVelocityIterations = RagdollSolverVelocityIterations;
                _ragdollRbs[i].linearVelocity = inheritedVelocity;
            }
        }

        // Kinematic 활성화 (Remote — BoneReceiver가 본 위치를 직접 설정).
        public void ActivateKinematic()
        {
            SetKinematic(true);
            SetCollidersEnabled(false);
            _isPhysicsActive = false;
        }

        // 애니메이션 모드 복귀.
        public void DeactivateRagdoll()
        {
            SetKinematic(true);
            SetCollidersEnabled(false);
            _isPhysicsActive = false;
        }

        private const float AngularVelocityWeight = 0.3f;

        public bool IsSettled(float settleVelocity)
        {
            // 펠비스 수직 속도가 크면 아직 낙하 중.
            if (Mathf.Abs(_pelvisRb.linearVelocity.y) > settleVelocity)
                return false;

            float totalSqrSpeed = 0f;
            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                totalSqrSpeed += _ragdollRbs[i].linearVelocity.sqrMagnitude;
                totalSqrSpeed += _ragdollRbs[i].angularVelocity.sqrMagnitude * AngularVelocityWeight;
            }

            return totalSqrSpeed / _ragdollRbs.Length < settleVelocity * settleVelocity;
        }

        private void FixedUpdate()
        {
            if (!_isPhysicsActive) return;

            Vector3 extraForce = Vector3.down * _extraGravity;
            for (int i = 0; i < _ragdollRbs.Length; i++)
            {
                Rigidbody rb = _ragdollRbs[i];
                rb.AddForce(extraForce, ForceMode.Acceleration);

                // 솔버 디페네트레이션 방어: 프레임 드랍 시 관통 → 폭발적 속도 방지.
                Vector3 v = rb.linearVelocity;
                if (v.sqrMagnitude > _maxBoneSpeedSqr)
                    rb.linearVelocity = v.normalized * _maxBoneSpeed;
            }
        }

        private void SetKinematic(bool value)
        {
            for (int i = 0; i < _ragdollRbs.Length; i++)
                _ragdollRbs[i].isKinematic = value;
        }

        private void SetCollidersEnabled(bool value)
        {
            for (int i = 0; i < _ragdollCols.Length; i++)
                _ragdollCols[i].enabled = value;
        }
    }
}
