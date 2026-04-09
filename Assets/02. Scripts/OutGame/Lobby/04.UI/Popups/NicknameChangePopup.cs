using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NicknameChangePopup : LobbyPopupBase
{
    [Header("UI")]
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    [Header("팝업 닫기")]
    [SerializeField] private LobbyPopupCoordinator _popupCoordinator;

    [Header("안내 문구 (선택)")]
    [SerializeField] private string _validPreviewMessage = "사용할 수 있는 닉네임입니다.";

    private void OnEnable()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(OnCancelClicked);
        if (_nicknameInput != null)
            _nicknameInput.onValueChanged.AddListener(OnNicknameInputChanged);

        ApplyOpenedState();
    }

    private void OnDisable()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (_cancelButton != null)
            _cancelButton.onClick.RemoveListener(OnCancelClicked);
        if (_nicknameInput != null)
            _nicknameInput.onValueChanged.RemoveListener(OnNicknameInputChanged);

        ClearUiFields();
    }

    private void ApplyOpenedState()
    {
        if (_statusText != null)
            _statusText.text = string.Empty;

        RefreshInputFromPhoton();
        UpdateStatusPreview();
    }

    private void ClearUiFields()
    {
        if (_nicknameInput != null)
            _nicknameInput.SetTextWithoutNotify(string.Empty);

        if (_statusText != null)
            _statusText.text = string.Empty;
    }

    private void RefreshInputFromPhoton()
    {
        if (_nicknameInput == null)
            return;

        string current = PhotonNetwork.NickName ?? string.Empty;
        _nicknameInput.SetTextWithoutNotify(current);
    }

    private void OnNicknameInputChanged(string _)
    {
        UpdateStatusPreview();
    }

    private void UpdateStatusPreview()
    {
        if (_statusText == null || _nicknameInput == null)
            return;

        if (NicknameValidator.TryValidate(_nicknameInput.text, out _, out string error))
        {
            _statusText.text = string.IsNullOrEmpty(_validPreviewMessage) ? string.Empty : _validPreviewMessage;
        }
        else
        {
            _statusText.text = error;
        }
    }

    private void OnConfirmClicked()
    {
        if (_nicknameInput == null)
            return;

        if (!NicknameValidator.TryValidate(_nicknameInput.text, out string trimmed, out string error))
        {
            if (_statusText != null)
                _statusText.text = error;
            return;
        }

        if (LobbyManager.Instance == null)
        {
            if (_statusText != null)
                _statusText.text = "로비를 불러올 수 없습니다. 잠시 후 다시 시도해 주세요.";
            return;
        }

        LobbyManager.Instance.OnNicknameConfirmed(trimmed);

        // Firebase에 닉네임 저장
        _ = FirebaseCustomizationRepository.SaveNickname(trimmed);

        ClosePopup();
    }

    private void OnCancelClicked()
    {
        ClosePopup();
    }

    private void ClosePopup()
    {
        if (_popupCoordinator != null)
            _popupCoordinator.CloseAllPopups();
    }
}
