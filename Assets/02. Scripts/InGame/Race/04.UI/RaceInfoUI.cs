using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class RaceInfoUI : MonoBehaviour
{
    private const float DefaultHoldSeconds = 0.85f;
    private const float DefaultFadeSeconds = 0.65f;
    private const float GameOverHoldMultiplier = 1.2f;

    [Header("Display")]
    [SerializeField] private Image _image;
    [SerializeField] private Transform _punchTarget;

    [Header("Number Sprites (index 0 = '1', index 9 = '10')")]
    [SerializeField] private Sprite[] _numberSprites = new Sprite[10];

    [Header("Special Sprites")]
    [SerializeField] private Sprite _startSprite;
    [SerializeField] private Sprite _winnerSprite;
    [SerializeField] private Sprite _finishSprite;
    [SerializeField] private Sprite _gameOverSprite;

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

        if (_image != null)
            SetAlpha(0f);
    }

    public void ShowCountdown(int number)
    {
        int index = number - 1;
        if (index < 0 || index >= _numberSprites.Length) return;
        ShowSprite(_numberSprites[index], _waitDefaultHold);
    }

    public void ShowFinishWindowSeconds(int secondsRemaining) =>
        ShowCountdown(secondsRemaining);

    public void ShowStart() =>
        ShowSprite(_startSprite, _waitDefaultHold);

    public void ShowWinner() =>
        ShowSprite(_winnerSprite, _waitDefaultHold);

    public void ShowFinish() =>
        ShowSprite(_finishSprite, _waitDefaultHold);

    public void ShowGameOver() =>
        ShowSprite(_gameOverSprite, _waitGameOverHold);

    private void ShowSprite(Sprite sprite, WaitForSeconds holdWait)
    {
        if (_image == null || sprite == null) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        KillTweens();
        _routine = StartCoroutine(DisplayRoutine(sprite, holdWait));
    }

    private IEnumerator DisplayRoutine(Sprite sprite, WaitForSeconds holdWait)
    {
        // 스프라이트 교체 + 즉시 표시
        _image.sprite = sprite;
        SetAlpha(1f);

        // 펀치 애니메이션 (스케일 + 회전 동시)
        if (_punchTarget != null)
        {
            _punchTarget.localScale = _baseScale;
            _punchTarget.localRotation = Quaternion.identity;
            _punchTarget.DOPunchScale(_punchScale, _punchScaleDuration, _vibrato, _elasticity);
            _punchTarget.DOPunchRotation(_punchRotation, _punchRotationDuration, _vibrato, _elasticity);
        }

        // 홀드
        if (holdWait != null)
            yield return holdWait;

        // 페이드아웃
        float t = 0f;
        while (t < _defaultFadeSeconds)
        {
            t += Time.deltaTime;
            float alpha = _defaultFadeSeconds > 0f
                ? Mathf.Lerp(1f, 0f, t / _defaultFadeSeconds)
                : 0f;
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(0f);
        _routine = null;
    }

    private void SetAlpha(float a)
    {
        if (_image == null) return;
        Color c = _image.color;
        c.a = a;
        _image.color = c;
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
