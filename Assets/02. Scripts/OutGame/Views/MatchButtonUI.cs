using System;
using UnityEngine;
using UnityEngine.UI;

public class MatchButtonUI : MonoBehaviour
{
    private Button _matchButton;

    public event Action OnMatchClicked;

    private void Awake()
    {
        _matchButton = GetComponent<Button>();
    }

    private void Start()
    {
        _matchButton.onClick.AddListener(HandleMatch);
    }

    private void HandleMatch()
    {
        OnMatchClicked?.Invoke();
    }

    public void SetInteractable(bool interactable)
    {
        _matchButton.interactable = interactable;
    }
}
