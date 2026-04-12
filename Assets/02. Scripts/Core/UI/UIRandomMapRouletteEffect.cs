using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIRandomMapRouletteEffect : MonoBehaviour
{
    [Header("Target Images (Left / Center / Right)")]
    [SerializeField] private Image[] _images = new Image[3];

    [Header("Punch Target Parents (Left / Center / Right)")]
    [SerializeField] private Transform[] _punchTargets = new Transform[3];

    [Header("Sprite Pool")]
    [SerializeField] private Sprite[] _spritePool;

    [Header("Timing")]
    [SerializeField] private float _totalDuration = 5f;
    [SerializeField] private float _startInterval = 0.15f;
    [SerializeField] private float _endInterval = 0.5f;

    [Header("Punch Scale")]
    [SerializeField] private Vector3 _punchScale = new Vector3(0.25f, 0.25f, 0f);
    [SerializeField] private float _punchDuration = 0.2f;
    [SerializeField] private int _punchVibrato = 6;
    [SerializeField] private float _punchElasticity = 0.8f;

    private Coroutine _routine;
    private Vector3[] _baseScales;
    private int _currentIndex;

    private void Awake()
    {
        _baseScales = new Vector3[_punchTargets.Length];
        for (int i = 0; i < _punchTargets.Length; i++)
            if (_punchTargets[i] != null)
                _baseScales[i] = _punchTargets[i].localScale;
    }

    private void OnEnable()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RouletteRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        for (int i = 0; i < _punchTargets.Length; i++)
        {
            if (_punchTargets[i] == null) continue;
            _punchTargets[i].DOKill();
            _punchTargets[i].localScale = _baseScales[i];
        }
    }

    private IEnumerator RouletteRoutine()
    {
        if (_spritePool == null || _spritePool.Length == 0) yield break;
        if (_images.Length < 3) yield break;

        _currentIndex = Random.Range(0, _spritePool.Length);

        float elapsed = 0f;

        while (elapsed < _totalDuration)
        {
            _images[2].sprite = _images[1].sprite;
            _images[1].sprite = _images[0].sprite;
            _images[0].sprite = _spritePool[_currentIndex];

            _currentIndex = (_currentIndex + 1) % _spritePool.Length;

            for (int i = 0; i < _punchTargets.Length; i++)
            {
                var t = _punchTargets[i];
                if (t == null) continue;
                t.DOKill();
                t.localScale = _baseScales[i];
                t.DOPunchScale(_punchScale, _punchDuration, _punchVibrato, _punchElasticity);
            }

            float progress = Mathf.Clamp01(elapsed / _totalDuration);
            float eased = progress * progress * progress;
            float interval = Mathf.Lerp(_startInterval, _endInterval, eased);

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        _routine = null;
    }
}
