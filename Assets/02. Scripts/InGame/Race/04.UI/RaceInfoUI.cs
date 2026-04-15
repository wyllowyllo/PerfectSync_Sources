using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class RaceInfoUI : MonoBehaviour
{
    private const float DefaultHoldSeconds = 0.85f;
    private const float DefaultFadeSeconds = 0.65f;
    private const float GameOverHoldMultiplier = 1.2f;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Transform _punchTarget;

    [Header("Number Colors (index 0 = '1', index 9 = '10')")]
    [SerializeField] private Color[] _numberColors = new Color[]
    {
        new Color(1f, 0.6f, 0.7f),
        new Color(1f, 0.7f, 0.4f),
        new Color(1f, 0.9f, 0.4f),
        new Color(0.6f, 1f, 0.6f),
        new Color(0.4f, 1f, 0.8f),
        new Color(0.4f, 0.9f, 1f),
        new Color(0.5f, 0.7f, 1f),
        new Color(0.7f, 0.5f, 1f),
        new Color(0.9f, 0.5f, 0.9f),
        new Color(1f, 0.5f, 0.5f),
    };

    [Header("Special Text Colors")]
    [SerializeField] private Color _startColor = new Color(0.6f, 1f, 0.4f);
    [SerializeField] private Color _winnerColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color _finishColor = new Color(0.4f, 0.85f, 1f);
    [SerializeField] private Color _gameOverColor = new Color(1f, 0.45f, 0.5f);

    [Header("Timing")]
    [SerializeField] private float _defaultHoldSeconds = DefaultHoldSeconds;
    [SerializeField] private float _defaultFadeSeconds = DefaultFadeSeconds;

    [Header("Punch Animation")]
    [SerializeField] private Vector3 _punchScale = new Vector3(0.3f, 0.3f, 0f);
    [SerializeField] private float _punchScaleDuration = 0.35f;
    [SerializeField] private Vector3 _punchRotation = new Vector3(0f, 0f, 15f);
    [SerializeField] private float _punchRotationDuration = 0.4f;
    [SerializeField] private int _vibrato = 8;
    [SerializeField] private float _elasticity = 0.6f;

    private Coroutine _routine;
    private Vector3 _baseScale;
    private WaitForSeconds _waitDefaultHold;
    private WaitForSeconds _waitGameOverHold;

    private void Awake()
    {
        _waitDefaultHold = new WaitForSeconds(_defaultHoldSeconds);
        _waitGameOverHold = new WaitForSeconds(_defaultHoldSeconds * GameOverHoldMultiplier);

        if (_punchTarget != null)
            _baseScale = _punchTarget.localScale;

        if (_text != null)
            _text.alpha = 0f;
    }

    public void ShowCountdown(int number)
    {
        if (number <= 0) return;
        int index = number - 1;
        Color color = index < _numberColors.Length ? _numberColors[index] : Color.white;
        ShowText(number.ToString(), color, _waitDefaultHold);
    }

    public void ShowFinishWindowSeconds(int secondsRemaining) =>
        ShowCountdown(secondsRemaining);

    public void ShowStart() =>
        ShowText("Start!", _startColor, _waitDefaultHold);

    public void ShowWinner() =>
        ShowText("Winner!", _winnerColor, _waitDefaultHold);

    public void ShowFinish() =>
        ShowText("Finish!", _finishColor, _waitDefaultHold);

    public void ShowGameOver() =>
        ShowText("GameOver!", _gameOverColor, _waitGameOverHold);

    private void ShowText(string message, Color color, WaitForSeconds holdWait)
    {
        if (_text == null || string.IsNullOrEmpty(message)) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        KillTweens();
        _routine = StartCoroutine(DisplayRoutine(message, color, holdWait));
    }

    private IEnumerator DisplayRoutine(string message, Color color, WaitForSeconds holdWait)
    {
        _text.text = message;
        _text.color = new Color(color.r, color.g, color.b, 1f);

        if (_punchTarget != null)
        {
            _punchTarget.localScale = _baseScale;
            _punchTarget.localRotation = Quaternion.identity;
            _punchTarget.DOPunchScale(_punchScale, _punchScaleDuration, _vibrato, _elasticity);
            _punchTarget.DOPunchRotation(_punchRotation, _punchRotationDuration, _vibrato, _elasticity);
        }

        if (holdWait != null)
            yield return holdWait;

        float t = 0f;
        while (t < _defaultFadeSeconds)
        {
            t += Time.deltaTime;
            _text.alpha = _defaultFadeSeconds > 0f
                ? Mathf.Lerp(1f, 0f, t / _defaultFadeSeconds)
                : 0f;
            yield return null;
        }

        _text.alpha = 0f;
        _routine = null;
    }

    private void KillTweens()
    {
        if (_punchTarget == null) return;
        _punchTarget.DOKill();
        _punchTarget.localScale = _baseScale;
        _punchTarget.localRotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        KillTweens();
    }
}
