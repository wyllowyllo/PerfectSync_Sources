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

        private static readonly int s_jumpLandHash = Animator.StringToHash("JumpLand");

        // GetUp 또는 JumpLand 재생/전환 중이면 점프 차단.
        // 단, 회복 상태에서 빠져나가는 전환 중에는 점프 허용.
        public bool IsJumpLocked()
        {
            if (_animator.IsInTransition(0))
            {
                int nextHash = _animator.GetNextAnimatorStateInfo(0).shortNameHash;
                return IsRecoveryHash(nextHash);
            }

            int hash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return IsRecoveryHash(hash);
        }

        private bool IsRecoveryHash(int hash)
        {
            return hash == s_getUpFromBackHash
                || hash == s_getUpFromBellyHash;
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
