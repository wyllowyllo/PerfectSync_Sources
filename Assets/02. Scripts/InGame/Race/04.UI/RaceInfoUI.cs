using System.Collections;
using TMPro;
using UnityEngine;

public class RaceInfoUI : MonoBehaviour
{
    private const float DefaultHoldSeconds = 0.85f;
    private const float DefaultFadeSeconds = 0.65f;
    private const float GameOverHoldMultiplier = 1.2f;
    private const float FullRgbAlpha = 1f;
    private const float TransparentAlpha = 0f;

    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _defaultHoldSeconds = DefaultHoldSeconds;
    [SerializeField] private float _defaultFadeSeconds = DefaultFadeSeconds;

    private Coroutine _routine;
    private Color _rgb = Color.white;
    private WaitForSeconds _waitDefaultHold;
    private WaitForSeconds _waitGameOverHold;

    private void Awake()
    {
        _waitDefaultHold = new WaitForSeconds(_defaultHoldSeconds);
        _waitGameOverHold = new WaitForSeconds(_defaultHoldSeconds * GameOverHoldMultiplier);

        if (_text == null) return;

        Color c = _text.color;
        _rgb = new Color(c.r, c.g, c.b, FullRgbAlpha);
        SetAlpha(TransparentAlpha);
    }

    public void ShowCountdown(int number) =>
        StartFadeRoutine(number.ToString(), _waitDefaultHold, _defaultFadeSeconds);

    public void ShowFinishWindowSeconds(int secondsRemaining) =>
        ShowCountdown(secondsRemaining);

    public void ShowStart() =>
        StartFadeRoutine("Start", _waitDefaultHold, _defaultFadeSeconds);

    public void ShowWinner() =>
        StartFadeRoutine("Winner", _waitDefaultHold, _defaultFadeSeconds);

    public void ShowFinish() =>
        StartFadeRoutine("완주", _waitDefaultHold, _defaultFadeSeconds);

    public void ShowGameOver() =>
        StartFadeRoutine("GameOver", _waitGameOverHold, _defaultFadeSeconds);

    public void ShowMessage(string message, float holdSeconds, float fadeSeconds)
    {
        StartFadeRoutine(message, ResolveHoldWait(holdSeconds), fadeSeconds);
    }

    private void StartFadeRoutine(string message, WaitForSeconds holdWait, float fadeSeconds)
    {
        if (_text == null) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _routine = StartCoroutine(FadeRoutine(message, holdWait, fadeSeconds));
    }

    private WaitForSeconds ResolveHoldWait(float holdSeconds)
    {
        if (holdSeconds <= 0f)
            return null;
        if (Mathf.Approximately(holdSeconds, _defaultHoldSeconds))
            return _waitDefaultHold;
        if (Mathf.Approximately(holdSeconds, _defaultHoldSeconds * GameOverHoldMultiplier))
            return _waitGameOverHold;
        return new WaitForSeconds(holdSeconds);
    }

    private void SetAlpha(float a)
    {
        var c = _rgb;
        c.a = a;
        _text.color = c;
    }

    private IEnumerator FadeRoutine(string message, WaitForSeconds holdWait, float fadeSeconds)
    {
        _text.text = message;
        SetAlpha(FullRgbAlpha);

        if (holdWait != null)
            yield return holdWait;

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            SetAlpha(fadeSeconds > 0f ? Mathf.Lerp(FullRgbAlpha, TransparentAlpha, t / fadeSeconds) : TransparentAlpha);
            yield return null;
        }

        SetAlpha(TransparentAlpha);
        _routine = null;
    }
}
