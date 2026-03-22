using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    // 단일 계층 래그돌: 비주얼 스켈레톤 본에 직접 부착.
    // 별도 래그돌 오브젝트 없이, 같은 본의 Rigidbody/Collider를 kinematic 토글로 제어.
    public class RagdollRig : MonoBehaviour, IRagdollRig
    {
        [SerializeField] private Transform _pelvis;

        private Rigidbody[] _ragdollRbs;
        private Collider[] _ragdollCols;
        private Transform[] _ragdollBoneTransforms;

        public IReadOnlyList<Rigidbody> Rigidbodies => _ragdollRbs;
        public IReadOnlyList<Transform> BoneTransforms => _ragdollBoneTransforms;
        public Transform PelvisTransform => _pelvis;

        private void Awake()
        {
            _ragdollRbs = GetComponentsInChildren<Rigidbody>(true);
            _ragdollCols = GetComponentsInChildren<Collider>(true);

            _ragdollBoneTransforms = new Transform[_ragdollRbs.Length];
            for (int i = 0; i < _ragdollRbs.Length; i++)
                _ragdollBoneTransforms[i] = _ragdollRbs[i].transform;

            // 시작 시 애니메이션 모드: kinematic + 콜라이더 비활성.
            SetKinematic(true);
            SetCollidersEnabled(false);
        }

        // 물리 래그돌 활성화 (Authority).
        public void Activate(Vector3 inheritedVelocity)
        {
            SetKinematic(false);
            SetCollidersEnabled(true);

            for (int i = 0; i < _ragdollRbs.Length; i++)
                _ragdollRbs[i].linearVelocity = inheritedVelocity;
        }

        // Kinematic 활성화 (Remote — BoneReceiver가 본 위치를 직접 설정).
        public void ActivateKinematic()
        {
            SetKinematic(true);
            SetCollidersEnabled(false);
        }

        // 애니메이션 모드 복귀.
        public void Deactivate()
        {
            SetKinematic(true);
            SetCollidersEnabled(false);
        }

        public bool IsSettled(float settleVelocity)
        {
            // 펠비스 수직 속도가 크면 아직 낙하 중.
            if (Mathf.Abs(_ragdollRbs[0].linearVelocity.y) > settleVelocity)
                return false;

            float totalSqrSpeed = 0f;
            for (int i = 0; i < _ragdollRbs.Length; i++)
                totalSqrSpeed += _ragdollRbs[i].linearVelocity.sqrMagnitude;

            return totalSqrSpeed / _ragdollRbs.Length < settleVelocity * settleVelocity;
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
