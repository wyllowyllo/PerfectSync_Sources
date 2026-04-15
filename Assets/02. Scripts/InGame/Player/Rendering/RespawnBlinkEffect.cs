using System.Collections;
using InGame.Gimmick;
using UnityEngine;

namespace InGame.Player.Rendering
{
    // RespawnHandler의 무적 이벤트를 구독하여 스폰 VFX 버스트 + 무적 깜빡임을 처리한다.
    // MergedBody 하위에 부착.
    public class RespawnBlinkEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _blinkEffectPrefab;
        [SerializeField] private float _blinkInterval = 0.1f;

        private RespawnHandler _respawnHandler;
        private Renderer[] _renderers;
        private Coroutine _blinkCoroutine;

        private void Start()
        {
            _respawnHandler = GetComponentInParent<RespawnHandler>();
            if (_respawnHandler == null) return;

            _renderers = GetComponentsInChildren<Renderer>();

            _respawnHandler.OnRespawnInvincibleStart += HandleInvincibleStart;
            _respawnHandler.OnRespawnInvincibleEnd += HandleInvincibleEnd;
        }

        private void OnDestroy()
        {
            if (_respawnHandler == null) return;

            _respawnHandler.OnRespawnInvincibleStart -= HandleInvincibleStart;
            _respawnHandler.OnRespawnInvincibleEnd -= HandleInvincibleEnd;
        }

        private void HandleInvincibleStart()
        {
            PlaySpawnBurst();

            if (_blinkCoroutine != null)
                StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }

        private void HandleInvincibleEnd()
        {
            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }
            SetRenderersVisible(true);
        }

        private void PlaySpawnBurst()
        {
            if (_blinkEffectPrefab == null) return;

            var instance = Instantiate(_blinkEffectPrefab, transform.position, Quaternion.identity);
            instance.Play();

            float duration = instance.main.duration + instance.main.startLifetime.constantMax;
            Destroy(instance.gameObject, duration);
        }

        private IEnumerator BlinkCoroutine()
        {
            var wait = new WaitForSeconds(_blinkInterval);
            while (true)
            {
                SetRenderersVisible(false);
                yield return wait;
                SetRenderersVisible(true);
                yield return wait;
            }
        }

        private void SetRenderersVisible(bool visible)
        {
            foreach (var r in _renderers)
            {
                if (r != null)
                    r.enabled = visible;
            }
        }
    }
}
