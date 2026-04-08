using System;
using System.Collections;
using System.Collections.Generic;
using Gpm.Ui;
using UnityEngine;

public class CustomizationScrollManager : MonoBehaviour
{
    [SerializeField] private CharacterCustomizationPartNavigator _navigator;
    [SerializeField] private InfiniteScroll _infiniteScroll;
    // --- REMOVE:LocalPartPreview (start) ---
    [SerializeField] private CustomizationPartItemsActivator _partItemsActivator;

    public CustomizationPartItemsActivator PartItemsActivator => _partItemsActivator;
    // --- REMOVE:LocalPartPreview (end) ---

    private static readonly CharacterCustomizationPart[] Parts =
        (CharacterCustomizationPart[])Enum.GetValues(typeof(CharacterCustomizationPart));

    private AddressableLoadListResult<CustomizationItemDefinition> _currentLoad;
    private bool _hasLoad;
    private Coroutine _loadCoroutine;

    private void OnEnable()
    {
        _navigator.PartIndexChanged += OnPartChanged;
        LoadPart(_navigator.CurrentPart);
    }

    private void OnDisable()
    {
        _navigator.PartIndexChanged -= OnPartChanged;
        CancelAndRelease();
    }

    private void OnPartChanged(int partIndex)
    {
        LoadPart(Parts[partIndex]);
    }

    private void LoadPart(CharacterCustomizationPart part)
    {
        CancelAndRelease();
        _loadCoroutine = StartCoroutine(CoLoadAndPopulate(part));
    }

    private IEnumerator CoLoadAndPopulate(CharacterCustomizationPart part)
    {
        _infiniteScroll.Clear();

        var task = CustomizationItemRepository.LoadItemsByPartAsync(part);
        while (!task.IsCompleted)
            yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError($"커스터마이징 아이템 로드 실패 ({part}): {task.Exception}");
            yield break;
        }

        _currentLoad = task.Result;
        _hasLoad = true;

        var ordered = new List<CustomizationItemDefinition>(_currentLoad.Assets);
        ordered.Sort(CompareBySlotItemIndex);

        var datas = new InfiniteScrollData[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
            datas[i] = new CustomizationScrollData(ordered[i]);

        _infiniteScroll.InsertData(datas);
        _infiniteScroll.MoveToFirstData();
    }

    private void CancelAndRelease()
    {
        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }

        if (_hasLoad)
        {
            _currentLoad.Release();
            _hasLoad = false;
        }
    }

    private static int CompareBySlotItemIndex(CustomizationItemDefinition a, CustomizationItemDefinition b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int bySlot = a.SlotItemIndex.CompareTo(b.SlotItemIndex);
        if (bySlot != 0)
            return bySlot;

        return string.CompareOrdinal(a.name, b.name);
    }
}
