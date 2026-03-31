using DG.Tweening;
using UnityEngine;

public class LogoUI : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float _stretchPhaseDuration = 0.4f;
    [SerializeField] private float _pauseBetweenCycles = 0f;

    [Header("Stretch amounts (relative to uniform 1,1,1)")]
    [SerializeField] private Vector3 _wideShort = new Vector3(1.14f, 0.86f, 1f);
    [SerializeField] private Vector3 _narrowTall = new Vector3(0.86f, 1.14f, 1f);

    [Header("Easing")]
    [SerializeField] private float _stretchOvershoot = 1.15f;

    private RectTransform _rect;
    private Sequence _loop;

    private void Awake()
    {
        _rect = transform as RectTransform;
    }

    private void OnEnable()
    {
        if (_rect == null) return;

        _rect.localScale = Vector3.one;
        KillLoop();
        _loop = DOTween.Sequence();
        _loop.Append(_rect.DOScale(_wideShort, _stretchPhaseDuration).SetEase(Ease.OutBack, _stretchOvershoot));
        _loop.Append(_rect.DOScale(_narrowTall, _stretchPhaseDuration).SetEase(Ease.OutBack, _stretchOvershoot));
        if (_pauseBetweenCycles > 0f)
            _loop.AppendInterval(_pauseBetweenCycles);
        _loop.SetLoops(-1, LoopType.Restart);
    }

    private void OnDisable()
    {
        KillLoop();
    }

    private void OnDestroy()
    {
        KillLoop();
    }

    private void KillLoop()
    {
        if (_loop != null && _loop.IsActive())
            _loop.Kill();
        _loop = null;
        if (_rect != null)
            _rect.DOKill();
    }
}
