using System;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyInvitePopup : LobbyPopupBase
{
    public static event Action OpenPartyInviteFlowRequested;
    public static event Action PartyInviteClosedOnlyRequested;
    public static event Action<string> TransientToastRequested;
    public static event Action LeavePartyRequested;

    [Header("메시지")]
    [SerializeField] private TMP_Text _partyInviteMessageText;
    [SerializeField] private string _partyInviteMessageFormat = "{0}님이 파티 초대를 보냈습니다.";

    [Header("버튼")]
    [SerializeField] private Button _partyInviteConfirmButton;
    [SerializeField] private Button _partyInviteCancelButton;

    [Header("토스트 (상대 거절 시)")]
    [SerializeField] private string _partyInviteDeclinedToast = "상대가 파티 초대를 거절했습니다.";

    [Header("파티 탈퇴 메시지")]
    [SerializeField] private string _leavePartyMessage = "파티를 탈퇴하시겠습니까?";

    private bool _isLeavePartyMode;

    public string PartyInviteDeclinedToast => _partyInviteDeclinedToast;

    public void SetLeavePartyMode()
    {
        _isLeavePartyMode = true;
        if (_partyInviteMessageText != null)
            _partyInviteMessageText.text = _leavePartyMessage;
    }

    public void SetInviteMessage(int inviterActor, string inviterUserId)
    {
        _isLeavePartyMode = false;

        string displayName = inviterUserId;
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
            Player inviter = PhotonNetwork.CurrentRoom.GetPlayer(inviterActor);
            if (inviter != null && !string.IsNullOrEmpty(inviter.NickName))
                displayName = inviter.NickName;
        }

        if (_partyInviteMessageText != null)
            _partyInviteMessageText.text = string.Format(_partyInviteMessageFormat, displayName);
    }

    private void OnEnable()
    {
        if (_partyInviteConfirmButton != null)
            _partyInviteConfirmButton.onClick.AddListener(OnPartyInviteConfirmClicked);
        if (_partyInviteCancelButton != null)
            _partyInviteCancelButton.onClick.AddListener(OnPartyInviteCancelClicked);
    }

    private void OnDisable()
    {
        if (_partyInviteConfirmButton != null)
            _partyInviteConfirmButton.onClick.RemoveListener(OnPartyInviteConfirmClicked);
        if (_partyInviteCancelButton != null)
            _partyInviteCancelButton.onClick.RemoveListener(OnPartyInviteCancelClicked);
    }

    public void DismissAsCancel()
    {
        LobbyPartyService.Instance?.RespondToPendingPartyInvite(false);
        Hide();
        PartyInviteClosedOnlyRequested?.Invoke();
    }

    private void OnPartyInviteConfirmClicked()
    {
        if (_isLeavePartyMode)
        {
            LeavePartyRequested?.Invoke();
            _isLeavePartyMode = false;
            Hide();
            return;
        }

        LobbyPartyService.Instance?.RespondToPendingPartyInvite(true);
        Hide();
        PartyInviteClosedOnlyRequested?.Invoke();
    }

    private void OnPartyInviteCancelClicked()
    {
        if (_isLeavePartyMode)
        {
            _isLeavePartyMode = false;
            Hide();
            PartyInviteClosedOnlyRequested?.Invoke();
            return;
        }

        DismissAsCancel();
    }
}
