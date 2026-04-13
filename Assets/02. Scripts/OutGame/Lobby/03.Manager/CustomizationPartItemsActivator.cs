using System;
using System.Collections.Generic;
using UnityEngine;

public class CustomizationPartItemsActivator : MonoBehaviour
{
    [Serializable]
    public struct PartEntry
    {
        [Tooltip("이 엔트리가 담당하는 파츠")]
        public CharacterCustomizationPart part;

        [Tooltip("해당 파츠의 아이템 오브젝트들. 원본 프리팹 내 어디에 있어도 됨. 인덱스 1 = items[0], 2 = items[1], ...")]
        public GameObject[] items;
    }

    [Tooltip("파츠별 아이템 매핑. 파츠당 하나의 엔트리를 만들고, 해당 파츠 아이템 오브젝트들을 드래그.")]
    [SerializeField] private PartEntry[] _entries;

    private readonly Dictionary<CharacterCustomizationPart, GameObject[]> _itemsByPart =
        new Dictionary<CharacterCustomizationPart, GameObject[]>();

    private readonly Dictionary<CharacterCustomizationPart, int> _lastItemIndexByPart =
        new Dictionary<CharacterCustomizationPart, int>();

    public event Action<CharacterCustomizationPart, int> PartItemSelectionChanged;

    private void Awake()
    {
        RebuildMap();
    }

    /// <summary>
    /// 파티 캐릭터 등 비활성 상태로 씬에 있으면 <see cref="Awake"/>가 호출되지 않아 맵이 비어 있을 수 있습니다.
    /// 첫 사용 시점에 맵을 채웁니다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_itemsByPart.Count > 0)
            return;
        RebuildMap();
    }

    public void RefreshActivators()
    {
        RebuildMap();
    }

    public void SetPartItemIndex(CharacterCustomizationPart part, int itemIndex)
    {
        EnsureInitialized();
        if (!_itemsByPart.TryGetValue(part, out GameObject[] items))
        {
            Debug.LogWarning($"{nameof(CustomizationPartItemsActivator)}: 부위 {part}에 대한 엔트리가 없습니다.", this);
            return;
        }

        ApplyIndex(part, items, itemIndex);
        _lastItemIndexByPart[part] = itemIndex;
        PartItemSelectionChanged?.Invoke(part, itemIndex);
    }

    public void ApplyDefaultCustomization()
    {
        EnsureInitialized();
        foreach (CharacterCustomizationPart part in Enum.GetValues(typeof(CharacterCustomizationPart)))
        {
            int itemIndex = part == CharacterCustomizationPart.BodyColor ? 1 : 0;
            SetPartItemIndex(part, itemIndex);
        }
    }

    public bool TryGetLastItemIndex(CharacterCustomizationPart part, out int itemIndex)
    {
        EnsureInitialized();
        return _lastItemIndexByPart.TryGetValue(part, out itemIndex);
    }

    public bool HasSlotFor(CharacterCustomizationPart part)
    {
        EnsureInitialized();
        return _itemsByPart.ContainsKey(part);
    }

    private void RebuildMap()
    {
        _itemsByPart.Clear();
        if (_entries == null) return;

        foreach (var entry in _entries)
        {
            if (_itemsByPart.ContainsKey(entry.part))
            {
                Debug.LogWarning(
                    $"{nameof(CustomizationPartItemsActivator)}: 부위 {entry.part}에 대한 엔트리가 중복입니다.",
                    this);
                continue;
            }

            _itemsByPart.Add(entry.part, entry.items ?? Array.Empty<GameObject>());
        }
    }

    /// <summary>
    /// index == 0 → 전부 끔 (미착용).
    /// index == i (1..N) → items[i-1]만 켬, 나머지는 끔.
    /// </summary>
    private void ApplyIndex(CharacterCustomizationPart part, GameObject[] items, int index)
    {
        if (items == null || items.Length == 0)
            return;

        int maxIndex = items.Length;
        int clamped = Mathf.Clamp(index, 0, maxIndex);
        if (clamped != index)
        {
            Debug.LogWarning(
                $"{nameof(CustomizationPartItemsActivator)} [{part}]: 인덱스 {index}가 범위 [0, {maxIndex}] 밖입니다. {clamped}로 적용합니다.",
                this);
        }

        if (clamped <= 0)
        {
            for (int i = 0; i < items.Length; i++)
                if (items[i] != null) items[i].SetActive(false);
            return;
        }

        for (int i = 0; i < items.Length; i++)
            if (items[i] != null) items[i].SetActive(clamped == i + 1);
    }
}
