using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
#endif

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

#if UNITY_EDITOR
    /// <summary>
    /// 자식 계층을 스캔해 이름 접두사로 파츠별 아이템 목록을 자동 구성한다.
    /// (Hair06, Hair07, ... → Hair 파츠; Hat16~21 → Hat 파츠 등)
    /// 복수형 컨테이너(Bodyparts, Tails, MouthandNoses)는 제외.
    /// </summary>
    [ContextMenu("Auto Populate From Children")]
    private void AutoPopulateFromChildren()
    {
        var buckets = new Dictionary<CharacterCustomizationPart, List<GameObject>>();
        var transforms = GetComponentsInChildren<Transform>(true);

        foreach (var tr in transforms)
        {
            if (tr == null || tr == transform)
                continue;

            GameObject go = tr.gameObject;
            if (!TryMatchPart(go.name, out CharacterCustomizationPart part))
                continue;

            if (!buckets.TryGetValue(part, out List<GameObject> list))
            {
                list = new List<GameObject>();
                buckets[part] = list;
            }
            list.Add(go);
        }

        var entries = new List<PartEntry>();
        foreach (CharacterCustomizationPart part in Enum.GetValues(typeof(CharacterCustomizationPart)))
        {
            if (!buckets.TryGetValue(part, out List<GameObject> list) || list.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(CustomizationPartItemsActivator)} [Auto Populate]: 부위 {part}에 매칭되는 자식이 없어 빈 엔트리를 남깁니다.",
                    this);
                entries.Add(new PartEntry { part = part, items = Array.Empty<GameObject>() });
                continue;
            }

            list.Sort((a, b) => NaturalCompare(a.name, b.name));
            entries.Add(new PartEntry { part = part, items = list.ToArray() });
        }

        Undo.RecordObject(this, "Auto Populate Customization Entries");
        _entries = entries.ToArray();
        EditorUtility.SetDirty(this);

        Debug.Log(
            $"{nameof(CustomizationPartItemsActivator)} [Auto Populate]: 완료. " +
            string.Join(", ", entries.Select(e => $"{e.part}={e.items.Length}")),
            this);
    }

    private static bool TryMatchPart(string goName, out CharacterCustomizationPart part)
    {
        part = default;
        if (string.IsNullOrEmpty(goName))
            return false;

        // "접두사 + 선택 숫자" 패턴만 인정. 복수형 컨테이너(Bodyparts, Tails 등)는
        // 접두사 자체가 switch에서 매칭되지 않아 자연스럽게 제외된다.
        var match = Regex.Match(goName, @"^([A-Za-z]+)(\d*)$");
        if (!match.Success)
            return false;

        string prefix = match.Groups[1].Value;

        switch (prefix)
        {
            case "Hair": part = CharacterCustomizationPart.Hair; return true;
            case "Hat": part = CharacterCustomizationPart.Hat; return true;
            case "Horn": part = CharacterCustomizationPart.Horn; return true;
            case "Ear":
            case "Ears": part = CharacterCustomizationPart.Ears; return true;
            case "Eye":
            case "Eyes": part = CharacterCustomizationPart.Eyes; return true;
            case "Nose": part = CharacterCustomizationPart.Nose; return true;
            case "Mouth": part = CharacterCustomizationPart.Mouth; return true;
            case "Glove":
            case "Gloves": part = CharacterCustomizationPart.Gloves; return true;
            case "Tail": part = CharacterCustomizationPart.Tail; return true;
            case "Bodypart":
            case "Body": part = CharacterCustomizationPart.Body; return true;
            case "BodyColor": part = CharacterCustomizationPart.BodyColor; return true;
        }

        return false;
    }

    private static int NaturalCompare(string a, string b)
    {
        var ma = Regex.Match(a ?? string.Empty, @"^([A-Za-z]+)(\d*)$");
        var mb = Regex.Match(b ?? string.Empty, @"^([A-Za-z]+)(\d*)$");

        if (ma.Success && mb.Success)
        {
            int prefixCmp = string.Compare(ma.Groups[1].Value, mb.Groups[1].Value, StringComparison.Ordinal);
            if (prefixCmp != 0)
                return prefixCmp;

            string digitsA = ma.Groups[2].Value;
            string digitsB = mb.Groups[2].Value;
            if (digitsA.Length == 0 && digitsB.Length == 0)
                return 0;
            if (digitsA.Length == 0)
                return -1;
            if (digitsB.Length == 0)
                return 1;

            int numA = int.Parse(digitsA);
            int numB = int.Parse(digitsB);
            return numA.CompareTo(numB);
        }

        return string.Compare(a, b, StringComparison.Ordinal);
    }
#endif
}
