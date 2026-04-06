using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class UIPromptAlphaBlink : MonoBehaviour
{
    [Header("Alpha range")]
    [SerializeField] [Range(0f, 1f)] private float _minAlpha = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float _maxAlpha = 1f;

    [Header("Timing")]
    [SerializeField] private float _fadeInDuration = 0.45f;
    [SerializeField] private float _fadeOutDuration = 0.45f;
    [SerializeField] private float _pauseAtPeak;

    private Graphic _graphic;
    private Sequence _sequence;
    private Color _baseColor;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _baseColor = _graphic.color;
    }

    private void OnEnable()
    {
        if (_graphic == null)
            _graphic = GetComponent<Graphic>();

        KillSequence();
        float min = Mathf.Min(_minAlpha, _maxAlpha);
        float max = Mathf.Max(_minAlpha, _maxAlpha);

        Color c = _graphic.color;
        c.a = min;
        _graphic.color = c;

        _sequence = DOTween.Sequence();
        _sequence.SetUpdate(true);
        _sequence.Append(_graphic.DOFade(max, _fadeInDuration).SetEase(Ease.InOutSine));
        if (_pauseAtPeak > 0f)
            _sequence.AppendInterval(_pauseAtPeak);
        _sequence.Append(_graphic.DOFade(min, _fadeOutDuration).SetEase(Ease.InOutSine));
        _sequence.SetLoops(-1, LoopType.Restart);
    }

    private void OnDisable()
    {
        KillSequence();
        if (_graphic != null)
            _graphic.color = _baseColor;
    }

    private void OnDestroy()
    {
        KillSequence();
    }

    private void KillSequence()
    {
        if (_sequence != null && _sequence.IsActive())
            _sequence.Kill();
        _sequence = null;

        if (_graphic != null)
            _graphic.DOKill();
    }
}
