using InGame.Team;
using UnityEngine;

namespace InGame.Effect
{
    /// <summary>
    /// 무적 모드 발동·해제 시 시각 이펙트를 재생한다.
    /// 발동 순간: 충격파 + 파티클 버스트 (one-shot).
    /// 지속 중:  루프 이펙트 프리팹 재생.
    /// MergedBody 하위에 부착.
    /// </summary>
    public class InvincibleActivationEffect : MonoBehaviour
    {
        [Header("One-Shot (발동 순간)")]
        [Tooltip("발동 시 확산되는 충격파 파티클 프리팹")]
        [SerializeField] private ParticleSystem _shockwavePrefab;
        [Tooltip("발동 시 터지는 별/반짝이 파티클 프리팹")]
        [SerializeField] private ParticleSystem _burstPrefab;

        [Header("Loop (무적 지속 중)")]
        [Tooltip("무적 지속 중 재생할 루프 파티클 프리팹")]
        [SerializeField] private ParticleSystem _loopEffectPrefab;

        private InvincibleModeController _controller;
        private ParticleSystem _loopEffectInstance;

        private void Start()
        {
            _controller = GetComponentInParent<InvincibleModeController>();
            if (_controller == null) return;

            _controller.OnInvincibleEnter += HandleEnter;
            _controller.OnInvincibleExit += HandleExit;
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.OnInvincibleEnter -= HandleEnter;
                _controller.OnInvincibleExit -= HandleExit;
            }
        }

        private void HandleEnter()
        {
            SpawnOneShot(_shockwavePrefab);
            SpawnOneShot(_burstPrefab);

            if (_loopEffectPrefab != null && _loopEffectInstance == null)
            {
                _loopEffectInstance = Instantiate(_loopEffectPrefab, transform);
                _loopEffectInstance.Play();
            }
        }

        private void HandleExit()
        {
            if (_loopEffectInstance != null)
            {
                _loopEffectInstance.Stop();
                Destroy(_loopEffectInstance.gameObject);
                _loopEffectInstance = null;
            }
        }

        private void SpawnOneShot(ParticleSystem prefab)
        {
            if (prefab == null) return;
            var instance = Instantiate(prefab, transform.position, Quaternion.identity);
            instance.Play();
            float lifetime = instance.main.duration + instance.main.startLifetime.constantMax;
            Destroy(instance.gameObject, lifetime);
        }
    }
}
