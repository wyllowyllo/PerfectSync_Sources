using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    public class SlotSoundTrigger : MonoBehaviour
    {
        [SerializeField] private SfxProfile _spinProfile;
        [SerializeField] private SfxProfile _matchProfile;
        [SerializeField] private SfxProfile _noMatchProfile;

        private TeamModeSynchronizer _synchronizer;

        private void Start()
        {
            _synchronizer = GetComponent<TeamModeSynchronizer>();
            if (_synchronizer == null)
                return;

            _synchronizer.OnSlotSpinStarted += HandleSpinStarted;
            _synchronizer.OnSlotResultReceived += HandleResultReceived;
        }

        private void OnDestroy()
        {
            if (_synchronizer == null)
                return;

            _synchronizer.OnSlotSpinStarted -= HandleSpinStarted;
            _synchronizer.OnSlotResultReceived -= HandleResultReceived;
        }

        private void HandleSpinStarted()
        {
            PlaySfx(_spinProfile);
        }

        private void HandleResultReceived(int[] symbols, bool isMatch)
        {
            PlaySfx(isMatch ? _matchProfile : _noMatchProfile);
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
