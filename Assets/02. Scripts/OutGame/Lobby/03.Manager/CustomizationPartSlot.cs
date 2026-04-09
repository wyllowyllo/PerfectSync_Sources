using UnityEngine;

public class CustomizationPartSlot : MonoBehaviour
{
    [SerializeField] private CharacterCustomizationPart _part;

    private int _minIndex = 0;
    private int _maxIndex;

    public CharacterCustomizationPart Part => _part;

    private void OnValidate()
    {
        RefreshIndexBounds();
    }

    private void Awake()
    {
        RefreshIndexBounds();
    }

    private void RefreshIndexBounds()
    {
        _minIndex = 0;
        _maxIndex = transform.childCount;
    }

    public void SetActiveItemIndex(int index)
    {
        RefreshIndexBounds();

        int clamped = Mathf.Clamp(index, _minIndex, _maxIndex);
        if (clamped != index)
        {
            Debug.LogWarning(
                $"{nameof(CustomizationPartSlot)} [{_part}]: 인덱스 {index}가 범위 [{_minIndex}, {_maxIndex}] 밖입니다. {clamped}로 적용합니다.",
                this);
        }

        int count = transform.childCount;

        if (clamped <= 0)
        {
            for (int i = 0; i < count; i++)
                transform.GetChild(i).gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < count; i++)
            transform.GetChild(i).gameObject.SetActive(clamped == i + 1);
    }
}
