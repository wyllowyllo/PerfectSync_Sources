using Core.Utilities;
using InGame.Player.Network;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Animation
{
    public class PlayerAnimation : MonoBehaviour
    {
        private const int FinishVariantCount = 3;

        [SerializeField] private Animator _animator;

        private InGameCustomizationApplier _customizationApplier;
        private PhotonView _photonView;

        private static readonly int s_speedHash = Animator.StringToHash("Speed");
        private static readonly int s_isGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int s_jumpHash = Animator.StringToHash("Jump");
        private static readonly int s_diveHash = Animator.StringToHash("Dive");
        private static readonly int s_getUpFromBackHash = Animator.StringToHash("GetUpFromBack");
        private static readonly int s_getUpFromBellyHash = Animator.StringToHash("GetUpFromBelly");
        private static readonly int s_stumbleHash = Animator.StringToHash("Stumble");
        private static readonly int s_trampolineLaunchHash = Animator.StringToHash("TrampolineLaunch");
        private static readonly int s_finishHash = Animator.StringToHash("Finish");
        private static readonly int s_finishVariantHash = Animator.StringToHash("FinishVariant");
        private static readonly int s_locomotionHash = Animator.StringToHash("Locomotion");
        private static readonly int s_jumpLandHash = Animator.StringToHash("JumpLand");
        private static readonly int s_jumpStateHash = Animator.StringToHash("Jump");
        private static readonly int s_jumpAirHash = Animator.StringToHash("JumpAir");
        private static readonly int s_trampolineFlyHash = Animator.StringToHash("TrampolineFly");

        private void Awake()
        {
            _customizationApplier = GetComponentInParent<InGameCustomizationApplier>();
            _photonView = GetComponentInParent<PhotonView>();
        }

        private void OnEnable()
        {
            RaceRankingManager.OnTeamFinished += HandleTeamFinished;
        }

        private void OnDisable()
        {
            RaceRankingManager.OnTeamFinished -= HandleTeamFinished;
        }

        public void Locomotion(bool isGrounded, float speed)
        {
            _animator.SetFloat(s_speedHash, speed);
            _animator.SetBool(s_isGroundedHash, isGrounded);
        }

        public void Jump()
        {
            // 이전 프레임에 소비되지 못해 큐에 남은 Jump 트리거 제거 후 재발행.
            _animator.ResetTrigger(s_jumpHash);
            _animator.SetTrigger(s_jumpHash);
        }

        public void Dive()
        {
            _animator.SetTrigger(s_diveHash);
        }

        public void GetUp(bool isFaceUp)
        {
            _animator.ResetTrigger(s_jumpHash);
            _animator.ResetTrigger(s_diveHash);
            _animator.SetFloat(s_speedHash, 0f);
            _animator.SetBool(s_isGroundedHash, true);

            _animator.SetBool(s_getUpFromBackHash, isFaceUp);
            _animator.SetBool(s_getUpFromBellyHash, !isFaceUp);
        }

        public void ClearGetUpState()
        {
            _animator.SetBool(s_getUpFromBackHash, false);
            _animator.SetBool(s_getUpFromBellyHash, false);
        }

        // Any State → Jump 전이가 없으므로 Locomotion/JumpLand state에서만 Jump 트리거가 소비됨.
        // 물리 점프 게이트를 이 함수와 일치시켜 "물리만 발동, 애니 누락" 불일치를 방지.
        public bool CanAcceptJump()
        {
            if (_animator.IsInTransition(0))
            {
                int currentHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                int nextHash = _animator.GetNextAnimatorStateInfo(0).shortNameHash;
                return IsJumpAcceptingHash(currentHash) && IsJumpAcceptingHash(nextHash);
            }

            int hash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return IsJumpAcceptingHash(hash);
        }

        private bool IsJumpAcceptingHash(int hash)
        {
            return hash == s_locomotionHash || hash == s_jumpLandHash;
        }

        // Dive 트리거는 JumpAir/TrampolineFly에서만 소비되므로 공중 상태 그룹에서만 허용.
        // Jump state는 곧 JumpAir로 자동 전이되므로 포함해도 트리거가 큐잉되어 정상 소비된다.
        public bool CanAcceptDive()
        {
            if (_animator.IsInTransition(0))
            {
                int currentHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                int nextHash = _animator.GetNextAnimatorStateInfo(0).shortNameHash;
                return IsDiveAcceptingHash(currentHash) || IsDiveAcceptingHash(nextHash);
            }

            int hash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return IsDiveAcceptingHash(hash);
        }

        private bool IsDiveAcceptingHash(int hash)
        {
            return hash == s_jumpStateHash
                || hash == s_jumpAirHash
                || hash == s_trampolineFlyHash;
        }

        public void Stumble()
        {
            _animator.SetTrigger(s_stumbleHash);
        }

        public void ClearStumbleState()
        {
            _animator.ResetTrigger(s_stumbleHash);
        }

        public void TrampolineLaunch()
        {
            _animator.SetTrigger(s_trampolineLaunchHash);
        }

        // OnTeamFinished는 모든 클라에서 동시 발행, ViewID는 전 클라 공통 → RPC 없이 variant 일치.
        private void HandleTeamFinished(int teamNumber, int place)
        {
            if (_customizationApplier == null || _photonView == null) return;
            if (teamNumber != _customizationApplier.TeamNumber) return;

            int variant = DeterministicHash.PickIndex(_photonView.ViewID, FinishVariantCount);
            Finish(variant);
        }

        private void Finish(int variantIndex)
        {
            // Trigger 소비 전 Int가 반영되어야 Any State 전이에서 올바른 분기가 선택됨.
            _animator.SetInteger(s_finishVariantHash, variantIndex);
            _animator.SetTrigger(s_finishHash);
        }
    }
}
