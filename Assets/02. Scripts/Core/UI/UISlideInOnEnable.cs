using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UISlideInOnEnable : MonoBehaviour
{
    [SerializeField] private UISlideExitDirection _enterFrom = UISlideExitDirection.ToRight;
    [SerializeField] private float _duration = 0.4f;
    [SerializeField] private Ease _ease = Ease.OutCubic;

    private RectTransform _rect;
    private Vector2 _restOffsetMin;
    private Vector2 _restOffsetMax;
    private Tween _tween;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _restOffsetMin = _rect.offsetMin;
        _restOffsetMax = _rect.offsetMax;
    }

    private void OnEnable()
    {
        if (_rect == null) _rect = (RectTransform)transform;

        _tween?.Kill();

        Vector2 offset = GetSlideOffset();
        _rect.offsetMin = _restOffsetMin + offset;
        _rect.offsetMax = _restOffsetMax + offset;

        _tween = DOVirtual.Float(1f, 0f, _duration, t =>
        {
            Vector2 current = offset * t;
            _rect.offsetMin = _restOffsetMin + current;
            _rect.offsetMax = _restOffsetMax + current;
        }).SetEase(_ease);
    }

    private void OnDisable()
    {
        _tween?.Kill();
        _tween = null;

        if (_rect != null)
        {
            _rect.offsetMin = _restOffsetMin;
            _rect.offsetMax = _restOffsetMax;
        }
    }

    private Vector2 GetSlideOffset()
    {
        Vector2 canvasSize = GetCanvasSize();

        switch (_enterFrom)
        {
            case UISlideExitDirection.ToRight:  return new Vector2(canvasSize.x, 0f);
            case UISlideExitDirection.ToLeft:   return new Vector2(-canvasSize.x, 0f);
            case UISlideExitDirection.ToUp:     return new Vector2(0f, canvasSize.y);
            case UISlideExitDirection.ToDown:   return new Vector2(0f, -canvasSize.y);
            default:                            return Vector2.zero;
        }
    }

    private Vector2 GetCanvasSize()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform root = canvas.transform as RectTransform;
            if (root != null)
                return root.rect.size;
        }
        return new Vector2(Screen.width, Screen.height);
    }
}
