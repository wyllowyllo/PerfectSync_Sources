using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 파티 초대: UserID 입력, 유효성 검사 후 <see cref="LobbyPartyService.TrySendPartyInviteByUserId"/>.
/// </summary>
[DisallowMultipleComponent]
public class LobbyInviteFriendsPanel : MonoBehaviour
{
    // PARTY_INVITE_DEBUG_REMOVE: 아래 Debug 호출·태그 상수 제거 시 이 주석도 삭제
    private const string PartyInviteDebugTag = "[PARTY_INVITE_DEBUG]";

    [Header("UI")]
    [SerializeField] private TMP_InputField _userIdInput;
    [Tooltip("기본 안내 + 오류 시 같은 필드에 메시지 표시")]
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    [Header("패널을 열 때마다 표시할 기본 문구")]
    [SerializeField] [TextArea(2, 5)] private string _defaultInstruction =
        "같은 로비에 있는 플레이어의 User ID를 입력하세요.\n(설정 화면에 표시된 ID를 복사해 붙여 넣으면 됩니다. 닉네임은 사용할 수 없습니다.)";

    [Header("팝업 닫기")]
    [SerializeField] private LobbyPopupCoordinator _popupCoordinator;

    private void OnEnable()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(OnCancelClicked);

        ApplyOpenedState();
    }

    private void OnDisable()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (_cancelButton != null)
            _cancelButton.onClick.RemoveListener(OnCancelClicked);

        ClearUiFields();
    }

    private void ApplyOpenedState()
    {
        if (_messageText != null)
            _messageText.text = _defaultInstruction;

        if (_userIdInput != null)
            _userIdInput.SetTextWithoutNotify(string.Empty);
    }

    private void ClearUiFields()
    {
        if (_userIdInput != null)
            _userIdInput.SetTextWithoutNotify(string.Empty);

        if (_messageText != null)
            _messageText.text = _defaultInstruction;
    }

    private void OnConfirmClicked()
    {
        if (_userIdInput == null)
            return;

        string raw = _userIdInput.text ?? string.Empty;
        Debug.Log($"{PartyInviteDebugTag} Confirm clicked, raw length={raw.Length}");

        if (LobbyPartyService.Instance == null)
        {
            Debug.LogWarning($"{PartyInviteDebugTag} LobbyPartyService.Instance is null");
            if (_messageText != null)
                _messageText.text = "파티 서비스를 불러올 수 없습니다.";
            return;
        }

        if (!LobbyPartyService.Instance.TrySendPartyInviteByUserId(raw, out string error))
        {
            Debug.LogWarning($"{PartyInviteDebugTag} TrySendPartyInviteByUserId failed: {error}");
            if (_messageText != null)
                _messageText.text = error;
            return;
        }

        Debug.Log($"{PartyInviteDebugTag} TrySendPartyInviteByUserId succeeded");
        ClosePopup();
    }

    private void OnCancelClicked()
    {
        Debug.Log($"{PartyInviteDebugTag} Cancel clicked → CloseAllPopups");
        ClosePopup();
    }

    private void ClosePopup()
    {
        if (_popupCoordinator != null)
            _popupCoordinator.CloseAllPopups();
    }
}
