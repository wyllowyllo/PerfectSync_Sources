using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class UIScreenFadeTransition : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color _fadeColor = Color.black;

    [Header("Alpha")]
    [SerializeField] [Range(0f, 1f)] private float _startAlpha = 0f;
    [SerializeField] [Range(0f, 1f)] private float _endAlpha = 1f;

    [Header("Duration")]
    [SerializeField] private float _duration = 1f;

    private Graphic _graphic;
    private Coroutine _routine;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        if (_graphic == null)
            _graphic = GetComponent<Graphic>();

        Color c = _fadeColor;
        c.a = _startAlpha;
        _graphic.color = c;

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(FadeRoutine());
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator FadeRoutine()
    {
        if (_duration <= 0f)
        {
            Color immediate = _fadeColor;
            immediate.a = _endAlpha;
            _graphic.color = immediate;
            _routine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _duration);
            Color c = _fadeColor;
            c.a = Mathf.Lerp(_startAlpha, _endAlpha, t);
            _graphic.color = c;
            yield return null;
        }

        Color final = _fadeColor;
        final.a = _endAlpha;
        _graphic.color = final;
        _routine = null;
    }
}
