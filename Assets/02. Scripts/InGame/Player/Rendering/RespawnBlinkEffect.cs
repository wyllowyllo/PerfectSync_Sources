using InGame.Gimmick;
using UnityEngine;

namespace InGame.Player.Rendering
{
    // RespawnHandler의 무적 이벤트를 구독하여 리스폰 무적 VFX를 재생한다.
    // MergedBody 하위에 부착.
    public class RespawnBlinkEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _blinkEffectPrefab;

        private ParticleSystem _blinkEffectInstance;

        private void Start()
        {
            var respawnHandler = GetComponentInParent<RespawnHandler>();
            if (respawnHandler == null) return;

            respawnHandler.OnRespawnInvincibleStart += StartEffect;
            respawnHandler.OnRespawnInvincibleEnd += StopEffect;
        }

        private void OnDestroy()
        {
            var respawnHandler = GetComponentInParent<RespawnHandler>();
            if (respawnHandler == null) return;

            respawnHandler.OnRespawnInvincibleStart -= StartEffect;
            respawnHandler.OnRespawnInvincibleEnd -= StopEffect;
        }

        private void StartEffect()
        {
            if (_blinkEffectPrefab == null || _blinkEffectInstance != null) return;

            _blinkEffectInstance = Instantiate(_blinkEffectPrefab, transform);
            _blinkEffectInstance.Play();
        }

        private void StopEffect()
        {
            if (_blinkEffectInstance == null) return;

            _blinkEffectInstance.Stop();
            Destroy(_blinkEffectInstance.gameObject);
            _blinkEffectInstance = null;
        }
    }
}
