using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public void ResetToFirst()
    {
        _index = 0;
        RefreshView();
    }
}
