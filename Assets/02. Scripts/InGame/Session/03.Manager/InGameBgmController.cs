using System;
using System.Collections;
using UnityEngine;

public class InGameBgmController : MonoBehaviour
{
    public enum EInGameSubState
    {
        Racing,
        Ceremony,
    }

    [Serializable]
    private class Entry
    {
        public EInGameSubState State;
        public string BgmAddress;
        [Range(0f, 5f)] public float CrossfadeDuration = 1f;
    }

    private const string InvincibleLayerChildName = "InvincibleBgmLayer";

    public static InGameBgmController Instance { get; private set; }

    [SerializeField] private Entry[] _entries;

    [Header("Invincible BGM")]
    [SerializeField] private AudioClip _invincibleBgmClip;
    [SerializeField, Range(0f, 5f)] private float _invincibleMuffleFade = 0.3f;
    [SerializeField, Range(0f, 5f)] private float _invincibleLayerFade = 0.5f;

    private EInGameSubState _current = EInGameSubState.Racing;
    private InGameManager _inGameManager;
    private AudioSource _invincibleLayerSource;
    private Coroutine _invincibleRoutine;
    private bool _invincibleActive;
    private bool _isInvincibleFading;

    private float EffectiveBgmVolume =>
        AudioManager.Instance != null
            ? AudioManager.Instance.MasterVolume * AudioManager.Instance.BgmVolume
            : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[InGameBgmController] Duplicate instance on '{name}'; destroying.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateInvincibleLayerSource();
        AudioManager.VolumesChanged += HandleVolumesChanged;
    }

    private void Start()
    {
        Apply(EInGameSubState.Racing);

        _inGameManager = InGameManager.Instance;
        if (_inGameManager != null)
            _inGameManager.OnCeremonyReady += HandleCeremonyReady;
    }

    private void OnDisable()
    {
        if (_inGameManager != null)
            _inGameManager.OnCeremonyReady -= HandleCeremonyReady;
        _inGameManager = null;

        // 무적 중 씬 언로드 시 AudioManager(DontDestroyOnLoad)에 머플 잔존 방지.
        StopInvincibleBgm();
    }

    private void OnDestroy()
    {
        AudioManager.VolumesChanged -= HandleVolumesChanged;

        if (Instance == this)
            Instance = null;
    }

    public void PlayInvincibleBgm()
    {
        if (_invincibleActive) return;
        if (_invincibleBgmClip == null) return;

        _invincibleActive = true;
        AudioManager.Instance?.SetBgmMuffled(true, _invincibleMuffleFade);

        if (_invincibleRoutine != null)
            StopCoroutine(_invincibleRoutine);
        _invincibleRoutine = StartCoroutine(CoPlayInvincibleLayer(_invincibleLayerFade));
    }

    public void StopInvincibleBgm()
    {
        if (!_invincibleActive) return;
        _invincibleActive = false;

        AudioManager.Instance?.SetBgmMuffled(false, _invincibleMuffleFade);

        if (_invincibleLayerSource == null)
            return;

        if (_invincibleRoutine != null)
            StopCoroutine(_invincibleRoutine);
        _invincibleRoutine = StartCoroutine(CoStopInvincibleLayer(_invincibleLayerFade));
    }

    private void CreateInvincibleLayerSource()
    {
        var child = new GameObject(InvincibleLayerChildName);
        child.transform.SetParent(transform, false);
        _invincibleLayerSource = child.AddComponent<AudioSource>();
        _invincibleLayerSource.playOnAwake = false;
        _invincibleLayerSource.loop = true;
        _invincibleLayerSource.volume = 0f;

        // 첫 Play 시점에 발생하는 디코더 초기화 블로킹 제거:
        // 1) 클립 오디오 데이터 강제 상주, 2) AudioSource DSP 버퍼 워밍.
        if (_invincibleBgmClip != null)
        {
            _invincibleBgmClip.LoadAudioData();
            _invincibleLayerSource.clip = _invincibleBgmClip;
            _invincibleLayerSource.Play();
            _invincibleLayerSource.Pause();
            _invincibleLayerSource.time = 0f;
        }
    }

    private void HandleVolumesChanged()
    {
        if (!_invincibleActive) return;
        if (_isInvincibleFading) return;
        if (_invincibleLayerSource == null) return;

        _invincibleLayerSource.volume = EffectiveBgmVolume;
    }

    private IEnumerator CoPlayInvincibleLayer(float fadeDuration)
    {
        // Awake 워밍 결과 Paused 상태 유지. Pause/Play 재개로 디코드 블로킹 회피.
        _invincibleLayerSource.time = 0f;
        _invincibleLayerSource.volume = 0f;
        _invincibleLayerSource.UnPause();
        if (!_invincibleLayerSource.isPlaying)
            _invincibleLayerSource.Play();

        yield return CoFadeInvincibleLayer(0f, EffectiveBgmVolume, fadeDuration);
        _invincibleRoutine = null;
    }

    private IEnumerator CoStopInvincibleLayer(float fadeDuration)
    {
        yield return CoFadeInvincibleLayer(_invincibleLayerSource.volume, 0f, fadeDuration);

        // 재발동 시 다시 디코드되지 않도록 clip은 유지, Pause로만 정지.
        _invincibleLayerSource.Pause();
        _invincibleLayerSource.time = 0f;
        _invincibleRoutine = null;
    }

    private IEnumerator CoFadeInvincibleLayer(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            _invincibleLayerSource.volume = to;
            yield break;
        }

        _isInvincibleFading = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _invincibleLayerSource.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }
        _invincibleLayerSource.volume = to;
        _isInvincibleFading = false;
    }

    private void HandleCeremonyReady()
    {
        Apply(EInGameSubState.Ceremony);
    }

    private void Apply(EInGameSubState next)
    {
        _current = next;

        Entry entry = FindEntry(next);
        if (entry == null || string.IsNullOrEmpty(entry.BgmAddress))
            return;

        AudioManager.Instance?.PlayBgmByAddress(entry.BgmAddress, entry.CrossfadeDuration);
    }

    private Entry FindEntry(EInGameSubState state)
    {
        if (_entries == null)
            return null;

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] != null && _entries[i].State == state)
                return _entries[i];
        }
        return null;
    }
}
