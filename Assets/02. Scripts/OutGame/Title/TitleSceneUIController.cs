using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TitleSceneUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _logoImage;
    [SerializeField] private RectTransform _characterImage1;
    [SerializeField] private RectTransform _characterImage2;
    [SerializeField] private GameObject _tapPromptRoot;
    [SerializeField] private GameObject _loginPopupRoot;
    [SerializeField] private Button _skipButton;

    [Header("Logo — scale pop")]
    [SerializeField] private float _logoDuration = 0.5f;
    [SerializeField] private float _logoTargetScale = 1f;
    [SerializeField] private float _logoOvershoot = 1.12f;

    [Header("Character 1 — scale pop")]
    [SerializeField] private float _character1Duration = 0.5f;
    [SerializeField] private float _character1TargetScale = 1f;
    [SerializeField] private float _character1Overshoot = 1.12f;

    [Header("Character 2 — scale pop")]
    [SerializeField] private float _character2Duration = 0.5f;
    [SerializeField] private float _character2TargetScale = 1f;
    [SerializeField] private float _character2Overshoot = 1.12f;

    [Header("Login popup — scale pop")]
    [SerializeField] private float _loginPopupDuration = 0.25f;
    [SerializeField] private float _loginPopupTargetScale = 1f;
    [SerializeField] private float _loginPopupOvershoot = 1.12f;

    [Header("Flow")]
    [SerializeField] private float _delayBeforeLogo = 0.25f;
    [SerializeField] private float _delayBeforeTapPrompt;

    private Sequence _introSequence;
    private Tween _loginPopupTween;

    private void Awake()
    {
        ApplyInitialLayout();
    }

    private void Start()
    {
        PlayTitleIntro();
    }

    private void Update()
    {
        if(Input.anyKeyDown && _skipButton.interactable == true)
        {
            ShowLoginPopup();
        }
    }

    private void OnDisable()
    {
        KillIntroSequence();
        KillLoginPopupTween();
    }

    private void ApplyInitialLayout()
    {
        if (_logoImage != null)
            _logoImage.localScale = Vector3.zero;
        if (_characterImage1 != null)
            _characterImage1.localScale = Vector3.zero;
        if (_characterImage2 != null)
            _characterImage2.localScale = Vector3.zero;

        if (_tapPromptRoot != null)
            _tapPromptRoot.SetActive(false);

        if (_loginPopupRoot != null)
        {
            _loginPopupRoot.transform.localScale = Vector3.zero;
            _loginPopupRoot.SetActive(false);
        }
    }

    private void PlayTitleIntro()
    {
        KillIntroSequence();

        _introSequence = DOTween.Sequence();
        _introSequence.SetUpdate(true);

        if (_delayBeforeLogo > 0f)
            _introSequence.AppendInterval(_delayBeforeLogo);

        if (_logoImage != null)
        {
            _introSequence.Append(
                UIScalePopUtility.PlayUniform(
                    _logoImage,
                    _logoDuration,
                    _logoTargetScale,
                    _logoOvershoot));
        }

        if (_characterImage1 != null)
        {
            _introSequence.Append(
                UIScalePopUtility.PlayUniform(
                    _characterImage1,
                    _character1Duration,
                    _character1TargetScale,
                    _character1Overshoot));
        }

        if (_characterImage2 != null)
        {
            _introSequence.Append(
                UIScalePopUtility.PlayUniform(
                    _characterImage2,
                    _character2Duration,
                    _character2TargetScale,
                    _character2Overshoot));
        }

        if (_delayBeforeTapPrompt > 0f)
            _introSequence.AppendInterval(_delayBeforeTapPrompt);

        _introSequence.OnComplete(ShowTapPrompt);
    }

    private void ShowTapPrompt()
    {
        if (_tapPromptRoot != null)
            _tapPromptRoot.SetActive(true);
    }

    private void HideTapPrompt()
    {
        if (_tapPromptRoot != null)
            _tapPromptRoot.SetActive(false);
    }

    public void ShowLoginPopup()
    {
        KillIntroSequence(snapToTargetScale: true);
        HideTapPrompt();

        if (_skipButton != null)
            _skipButton.interactable = false;

        if (_loginPopupRoot == null)
            return;

        KillLoginPopupTween();

        _loginPopupRoot.SetActive(true);
        _loginPopupTween = UIScalePopUtility.PlayUniform(
            _loginPopupRoot.transform,
            _loginPopupDuration,
            _loginPopupTargetScale,
            _loginPopupOvershoot);
    }

    private void KillIntroSequence(bool snapToTargetScale = false)
    {
        if (_introSequence != null && _introSequence.IsActive())
            _introSequence.Kill();
        _introSequence = null;

        if (snapToTargetScale)
            SnapIntroVisualsToTargetScale();
    }

    private void SnapIntroVisualsToTargetScale()
    {
        if (_logoImage != null)
        {
            _logoImage.DOKill();
            _logoImage.localScale = Vector3.one * _logoTargetScale;
        }

        if (_characterImage1 != null)
        {
            _characterImage1.DOKill();
            _characterImage1.localScale = Vector3.one * _character1TargetScale;
        }

        if (_characterImage2 != null)
        {
            _characterImage2.DOKill();
            _characterImage2.localScale = Vector3.one * _character2TargetScale;
        }
    }

    private void KillLoginPopupTween()
    {
        if (_loginPopupTween != null && _loginPopupTween.IsActive())
            _loginPopupTween.Kill();
        _loginPopupTween = null;
        if (_loginPopupRoot != null)
            _loginPopupRoot.transform.DOKill();
    }
}
