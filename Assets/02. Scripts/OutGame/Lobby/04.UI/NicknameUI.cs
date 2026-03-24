using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NicknameUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button _confirmButton;

    public event Action<string> OnConfirmClicked;

    public string Nickname => _inputField.text;

    private void Start()
    {
        _confirmButton.onClick.AddListener(HandleConfirm);
    }

    private void HandleConfirm()
    {
        string nick = _inputField.text.Trim();
        if (string.IsNullOrEmpty(nick)) return;
        OnConfirmClicked?.Invoke(nick);
    }

    public void SetNickname(string nickname)
    {
        _inputField.text = nickname;
    }

    public void SetInteractable(bool interactable)
    {
        _inputField.interactable = interactable;
        _confirmButton.interactable = interactable;
    }
}
