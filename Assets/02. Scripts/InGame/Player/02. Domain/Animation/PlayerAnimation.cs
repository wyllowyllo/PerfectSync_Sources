using UnityEngine;

namespace InGame.Player._02._Domain.Animation
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimation : MonoBehaviour
    {
        private Animator _animator;

        private static readonly int s_speedHash = Animator.StringToHash("Speed");
        private static readonly int s_isGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int s_jumpHash = Animator.StringToHash("Jump");
        private static readonly int s_diveHash = Animator.StringToHash("Dive");
        private static readonly int s_getUpFromBackHash = Animator.StringToHash("GetUpFromBack");
        private static readonly int s_getUpFromBellyHash = Animator.StringToHash("GetUpFromBelly");
        private static readonly int s_diveLandHash = Animator.StringToHash("DiveLand");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
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
            _animator.SetBool(s_diveLandHash, false);
            _animator.SetTrigger(s_diveHash);
        }

        public void Land(bool active)
        {
            _animator.SetBool(s_diveLandHash, active);
        }

        public void GetUp(bool isFaceUp)
        {
            _animator.ResetTrigger(s_jumpHash);
            _animator.ResetTrigger(s_diveHash);
            _animator.SetBool(s_diveLandHash, false);
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

    }
}
