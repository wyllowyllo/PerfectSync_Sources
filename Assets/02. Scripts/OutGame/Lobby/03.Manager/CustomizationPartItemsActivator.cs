using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomizationPartItemsActivator : MonoBehaviour
{
    private readonly Dictionary<CharacterCustomizationPart, CustomizationPartSlot> _slots =
        new Dictionary<CharacterCustomizationPart, CustomizationPartSlot>();

    private readonly Dictionary<CharacterCustomizationPart, int> _lastItemIndexByPart =
        new Dictionary<CharacterCustomizationPart, int>();

    public event Action<CharacterCustomizationPart, int> PartItemSelectionChanged;

    private void Awake()
    {
        RebuildSlotMap();
    }

    /// <summary>
    /// 파티 캐릭터 등 비활성 상태로 씬에 있으면 <see cref="Awake"/>가 호출되지 않아 슬롯 맵이 비어 있을 수 있습니다.
    /// 첫 사용 시점에 슬롯을 채웁니다.
    /// </summary>
    private void EnsureSlotsInitialized()
    {
        if (_slots.Count > 0)
            return;
        RebuildSlotMap();
    }

    public void RefreshActivators()
    {
        RebuildSlotMap();
    }

    public void SetPartItemIndex(CharacterCustomizationPart part, int itemIndex)
    {
        EnsureSlotsInitialized();
        if (!_slots.TryGetValue(part, out CustomizationPartSlot slot))
        {
            Debug.LogWarning($"{nameof(CustomizationPartItemsActivator)}: 부위 {part}에 대한 슬롯이 없습니다.", this);
            return;
        }

        slot.SetActiveItemIndex(itemIndex);
        _lastItemIndexByPart[part] = itemIndex;
        PartItemSelectionChanged?.Invoke(part, itemIndex);
    }

    public void ApplyDefaultCustomization()
    {
        EnsureSlotsInitialized();
        foreach (CharacterCustomizationPart part in Enum.GetValues(typeof(CharacterCustomizationPart)))
        {
            int itemIndex = part == CharacterCustomizationPart.BodyColor ? 1 : 0;
            SetPartItemIndex(part, itemIndex);
        }
    }

    public bool TryGetLastItemIndex(CharacterCustomizationPart part, out int itemIndex)
    {
        EnsureSlotsInitialized();
        return _lastItemIndexByPart.TryGetValue(part, out itemIndex);
    }

    public bool HasSlotFor(CharacterCustomizationPart part)
    {
        EnsureSlotsInitialized();
        return _slots.ContainsKey(part);
    }

    private void RebuildSlotMap()
    {
        _slots.Clear();

        CustomizationPartSlot[] found = GetComponentsInChildren<CustomizationPartSlot>(true);
        foreach (CustomizationPartSlot slot in found)
        {
            if (_slots.ContainsKey(slot.Part))
            {
                Debug.LogWarning(
                    $"{nameof(CustomizationPartItemsActivator)}: 부위 {slot.Part}에 대한 {nameof(CustomizationPartSlot)}가 중복입니다. ({slot.name})",
                    slot);
                continue;
            }

            _slots.Add(slot.Part, slot);
        }
    }
}
