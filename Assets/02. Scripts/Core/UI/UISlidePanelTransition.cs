using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

public enum UISlideExitDirection
{
    ToRight,
    ToLeft,
    ToUp,
    ToDown,
}

public class UISlidePanelTransition : MonoBehaviour
{
    [SerializeField] private UISlideExitDirection _exitDirection;
    [SerializeField] [Min(0.01f)] private float _duration = 0.5f;
    [SerializeField] [Range(0.02f, 0.5f)] private float _anticipationScreenRatio = 0.08f;
    [SerializeField] [Range(0.05f, 0.45f)] private float _anticipationPhaseDurationRatio = 0.25f;
    [SerializeField] [Min(0.5f)] private float _exitDistanceMultiplier = 1f;

    [SerializeField] private bool _useManualRestPosition;
    [SerializeField] private Vector2 _manualRestAnchoredPosition;

    [SerializeField] private UnityEvent _onExitCompleted = new UnityEvent();
    [SerializeField] private UnityEvent _onEnterCompleted = new UnityEvent();

    public event Action ExitCompleted;
    public event Action EnterCompleted;

    private RectTransform _rect;
    private Vector2 _restAnchoredPosition;
    private Sequence _sequence;

    public Vector2 RestAnchoredPosition => _restAnchoredPosition;

    private void Awake()
    {
        _rect = transform as RectTransform;
        if (_rect == null)
            return;

        _restAnchoredPosition = _useManualRestPosition
            ? _manualRestAnchoredPosition
            : _rect.anchoredPosition;
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
    }

    public void PlayExit()
    {
        if (_rect == null)
            return;

        _sequence?.Kill();

        Vector2 canvasSize = GetReferenceCanvasSize();
        float exitW = canvasSize.x * _exitDistanceMultiplier;
        float exitH = canvasSize.y * _exitDistanceMultiplier;
        float antW = canvasSize.x * _anticipationScreenRatio;
        float antH = canvasSize.y * _anticipationScreenRatio;

        Vector2 exitDelta = GetExitDelta(exitW, exitH);
        Vector2 antiDelta = GetAnticipationDelta(antW, antH);

        Vector2 antiEnd = _restAnchoredPosition + antiDelta;
        Vector2 exitEnd = _restAnchoredPosition + exitDelta;

        float tAnt = Mathf.Min(_duration * _anticipationPhaseDurationRatio, _duration - 0.01f);
        tAnt = Mathf.Max(tAnt, 0.01f);
        float tExit = Mathf.Max(_duration - tAnt, 0.01f);

        _sequence = DOTween.Sequence();
        _sequence.Append(_rect.DOAnchorPos(antiEnd, tAnt).SetEase(Ease.OutQuad));
        _sequence.Append(_rect.DOAnchorPos(exitEnd, tExit).SetEase(Ease.InQuad));
        _sequence.OnComplete(() =>
        {
            ExitCompleted?.Invoke();
            _onExitCompleted?.Invoke();
        });
    }

    public void PlayEnter()
    {
        if (_rect == null)
            return;

        _sequence?.Kill();

        Vector2 start = _rect.anchoredPosition;
        Vector2 canvasSize = GetReferenceCanvasSize();
        float antW = canvasSize.x * _anticipationScreenRatio;
        float antH = canvasSize.y * _anticipationScreenRatio;

        Vector2 windUpDelta = GetExitDelta(antW, antH);
        Vector2 phase1End = start + windUpDelta;

        float tAnt = Mathf.Min(_duration * _anticipationPhaseDurationRatio, _duration - 0.01f);
        tAnt = Mathf.Max(tAnt, 0.01f);
        float tMain = Mathf.Max(_duration - tAnt, 0.01f);

        _sequence = DOTween.Sequence();
        _sequence.Append(_rect.DOAnchorPos(phase1End, tAnt).SetEase(Ease.OutQuad));
        _sequence.Append(_rect.DOAnchorPos(_restAnchoredPosition, tMain).SetEase(Ease.OutCubic));
        _sequence.OnComplete(() =>
        {
            EnterCompleted?.Invoke();
            _onEnterCompleted?.Invoke();
        });
    }

    public void CaptureRestPositionFromCurrent()
    {
        if (_rect != null)
            _restAnchoredPosition = _rect.anchoredPosition;
    }

    public void SnapToRestImmediate()
    {
        if (_rect == null)
            return;

        _sequence?.Kill();
        _sequence = null;
        _rect.anchoredPosition = _restAnchoredPosition;
    }

    public void SnapToExitEndImmediate()
    {
        if (_rect == null)
            return;

        _sequence?.Kill();
        _sequence = null;

        Vector2 canvasSize = GetReferenceCanvasSize();
        float exitW = canvasSize.x * _exitDistanceMultiplier;
        float exitH = canvasSize.y * _exitDistanceMultiplier;
        Vector2 exitDelta = GetExitDelta(exitW, exitH);
        _rect.anchoredPosition = _restAnchoredPosition + exitDelta;
    }

    private Vector2 GetExitDelta(float width, float height)
    {
        switch (_exitDirection)
        {
            case UISlideExitDirection.ToRight:
                return new Vector2(width, 0f);
            case UISlideExitDirection.ToLeft:
                return new Vector2(-width, 0f);
            case UISlideExitDirection.ToUp:
                return new Vector2(0f, height);
            case UISlideExitDirection.ToDown:
                return new Vector2(0f, -height);
            default:
                return Vector2.zero;
        }
    }

    private Vector2 GetAnticipationDelta(float antW, float antH)
    {
        switch (_exitDirection)
        {
            case UISlideExitDirection.ToRight:
                return new Vector2(-antW, 0f);
            case UISlideExitDirection.ToLeft:
                return new Vector2(antW, 0f);
            case UISlideExitDirection.ToUp:
                return new Vector2(0f, -antH);
            case UISlideExitDirection.ToDown:
                return new Vector2(0f, antH);
            default:
                return Vector2.zero;
        }
    }

    private Vector2 GetReferenceCanvasSize()
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
