using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapInfoUI : MonoBehaviour
{
    [SerializeField] private Image _thumbnail;
    [SerializeField] private TMP_Text _mapNameText;

    private void OnEnable()
    {
        if (MapSelectionManager.Instance != null)
            MapSelectionManager.Instance.OnMapSelected += OnMapSelected;
    }

    private void OnDisable()
    {
        if (MapSelectionManager.Instance != null)
            MapSelectionManager.Instance.OnMapSelected -= OnMapSelected;
    }

    private void OnMapSelected(MapDefinition mapDef)
    {
        if (_thumbnail != null)
            _thumbnail.sprite = mapDef.Thumbnail;

        if (_mapNameText != null)
            _mapNameText.text = mapDef.MapName;
    }
}
