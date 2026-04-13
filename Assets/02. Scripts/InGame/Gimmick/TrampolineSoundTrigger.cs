using InGame.Audio;
using UnityEngine;

namespace InGame.Gimmick
{
    public class TrampolineSoundTrigger : MonoBehaviour
    {
        [SerializeField] private SpatialSfxProfile _bounceProfile;

        private TrampolineTrigger _trampoline;

        private void Awake()
        {
            _trampoline = GetComponent<TrampolineTrigger>();
        }

        private void OnEnable()
        {
            if (_trampoline != null)
                _trampoline.OnBounced += HandleBounced;
        }

        private void OnDisable()
        {
            if (_trampoline != null)
                _trampoline.OnBounced -= HandleBounced;
        }

        private void HandleBounced()
        {
            InGameSfxManager.Instance?.EmitSpatialAt(_bounceProfile, transform.position, this);
        }
    }
}
