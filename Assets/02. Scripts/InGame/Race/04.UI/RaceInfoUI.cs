using System.Collections;
using TMPro;
using UnityEngine;

public class RaceInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _defaultHoldSeconds = 0.85f;
    [SerializeField] private float _defaultFadeSeconds = 0.65f;

    private Coroutine _routine;
    private Color _rgb = Color.white;

    private void Awake()
    {
        if (_text == null) return;

        Color c = _text.color;
        _rgb = new Color(c.r, c.g, c.b, 1f);
        SetAlpha(0f);
    }

    public void ShowCountdown(int number) =>
        ShowMessage(number.ToString(), _defaultHoldSeconds, _defaultFadeSeconds);

    public void ShowFinishWindowSeconds(int secondsRemaining) =>
        ShowCountdown(secondsRemaining);

    public void ShowStart() =>
        ShowMessage("Start", _defaultHoldSeconds, _defaultFadeSeconds);

    public void ShowWinner() =>
        ShowMessage("Winner", _defaultHoldSeconds, _defaultFadeSeconds);

    public void ShowFinish() =>
        ShowMessage("완주", _defaultHoldSeconds, _defaultFadeSeconds);

    public void ShowGameOver() =>
        ShowMessage("GameOver", _defaultHoldSeconds * 1.2f, _defaultFadeSeconds);

    public void ShowMessage(string message, float holdSeconds, float fadeSeconds)
    {
        if (_text == null) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _routine = StartCoroutine(FadeRoutine(message, holdSeconds, fadeSeconds));
    }

    private void SetAlpha(float a)
    {
        var c = _rgb;
        c.a = a;
        _text.color = c;
    }

    private IEnumerator FadeRoutine(string message, float holdSeconds, float fadeSeconds)
    {
        _text.text = message;
        SetAlpha(1f);

        if (holdSeconds > 0f)
            yield return new WaitForSeconds(holdSeconds);

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            SetAlpha(fadeSeconds > 0f ? Mathf.Lerp(1f, 0f, t / fadeSeconds) : 0f);
            yield return null;
        }

        SetAlpha(0f);
        _routine = null;
    }
}
