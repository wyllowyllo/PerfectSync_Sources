using UnityEngine;

namespace InGame.Player.Animation
{
    public class PlayerAnimation : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private static readonly int s_speedHash = Animator.StringToHash("Speed");
        private static readonly int s_isGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int s_jumpHash = Animator.StringToHash("Jump");
        private static readonly int s_diveHash = Animator.StringToHash("Dive");
        private static readonly int s_getUpFromBackHash = Animator.StringToHash("GetUpFromBack");
        private static readonly int s_getUpFromBellyHash = Animator.StringToHash("GetUpFromBelly");
        private static readonly int s_stumbleHash = Animator.StringToHash("Stumble");
        private static readonly int s_trampolineLaunchHash = Animator.StringToHash("TrampolineLaunch");

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
    }
}
