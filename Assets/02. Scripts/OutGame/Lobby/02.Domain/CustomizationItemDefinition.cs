using UnityEngine;

[CreateAssetMenu(fileName = "CustomizationItem", menuName = "Lobby/Customization/Item Definition", order = 0)]
public class CustomizationItemDefinition : ScriptableObject
{
    [SerializeField] private CharacterCustomizationPart _part;

    [Tooltip("썸네일·아이콘 등에 쓸 Addressables 주소(키).")]
    [SerializeField] private string _iconAddress;

    [Tooltip("CustomizationPartSlot.SetActiveItemIndex 에 넣는 값. 1 = 첫 번째 자식, 2 = 두 번째 …")]
    [SerializeField] private int _slotItemIndex = 1;

    public CharacterCustomizationPart Part => _part;
    public string IconAddress => _iconAddress;
    public int SlotItemIndex => _slotItemIndex;
}
