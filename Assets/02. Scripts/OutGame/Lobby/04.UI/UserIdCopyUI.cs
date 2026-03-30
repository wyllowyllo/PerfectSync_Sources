using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserIdCopyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _userIdText;
    [SerializeField] private Button _copyButton;
    [SerializeField] private string _emptyDisplay = "";

    private string _userId;

    private void OnEnable()
    {
        if (_copyButton != null)
            _copyButton.onClick.AddListener(OnCopyClicked);

        if (PhotonServerManager.Instance != null)
        {
            PhotonServerManager.Instance.OnLocalUserIdReady += HandleUserIdReady;
            if (PhotonNetwork.IsConnectedAndReady)
                HandleUserIdReady(GetCurrentPhotonUserIdString());
        }
        else
            RefreshDisplay();
    }

    private void OnDisable()
    {
        if (_copyButton != null)
            _copyButton.onClick.RemoveListener(OnCopyClicked);

        if (PhotonServerManager.Instance != null)
            PhotonServerManager.Instance.OnLocalUserIdReady -= HandleUserIdReady;
    }

    private void HandleUserIdReady(string userId)
    {
        SetUserId(userId);
    }

    private static string GetCurrentPhotonUserIdString()
    {
        if (PhotonNetwork.LocalPlayer != null && !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId))
            return PhotonNetwork.LocalPlayer.UserId;
        if (PhotonNetwork.AuthValues != null && !string.IsNullOrEmpty(PhotonNetwork.AuthValues.UserId))
            return PhotonNetwork.AuthValues.UserId;
        return string.Empty;
    }

    public void SetUserId(string userId)
    {
        _userId = userId ?? string.Empty;
        RefreshDisplay();
    }

    public string GetUserId() => _userId;

    private void RefreshDisplay()
    {
        if (_userIdText == null) return;

        _userIdText.text = string.IsNullOrEmpty(_userId) ? _emptyDisplay : _userId;
    }

    private void OnCopyClicked()
    {
        if (string.IsNullOrEmpty(_userId)) return;

        GUIUtility.systemCopyBuffer = _userId;
    }
}
