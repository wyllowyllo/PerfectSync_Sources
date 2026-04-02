using System.Collections;
using InGame.Team;
using UnityEngine;

namespace InGame.Effect
{
    /// <summary>
    /// 무적 모드 발동·해제 시 시각 이펙트를 재생한다.
    /// 발동 순간: 충격파 + 파티클 버스트 + 플래시.
    /// 지속 중:  무지개빛 Emission 순환.
    /// MergedBody 하위에 부착.
    /// </summary>
    public class InvincibleActivationEffect : MonoBehaviour
    {
        [Header("Shockwave")]
        [Tooltip("발동 시 확산되는 충격파 파티클 프리팹")]
        [SerializeField] private ParticleSystem _shockwavePrefab;

        [Header("Burst Particles")]
        [Tooltip("발동 시 터지는 별/반짝이 파티클 프리팹")]
        [SerializeField] private ParticleSystem _burstPrefab;

        [Header("Flash")]
        [SerializeField] private float _flashDuration = 0.25f;
        [SerializeField] private float _flashIntensity = 5f;

        [Header("Rainbow Glow")]
        [Tooltip("무적 중 무지개빛 색상 순환 속도")]
        [SerializeField] private float _glowCycleSpeed = 2f;
        [Tooltip("무적 중 발광 강도")]
        [SerializeField] private float _glowIntensity = 1.5f;

        private InvincibleModeController _controller;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private bool _isGlowActive;
        private Coroutine _flashCoroutine;
        private bool _emissionEnabled;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _propBlock = new MaterialPropertyBlock();
        }

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

        private void Update()
        {
            if (!_isGlowActive) return;
            if (_flashCoroutine != null) return;

            SetEmissionColor(GetRainbowColor());
        }

        // ── 발동 / 해제 ──────────────────────────────────────

        private void HandleEnter()
        {
            EnsureEmissionEnabled();

            SpawnOneShot(_shockwavePrefab);
            SpawnOneShot(_burstPrefab);

            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine());

            _isGlowActive = true;
        }

        private void HandleExit()
        {
            _isGlowActive = false;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            SetEmissionColor(Color.black);
        }

        // ── 플래시 ────────────────────────────────────────────

        private IEnumerator FlashRoutine()
        {
            Color peak = Color.white * _flashIntensity;
            SetEmissionColor(peak);

            float elapsed = 0f;
            while (elapsed < _flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _flashDuration;
                Color target = _isGlowActive ? GetRainbowColor() : Color.black;
                SetEmissionColor(Color.Lerp(peak, target, t));
                yield return null;
            }

            _flashCoroutine = null;
        }

        // ── 무지개 글로우 ─────────────────────────────────────

        private Color GetRainbowColor()
        {
            float hue = Mathf.Repeat(Time.time * _glowCycleSpeed, 1f);
            return Color.HSVToRGB(hue, 1f, 1f) * _glowIntensity;
        }

        // ── 머티리얼 유틸 ─────────────────────────────────────

        private void SetEmissionColor(Color color)
        {
            foreach (var rend in _renderers)
            {
                if (rend == null) continue;
                rend.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorId, color);
                rend.SetPropertyBlock(_propBlock);
            }
        }

        /// <summary>
        /// 최초 1회만 머티리얼 인스턴스를 생성하고 _EMISSION 키워드를 활성화한다.
        /// (인스턴스이므로 다른 오브젝트의 머티리얼에 영향 없음)
        /// </summary>
        private void EnsureEmissionEnabled()
        {
            if (_emissionEnabled) return;
            _emissionEnabled = true;

            foreach (var rend in _renderers)
            {
                if (rend == null) continue;
                foreach (var mat in rend.materials)
                    mat.EnableKeyword("_EMISSION");
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
