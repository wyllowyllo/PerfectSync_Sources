using InGame.Audio;
using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    public class TeamModeSoundTrigger : MonoBehaviour
    {
        [Header("Invincible")]
        [SerializeField] private SpatialSfxProfile _invincibleEnterProfile;
        [SerializeField] private SpatialSfxProfile _invincibleExitProfile;

        [Header("Slot")]
        [SerializeField] private SfxProfile _slotSpinProfile;
        [SerializeField] private SfxProfile _slotMatchProfile;
        [SerializeField] private SfxProfile _slotNoMatchProfile;

        private InvincibleModeController _invincibleController;
        private TeamModeSynchronizer _synchronizer;

        private void Start()
        {
            _invincibleController = GetComponent<InvincibleModeController>();
            if (_invincibleController != null)
            {
                _invincibleController.OnInvincibleEnter += HandleInvincibleEnter;
                _invincibleController.OnInvincibleExit += HandleInvincibleExit;
            }

            _synchronizer = GetComponent<TeamModeSynchronizer>();
            if (_synchronizer != null)
            {
                _synchronizer.OnSlotSpinStarted += HandleSlotSpinStarted;
                _synchronizer.OnSlotResultReceived += HandleSlotResultReceived;
            }
        }

        private void OnDestroy()
        {
            if (_invincibleController != null)
            {
                _invincibleController.OnInvincibleEnter -= HandleInvincibleEnter;
                _invincibleController.OnInvincibleExit -= HandleInvincibleExit;
            }

            if (_synchronizer != null)
            {
                _synchronizer.OnSlotSpinStarted -= HandleSlotSpinStarted;
                _synchronizer.OnSlotResultReceived -= HandleSlotResultReceived;
            }
        }

        private void HandleInvincibleEnter()
        {
            InGameSfxManager.Instance?.EmitSpatialOn(_invincibleEnterProfile, transform, this);
        }

        private void HandleInvincibleExit()
        {
            InGameSfxManager.Instance?.EmitSpatialOn(_invincibleExitProfile, transform, this);
        }

        private void HandleSlotSpinStarted()
        {
            InGameSfxManager.Instance?.PlaySfx2D(_slotSpinProfile);
        }

        private void HandleSlotResultReceived(int[] symbols, bool isMatch)
        {
            InGameSfxManager.Instance?.PlaySfx2D(isMatch ? _slotMatchProfile : _slotNoMatchProfile);
        }
    }
}
