using UnityEngine;

namespace InGame.Player.Ragdoll
{
    // 래그돌→애니메이션 본 블렌드 시스템 (RagdollHelper 방식).
    // RagdollStateMachine이 생성/소유하며, MonoBehaviour가 아닌 plain class.
    class RagdollBlender
    {
        private struct BonePose
        {
            public Transform Transform;
            public Vector3 StoredPosition;
            public Quaternion StoredRotation;
        }

        private readonly Animator _animator;
        private readonly float _blendTime;
        private readonly float _groundCheckDistance;
        private readonly LayerMask _groundLayer;

        private BonePose[] _blendBones;
        private int _hipBoneIndex = -1;
        private Vector3 _ragdolledHipPosition;
        private Vector3 _ragdolledHeadPosition;
        private Vector3 _ragdolledFeetPosition;
        private float _blendStartTime;

        private const float MecanimTransitionTime = 0.05f;
        private const float RayOriginUpOffset = 0.5f;
        private const float MinDirectionSqrMagnitude = 0.001f;

        public RagdollBlender(IRagdollRig rig, Animator animator, float blendTime,
                              float groundCheckDistance, LayerMask groundLayer)
        {
            _animator = animator;
            _blendTime = blendTime;
            _groundCheckDistance = groundCheckDistance;
            _groundLayer = groundLayer;

            InitializeBones(rig);
        }

        private void InitializeBones(IRagdollRig rig)
        {
            // 래그돌 본(Rigidbody가 있는 본)만 블렌드 대상으로 수집.
            // 장식 오브젝트(눈, 모자 등)는 부모 본을 따라 자연스럽게 이동하므로 제외.
            var boneTransforms = rig.BoneTransforms;
            _blendBones = new BonePose[boneTransforms.Count];

            Transform hipTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);

            for (int i = 0; i < boneTransforms.Count; i++)
            {
                _blendBones[i].Transform = boneTransforms[i];
                if (boneTransforms[i] == hipTransform)
                    _hipBoneIndex = i;
            }
        }

        public void SnapshotRagdollPoses()
        {
            for (int i = 0; i < _blendBones.Length; i++)
            {
                _blendBones[i].StoredPosition = _blendBones[i].Transform.position;
                _blendBones[i].StoredRotation = _blendBones[i].Transform.rotation;
            }
        }

        // BeginBlendToAnim 시 루트 매칭용 위치 저장.
        public void SnapshotRootAlignment()
        {
            Transform hipBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);

            _ragdolledHipPosition = hipBone.position;
            _ragdolledHeadPosition = headBone.position;
            _ragdolledFeetPosition = 0.5f * (leftFoot.position + rightFoot.position);
        }

        public void StartBlend()
        {
            _blendStartTime = Time.time;
        }

        public bool IsBlendComplete()
        {
            float elapsed = Time.time - _blendStartTime - MecanimTransitionTime;
            float ragdollBlend = 1.0f - elapsed / _blendTime;
            return ragdollBlend <= 0f;
        }

        // LateUpdate에서 호출. Animator가 포즈를 적용한 후 래그돌 포즈와 보간.
        public void BlendToAnimation(bool isAuthority, Rigidbody rootBody)
        {
            float elapsed = Time.time - _blendStartTime;

            // 메카님 전환 대기: 루트를 래그돌 위치에 맞추고 본을 래그돌 포즈로 유지.
            if (elapsed <= MecanimTransitionTime)
            {
                // 루트 매칭은 Authority만 수행. Remote는 root lerp로 별도 처리.
                if (isAuthority)
                    MatchRootToRagdolledPose(rootBody);

                for (int i = 0; i < _blendBones.Length; i++)
                {
                    if (_blendBones[i].Transform == null) continue;

                    _blendBones[i].Transform.rotation = _blendBones[i].StoredRotation;

                    if (i == _hipBoneIndex)
                        _blendBones[i].Transform.position = _blendBones[i].StoredPosition;
                }

                return;
            }

            // 블렌드 계수: 1.0(래그돌) → 0.0(애니메이션).
            float ragdollBlend = 1.0f
                - (elapsed - MecanimTransitionTime)
                / _blendTime;
            ragdollBlend = Mathf.Clamp01(ragdollBlend);

            // Animator가 이미 이번 프레임 애니메이션 포즈를 적용한 상태.
            // 저장된 래그돌 포즈와 현재 애니메이션 포즈를 보간.
            for (int i = 0; i < _blendBones.Length; i++)
            {
                if (_blendBones[i].Transform == null) continue;

                if (i == _hipBoneIndex)
                {
                    _blendBones[i].Transform.position = Vector3.Lerp(
                        _blendBones[i].Transform.position,
                        _blendBones[i].StoredPosition,
                        ragdollBlend);
                }

                _blendBones[i].Transform.rotation = Quaternion.Slerp(
                    _blendBones[i].Transform.rotation,
                    _blendBones[i].StoredRotation,
                    ragdollBlend);
            }
        }

        // 루트 바디를 래그돌 최종 pelvis 위치에 정렬.
        public void AlignRootBodyToPelvis(Rigidbody rootBody, Transform pelvis)
        {
            Vector3 pelvisPos = pelvis.position;
            float groundY = GetGroundY(pelvisPos);
            rootBody.position = new Vector3(pelvisPos.x, groundY, pelvisPos.z);

            Vector3 hipsForward = pelvis.rotation * Vector3.forward;
            hipsForward.y = 0f;

            if (hipsForward.sqrMagnitude > MinDirectionSqrMagnitude)
                rootBody.rotation = Quaternion.LookRotation(hipsForward);
        }

        private void MatchRootToRagdolledPose(Rigidbody rootBody)
        {
            Transform hipBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 offset = _ragdolledHipPosition - hipBone.position;
            Vector3 newRootPos = rootBody.position + offset;

            newRootPos.y = GetGroundY(newRootPos);
            rootBody.position = newRootPos;

            Vector3 ragdolledDir = _ragdolledHeadPosition - _ragdolledFeetPosition;
            ragdolledDir.y = 0f;

            Transform leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 animFeetPos = 0.5f * (leftFoot.position + rightFoot.position);
            Vector3 animDir = _animator.GetBoneTransform(HumanBodyBones.Head).position - animFeetPos;
            animDir.y = 0f;

            if (ragdolledDir.sqrMagnitude > MinDirectionSqrMagnitude
                && animDir.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                rootBody.rotation *= Quaternion.FromToRotation(
                    animDir.normalized, ragdolledDir.normalized);
            }
        }

        private float GetGroundY(Vector3 origin)
        {
            Vector3 rayOrigin = origin + Vector3.up * RayOriginUpOffset;

            if (Physics.Raycast(
                    rayOrigin, Vector3.down, out RaycastHit hit,
                    _groundCheckDistance, _groundLayer))
                return hit.point.y;

            return origin.y;
        }
    }
}
