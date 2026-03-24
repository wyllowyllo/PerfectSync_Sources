using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MatchingScreenUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _playerCountText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Button _leaveButton;

    public event Action OnLeaveClicked;

    private void Start()
    {
        _leaveButton.onClick.AddListener(HandleLeave);
    }

    private void HandleLeave()
    {
        OnLeaveClicked?.Invoke();
    }

    public void SetPlayerCount(int current, int max)
    {
        _playerCountText.text = $"{current} / {max}";
    }

    public void SetStatus(string message)
    {
        _statusText.text = message;
    }

    public void SetLeaveButtonInteractable(bool interactable)
    {
        _leaveButton.interactable = interactable;
    }
}
