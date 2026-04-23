using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRandomMapRouletteEffect : MonoBehaviour
{
    [Header("Target Images (Left / Center / Right)")]
    [SerializeField] private Image[] _images = new Image[3];

    [Header("Punch Target Parents (Left / Center / Right)")]
    [SerializeField] private Transform[] _punchTargets = new Transform[3];

    [Header("Center Map Name")]
    [SerializeField] private TMP_Text _centerMapName;
    [SerializeField] private Transform _centerMapNamePunchTarget;

    [Header("Timing")]
    [SerializeField] private float _totalDuration = 3f;
    [SerializeField] private float _startInterval = 0.05f;
    [SerializeField] private float _endInterval = 0.5f;

    [Header("Punch Scale")]
    [SerializeField] private Vector3 _punchScale = new Vector3(0.25f, 0.25f, 0f);
    [SerializeField] private float _punchDuration = 0.2f;
    [SerializeField] private int _punchVibrato = 6;
    [SerializeField] private float _punchElasticity = 0.8f;

    [Header("SFX")]
    [SerializeField] private SfxProfile _shiftSfx;

    private MapDefinition[] _maps;
    private Coroutine _routine;
    private Vector3[] _baseScales;
    private Vector3 _nameBaseScale;
    private int _currentIndex;
    private bool _sfxSuppressed;

    /// <summary>
    /// 캔버스가 꺼져있어 OnEnable/Awake가 실행되지 않으므로,
    /// 캔버스를 켜기 전에 반드시 이 메서드를 호출하여 맵 데이터를 주입합니다.
    /// </summary>
    public void Initialize(MapDefinition[] maps)
    {
        _maps = maps;

        _baseScales = new Vector3[_punchTargets.Length];
        for (int i = 0; i < _punchTargets.Length; i++)
            if (_punchTargets[i] != null)
                _baseScales[i] = _punchTargets[i].localScale;

        if (_centerMapNamePunchTarget != null)
            _nameBaseScale = _centerMapNamePunchTarget.localScale;
    }

    private void OnEnable()
    {
        if (_maps == null || _maps.Length == 0) return;
        _sfxSuppressed = false;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RouletteRoutine());
    }

    public void StopShiftSfx()
    {
        _sfxSuppressed = true;
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        KillAllPunchTweens();
    }

    private IEnumerator RouletteRoutine()
    {
        if (_images.Length < 3) yield break;

        _currentIndex = Random.Range(0, _maps.Length);

        // ── 초기 3개 이미지 + 이름 세팅 ──
        for (int i = 0; i < 3; i++)
        {
            int idx = (_currentIndex + i) % _maps.Length;
            _images[i].sprite = _maps[idx].Thumbnail;
        }

        int centerIdx = (_currentIndex + 1) % _maps.Length;
        if (_centerMapName != null)
            _centerMapName.text = _maps[centerIdx].MapName;

        float elapsed = 0f;

        while (elapsed < _totalDuration)
        {
            // 오른쪽 ← 가운데 ← 왼쪽 시프트
            _images[2].sprite = _images[1].sprite;
            _images[1].sprite = _images[0].sprite;
            _images[0].sprite = _maps[_currentIndex].Thumbnail;

            PlayShiftSfx();

            // 가운데 맵 이름 업데이트 (시프트 후 가운데 = 이전 왼쪽)
            if (_centerMapName != null)
                _centerMapName.text = _maps[_currentIndex].MapName;

            _currentIndex = (_currentIndex + 1) % _maps.Length;

            // 이미지 3개 펀치
            for (int i = 0; i < _punchTargets.Length; i++)
            {
                var t = _punchTargets[i];
                if (t == null) continue;
                t.DOKill();
                t.localScale = _baseScales[i];
                t.DOPunchScale(_punchScale, _punchDuration, _punchVibrato, _punchElasticity);
            }

            // 맵 이름 펀치
            if (_centerMapNamePunchTarget != null)
            {
                _centerMapNamePunchTarget.DOKill();
                _centerMapNamePunchTarget.localScale = _nameBaseScale;
                _centerMapNamePunchTarget.DOPunchScale(_punchScale, _punchDuration, _punchVibrato, _punchElasticity);
            }

            float progress = Mathf.Clamp01(elapsed / _totalDuration);
            float eased = progress * progress * progress;
            float interval = Mathf.Lerp(_startInterval, _endInterval, eased);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        _routine = null;
    }

    private void PlayShiftSfx()
    {
        if (_sfxSuppressed)
            return;

        if (_shiftSfx == null)
            return;

        AudioClip clip = _shiftSfx.GetRandomClip();
        if (clip == null)
            return;

        AudioManager.Instance?.Play(AudioType.Sfx, clip);
    }

    private void KillAllPunchTweens()
    {
        for (int i = 0; i < _punchTargets.Length; i++)
        {
            if (_punchTargets[i] == null) continue;
            _punchTargets[i].DOKill();
            if (_baseScales != null && i < _baseScales.Length)
                _punchTargets[i].localScale = _baseScales[i];
        }

        if (_centerMapNamePunchTarget != null)
        {
            _centerMapNamePunchTarget.DOKill();
            _centerMapNamePunchTarget.localScale = _nameBaseScale;
        }
    }
}
