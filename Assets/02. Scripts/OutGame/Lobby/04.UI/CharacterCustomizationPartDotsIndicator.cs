using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCustomizationPartDotsIndicator : MonoBehaviour
{
    private static readonly int PartCount =
        Enum.GetValues(typeof(CharacterCustomizationPart)).Length;

    [SerializeField] private CharacterCustomizationPartNavigator _navigator;
    [SerializeField] private RectTransform _dotsParent;
    [SerializeField] private Color _selectedColor = Color.white;
    [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private string _address;

    private Image[] _dotImages;
    private AddressableLoadResult<Sprite> _spriteLoad;
    private bool _hasSpriteLoad;

    private void OnEnable()
    {
        if (_navigator != null)
            _navigator.PartIndexChanged += OnPartIndexChanged;
    }

    private void Start()
    {
        StartCoroutine(CoLoadAndBuildDots());
    }

    private void OnDisable()
    {
        if (_navigator != null)
            _navigator.PartIndexChanged -= OnPartIndexChanged;
    }

    private void OnDestroy()
    {
        ReleaseSpriteLoad();
    }

    private IEnumerator CoLoadAndBuildDots()
    {
        if (_dotsParent == null)
            yield break;

        if (string.IsNullOrEmpty(_address))
        {
            Debug.LogError($"{nameof(CharacterCustomizationPartDotsIndicator)}: Address가 비어 있습니다.");
            yield break;
        }

        Task<AddressableLoadResult<Sprite>> task =
            AddressableAssetLoader.LoadAssetAsync<Sprite>(_address);
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError($"스프라이트 로드 실패 ({_address}): {task.Exception}");
            yield break;
        }

        ReleaseSpriteLoad();
        _spriteLoad = task.Result;
        _hasSpriteLoad = true;

        BuildDotImages(_spriteLoad.Asset);

        if (_navigator != null)
            ApplyHighlight(_navigator.CurrentPartIndex);
    }

    private void OnPartIndexChanged(int index)
    {
        ApplyHighlight(index);
    }

    private void BuildDotImages(Sprite sprite)
    {
        ClearDotChildren();

        _dotImages = new Image[PartCount];

        for (int i = 0; i < PartCount; i++)
        {
            var go = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_dotsParent, false);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = _normalColor;
            img.preserveAspect = true;

            _dotImages[i] = img;
        }
    }

    private void ClearDotChildren()
    {
        if (_dotsParent == null)
            return;

        for (int i = _dotsParent.childCount - 1; i >= 0; i--)
            Destroy(_dotsParent.GetChild(i).gameObject);

        _dotImages = null;
    }

    private void ApplyHighlight(int index)
    {
        if (_dotImages == null || _dotImages.Length == 0)
            return;

        for (int i = 0; i < _dotImages.Length; i++)
        {
            if (_dotImages[i] == null)
                continue;
            _dotImages[i].color = i == index ? _selectedColor : _normalColor;
        }
    }

    private void ReleaseSpriteLoad()
    {
        if (!_hasSpriteLoad)
            return;

        _spriteLoad.Release();
        _hasSpriteLoad = false;
    }
}
