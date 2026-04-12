using UnityEngine;

namespace InGame.Gimmick
{
    public class TrampolineSoundTrigger : MonoBehaviour
    {
        [SerializeField] private SpatialSfxPlayer _bounceSfx;

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
            if (_bounceSfx != null)
                _bounceSfx.Play();
        }
    }
}
