using System.Collections;
using InGame.Gimmick;
using UnityEngine;

namespace InGame.Player.Rendering
{
    // RespawnHandler의 무적 이벤트를 구독하여 캐릭터 블링크 연출.
    // MergedBody 하위에 부착.
    public class RespawnBlinkEffect : MonoBehaviour
    {
        [SerializeField] private float _blinkInterval = 0.15f;

        private Renderer[] _renderers;
        private Coroutine _blinkCoroutine;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Start()
        {
            var respawnHandler = GetComponentInParent<RespawnHandler>();
            if (respawnHandler == null) return;

            respawnHandler.OnRespawnInvincibleStart += StartBlink;
            respawnHandler.OnRespawnInvincibleEnd += StopBlink;
        }

        private void OnDestroy()
        {
            var respawnHandler = GetComponentInParent<RespawnHandler>();
            if (respawnHandler == null) return;

            respawnHandler.OnRespawnInvincibleStart -= StartBlink;
            respawnHandler.OnRespawnInvincibleEnd -= StopBlink;
        }

        private void StartBlink()
        {
            if (_blinkCoroutine != null)
                StopCoroutine(_blinkCoroutine);

            _blinkCoroutine = StartCoroutine(BlinkCoroutine());
        }

        private void StopBlink()
        {
            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }

            SetRenderersVisible(true);
        }

        private IEnumerator BlinkCoroutine()
        {
            var wait = new WaitForSeconds(_blinkInterval);
            bool visible = true;

            while (true)
            {
                visible = !visible;
                SetRenderersVisible(visible);
                yield return wait;
            }
        }

        private void SetRenderersVisible(bool visible)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }
    }
}
