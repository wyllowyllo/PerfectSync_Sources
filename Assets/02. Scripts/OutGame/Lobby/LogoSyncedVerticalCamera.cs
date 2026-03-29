using DG.Tweening;
using UnityEngine;

public class LogoSyncedVerticalCamera : MonoBehaviour
{
    [Header("Timing (match LogoUI)")]
    [SerializeField] private float _stretchPhaseDuration = 0.4f;
    [SerializeField] private float _pauseBetweenCycles = 0f;

    [Header("Motion")]
    [Tooltip("한 비트당 로컬(또는 월드) Y축으로 벗어나는 거리.")]
    [SerializeField] private float _verticalAmplitude = 0.06f;
    [SerializeField] private bool _useLocalSpace = true;

    [Header("Easing (match LogoUI)")]
    [SerializeField] private float _stretchOvershoot = 1.15f;

    private Sequence _loop;
    private Vector3 _baseLocalPosition;
    private Vector3 _baseWorldPosition;

    private void OnEnable()
    {
        CacheBase();
        ApplyBase();
        KillLoop();
        BuildLoop();
    }

    private void OnDisable()
    {
        KillLoop();
        RestoreBase();
    }

    private void OnDestroy()
    {
        KillLoop();
    }

    private void CacheBase()
    {
        _baseLocalPosition = transform.localPosition;
        _baseWorldPosition = transform.position;
    }

    private void ApplyBase()
    {
        if (_useLocalSpace)
            transform.localPosition = _baseLocalPosition;
        else
            transform.position = _baseWorldPosition;
    }

    private void RestoreBase()
    {
        ApplyBase();
    }

    private void BuildLoop()
    {
        _loop = DOTween.Sequence();

        if (_useLocalSpace)
        {
            float y0 = _baseLocalPosition.y;
            _loop.Append(transform.DOLocalMoveY(y0 + _verticalAmplitude, _stretchPhaseDuration).SetEase(Ease.OutBack, _stretchOvershoot));
            _loop.Append(transform.DOLocalMoveY(y0 - _verticalAmplitude, _stretchPhaseDuration).SetEase(Ease.OutBack, _stretchOvershoot));
        }
        else
        {
            float y0 = _baseWorldPosition.y;
            _loop.Append(transform.DOMoveY(y0 + _verticalAmplitude, _stretchPhaseDuration).SetEase(Ease.OutBack, _stretchOvershoot));
            _loop.Append(transform.DOMoveY(y0 - _verticalAmplitude, _stretchPhaseDuration).SetEase(Ease.OutBack, _stretchOvershoot));
        }

        if (_pauseBetweenCycles > 0f)
            _loop.AppendInterval(_pauseBetweenCycles);

        _loop.SetLoops(-1, LoopType.Restart);
    }

    private void KillLoop()
    {
        if (_loop != null && _loop.IsActive())
            _loop.Kill();
        _loop = null;
        transform.DOKill();
    }
}
