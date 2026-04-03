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

    public void RefreshActivators()
    {
        RebuildSlotMap();
    }

    public void SetPartItemIndex(CharacterCustomizationPart part, int itemIndex)
    {
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
        foreach (CharacterCustomizationPart part in Enum.GetValues(typeof(CharacterCustomizationPart)))
        {
            int itemIndex = part == CharacterCustomizationPart.BodyColor ? 1 : 0;
            SetPartItemIndex(part, itemIndex);
        }
    }

    public bool TryGetLastItemIndex(CharacterCustomizationPart part, out int itemIndex)
    {
        return _lastItemIndexByPart.TryGetValue(part, out itemIndex);
    }

    public bool HasSlotFor(CharacterCustomizationPart part)
    {
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
