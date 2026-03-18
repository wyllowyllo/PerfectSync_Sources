using UnityEngine;

namespace Player.Controller
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimation : MonoBehaviour
    {
        private Animator _animator;

        private static readonly int SSpeedHash = Animator.StringToHash("Speed");
        private static readonly int SIsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int SJumpHash = Animator.StringToHash("Jump");
        private static readonly int SDiveHash = Animator.StringToHash("Dive");
        private static readonly int SGetUpFromBackHash = Animator.StringToHash("GetUpFromBack");
        private static readonly int SGetUpFromBellyHash = Animator.StringToHash("GetUpFromBelly");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void UpdateLocomotion(bool isGrounded, float speed)
        {
            _animator.SetFloat(SSpeedHash, speed);
            _animator.SetBool(SIsGroundedHash, isGrounded);
        }

        public void TriggerJump()
        {
            _animator.SetTrigger(SJumpHash);
        }

        public void TriggerDive()
        {
            _animator.SetTrigger(SDiveHash);
        }

        public void PlayGetUp(bool isFaceUp)
        {
            _animator.ResetTrigger(SJumpHash);
            _animator.ResetTrigger(SDiveHash);
            _animator.SetFloat(SSpeedHash, 0f);
            _animator.SetBool(SIsGroundedHash, true);

            _animator.SetBool(SGetUpFromBackHash, isFaceUp);
            _animator.SetBool(SGetUpFromBellyHash, !isFaceUp);
        }

        public void ClearGetUpState()
        {
            _animator.SetBool(SGetUpFromBackHash, false);
            _animator.SetBool(SGetUpFromBellyHash, false);
        }

        public void ResetToLocomotion()
        {
            _animator.ResetTrigger(SJumpHash);
            _animator.ResetTrigger(SDiveHash);
            _animator.Play("Locomotion", 0, 0f);
            _animator.SetFloat(SSpeedHash, 0f);
            _animator.SetBool(SIsGroundedHash, true);
        }
    }
}
