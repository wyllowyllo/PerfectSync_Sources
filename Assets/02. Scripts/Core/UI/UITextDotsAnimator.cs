using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class UITextDotsAnimator : MonoBehaviour
{
    [Header("Base Text")]
    [SerializeField] private string _baseText = "Now Loading";

    [Header("Dots")]
    [SerializeField] private string _dotCharacter = ".";
    [SerializeField, Min(1)] private int _maxDots = 3;
    [SerializeField] private bool _includeZeroDots = true;

    [Header("Timing")]
    [SerializeField] private float _interval = 0.4f;
    [SerializeField] private bool _useUnscaledTime = true;

    private TMP_Text _text;
    private float _timer;
    private int _currentDots;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        _timer = 0f;
        _currentDots = _includeZeroDots ? 0 : 1;
        Apply();
    }

    private void Update()
    {
        _timer += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_timer < _interval) return;

        _timer = 0f;
        AdvanceDots();
        Apply();
    }

    private void OnDisable()
    {
        if (_text != null)
            _text.text = _baseText;
    }

    private void AdvanceDots()
    {
        int min = _includeZeroDots ? 0 : 1;
        _currentDots++;
        if (_currentDots > _maxDots)
            _currentDots = min;
    }

    private void Apply()
    {
        if (_text == null) return;

        char dot = string.IsNullOrEmpty(_dotCharacter) ? '.' : _dotCharacter[0];
        _text.text = _currentDots <= 0
            ? _baseText
            : _baseText + new string(dot, _currentDots);
    }
}
