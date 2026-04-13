using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    public class TeamModeSoundTrigger : MonoBehaviour
    {
        [Header("Invincible")]
        [SerializeField] private SpatialSfxPlayer _invincibleEnterSfx;
        [SerializeField] private SpatialSfxPlayer _invincibleExitSfx;

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
            if (_invincibleEnterSfx != null)
                _invincibleEnterSfx.Play();
        }

        private void HandleInvincibleExit()
        {
            if (_invincibleExitSfx != null)
                _invincibleExitSfx.Play();
        }

        private void HandleSlotSpinStarted()
        {
            PlaySfx(_slotSpinProfile);
        }

        private void HandleSlotResultReceived(int[] symbols, bool isMatch)
        {
            PlaySfx(isMatch ? _slotMatchProfile : _slotNoMatchProfile);
        }

        private static void PlaySfx(SfxProfile profile)
        {
            if (profile == null || AudioManager.Instance == null)
                return;

            AudioClip clip = profile.GetRandomClip();
            if (clip == null)
                return;

            AudioManager.Instance.Play(AudioType.Sfx, clip);
        }
    }
}
