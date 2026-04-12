using UnityEngine;

namespace InGame.Team
{
    public class InvincibleSoundTrigger : MonoBehaviour
    {
        [SerializeField] private SpatialSfxPlayer _enterSfx;
        [SerializeField] private SpatialSfxPlayer _exitSfx;

        private InvincibleModeController _controller;

        private void Start()
        {
            _controller = GetComponent<InvincibleModeController>();
            if (_controller == null)
                return;

            _controller.OnInvincibleEnter += HandleEnter;
            _controller.OnInvincibleExit += HandleExit;
        }

        private void OnDestroy()
        {
            if (_controller == null)
                return;

            _controller.OnInvincibleEnter -= HandleEnter;
            _controller.OnInvincibleExit -= HandleExit;
        }

        private void HandleEnter()
        {
            if (_enterSfx != null)
                _enterSfx.Play();
        }

        private void HandleExit()
        {
            if (_exitSfx != null)
                _exitSfx.Play();
        }
    }
}
