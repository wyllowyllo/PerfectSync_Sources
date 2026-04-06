using System.Collections;
using System.Threading.Tasks;
using Gpm.Ui;
using UnityEngine;
using UnityEngine.UI;

public class CustomizationItem : InfiniteScrollItem
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _iconImage;
    private CustomizationItemDefinition _definition;

    public CustomizationItemDefinition Definition
    {
        get => _definition;
        private set
        {
            if(value != null)
            {
                _definition = value;
                SubscribeAndLoad();
            }
        }
    }

    private AddressableLoadResult<Sprite> _spriteLoad;
    private bool _hasSpriteLoad;
    private Coroutine _loadCoroutine;

    private void OnEnable()
    {
        TryStartIconLoadCoroutine();
    }

    private void OnDisable()
    {
        UnsubscribeAndRelease();
        _definition = null;
    }

    private void SubscribeAndLoad()
    {
        if (_definition == null)
            return;

        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClicked);
            _button.onClick.AddListener(OnClicked);
        }

        TryStartIconLoadCoroutine();
    }

    private void TryStartIconLoadCoroutine()
    {
        if (_definition == null || _iconImage == null || string.IsNullOrEmpty(_definition.IconAddress))
            return;

        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }

        if (!gameObject.activeInHierarchy)
            return;

        _loadCoroutine = StartCoroutine(CoLoadIcon(_definition.IconAddress));
    }

    private void UnsubscribeAndRelease()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);

        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }

        ReleaseSpriteLoad();

        if (_iconImage != null)
            _iconImage.sprite = null;
    }

    private IEnumerator CoLoadIcon(string address)
    {
        ReleaseSpriteLoad();

        Task<AddressableLoadResult<Sprite>> task = AddressableAssetLoader.LoadAssetAsync<Sprite>(address);
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError($"{nameof(CustomizationItem)}: 아이콘 로드 실패 ({address}): {task.Exception}", this);
            yield break;
        }

        _spriteLoad = task.Result;
        _hasSpriteLoad = true;

        if (_iconImage != null)
            _iconImage.sprite = _spriteLoad.Asset;
    }

    private void ReleaseSpriteLoad()
    {
        if (!_hasSpriteLoad)
            return;

        _spriteLoad.Release();
        _hasSpriteLoad = false;
    }

    private void OnClicked()
    {
        if (_definition == null)
            return;

        // --- REMOVE:LocalPartPreview (start) ---
        var scrollManager = GetComponentInParent<CustomizationScrollManager>();
        if (scrollManager != null && scrollManager.PartItemsActivator != null)
            scrollManager.PartItemsActivator.SetPartItemIndex(_definition.Part, _definition.SlotItemIndex);
        // --- REMOVE:LocalPartPreview (end) ---

        CustomizationPhotonKeys.SetLocalPlayerSlotIndex(_definition.Part, _definition.SlotItemIndex);

        // Firebase에 파츠 번호 저장
        _ = FirebaseCustomizationRepository.SavePart(_definition.Part, _definition.SlotItemIndex);
    }

    public void SetDefinition(CustomizationItemDefinition definition)
    {
        Definition = definition;
    }

    public override void UpdateData(InfiniteScrollData scrollData)
    {
        base.UpdateData(scrollData);

        if (scrollData is CustomizationScrollData customizationScrollData)
            Definition = customizationScrollData.Definition;
        else
            Definition = null;
    }
}
