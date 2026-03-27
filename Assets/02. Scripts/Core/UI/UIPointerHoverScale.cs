using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UIPointerHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private enum EaseKind
    {
        Linear,
        OutQuad
    }

    [SerializeField] private float _hoverScaleMultiplier = 1.08f;
    [SerializeField] private float _duration = 0.12f;
    [SerializeField] private EaseKind _ease = EaseKind.OutQuad;

    private Transform _transform;
    private Vector3 _restLocalScale;
    private Vector3 _hoverLocalScale;

    private Coroutine _driver;
    private bool _scaling;
    private float _elapsed;
    private Vector3 _fromScale;
    private Vector3 _toScale;

    private void Awake()
    {
        _transform = transform;
    }

    private void OnEnable()
    {
        _scaling = false;
        _restLocalScale = _transform.localScale;
        RefreshHoverScale();
        _driver = StartCoroutine(ScaleDriver());
    }

    private void OnDisable()
    {
        if (_driver != null)
        {
            StopCoroutine(_driver);
            _driver = null;
        }

        _scaling = false;
        _transform.localScale = _restLocalScale;
    }

    private void RefreshHoverScale()
    {
        _hoverLocalScale = _restLocalScale * _hoverScaleMultiplier;
    }

    private static float EvaluateEase(EaseKind kind, float t)
    {
        switch (kind)
        {
            case EaseKind.Linear:
                return t;
            default:
            {
                float inv = 1f - t;
                return 1f - inv * inv;
            }
        }
    }

    private IEnumerator ScaleDriver()
    {
        while (true)
        {
            yield return null;

            if (!_scaling)
                continue;

            _elapsed += Time.deltaTime;
            float t = _duration > 1e-6f ? Mathf.Clamp01(_elapsed / _duration) : 1f;
            float k = EvaluateEase(_ease, t);
            _transform.localScale = Vector3.LerpUnclamped(_fromScale, _toScale, k);

            if (t >= 1f)
            {
                _transform.localScale = _toScale;
                _scaling = false;
            }
        }
    }

    private void BeginScaleToward(Vector3 end)
    {
        _fromScale = _transform.localScale;
        _toScale = end;
        _elapsed = 0f;

        if (_duration <= 1e-6f)
        {
            _transform.localScale = end;
            _scaling = false;
            return;
        }

        _scaling = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        BeginScaleToward(_hoverLocalScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        BeginScaleToward(_restLocalScale);
    }
}
