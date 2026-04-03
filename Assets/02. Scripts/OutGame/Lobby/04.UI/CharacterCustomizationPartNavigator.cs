using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 커스터마이징 부위를 enum 순서대로 순회하고, 현재 선택 부위 한글명을 표시합니다.
/// </summary>
public class CharacterCustomizationPartNavigator : MonoBehaviour
{
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private TMP_Text _partNameLabel;

    private static readonly CharacterCustomizationPart[] Parts =
        (CharacterCustomizationPart[])Enum.GetValues(typeof(CharacterCustomizationPart));

    private int _index;

    public CharacterCustomizationPart CurrentPart => Parts[_index];

    public int CurrentPartIndex => _index;

    /// <summary>현재 선택 인덱스가 바뀔 때마다 호출됩니다.</summary>
    public event Action<int> PartIndexChanged;

    private void Start()
    {
        if (_previousButton != null)
            _previousButton.onClick.AddListener(OnPreviousClicked);
        if (_nextButton != null)
            _nextButton.onClick.AddListener(OnNextClicked);

        RefreshView();
    }

    private void OnDisable()
    {
        if (_previousButton != null)
            _previousButton.onClick.RemoveListener(OnPreviousClicked);
        if (_nextButton != null)
            _nextButton.onClick.RemoveListener(OnNextClicked);
    }

    private void OnPreviousClicked()
    {
        if (_index <= 0)
            return;

        _index--;
        RefreshView();
    }

    private void OnNextClicked()
    {
        if (_index >= Parts.Length - 1)
            return;

        _index++;
        RefreshView();
    }

    private void RefreshView()
    {
        if (_partNameLabel != null)
            _partNameLabel.text = CharacterCustomizationPartNames.GetDisplayName(Parts[_index]);

        if (_previousButton != null)
            _previousButton.interactable = _index > 0;
        if (_nextButton != null)
            _nextButton.interactable = _index < Parts.Length - 1;

        PartIndexChanged?.Invoke(_index);
    }
}
