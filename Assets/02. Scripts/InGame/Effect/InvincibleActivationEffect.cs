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

        [Header("Spawn Anchor")]
        [Tooltip("파티클 스폰 기준 Transform. 미지정 시 컴포넌트가 붙은 Transform 사용. 캐릭터 중앙(허리/가슴 본 등)을 지정하면 발밑이 아닌 중앙에서 재생된다.")]
        [SerializeField] private Transform _spawnAnchor;

        [Header("Scale")]
        [Tooltip("원샷(충격파/버스트) 파티클에 적용할 균등 스케일 배율.")]
        [SerializeField, Min(0.01f)] private float _oneShotScale = 1f;
        [Tooltip("루프 파티클에 적용할 균등 스케일 배율.")]
        [SerializeField, Min(0.01f)] private float _loopScale = 1f;

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
                Transform parent = _spawnAnchor != null ? _spawnAnchor : transform;
                _loopEffectInstance = Instantiate(_loopEffectPrefab, parent);
                _loopEffectInstance.transform.localPosition = Vector3.zero;
                _loopEffectInstance.transform.localRotation = Quaternion.identity;
                _loopEffectInstance.transform.localScale = Vector3.one * _loopScale;
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
            Vector3 spawnPosition = _spawnAnchor != null ? _spawnAnchor.position : transform.position;
            var instance = Instantiate(prefab, spawnPosition, Quaternion.identity);
            instance.transform.localScale = Vector3.one * _oneShotScale;
            instance.Play();
            float lifetime = instance.main.duration + instance.main.startLifetime.constantMax;
            Destroy(instance.gameObject, lifetime);
        }
    }
}
