using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReadyButton : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private TMP_Text _labelShadow;

    private Button _button;
    private string _lastLobbyStatusLine;

    private void Start()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnReadyClicked);

        if (_label == null)
            _label = GetComponentInChildren<TMP_Text>(true);

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.MatchingStatusChanged += OnMatchingStatusChanged;

        RefreshLabel();
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnReadyClicked);

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.MatchingStatusChanged -= OnMatchingStatusChanged;
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        RefreshLabel();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        RefreshLabel();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (changedProps == null)
            return;
        if (!changedProps.ContainsKey(LobbyMatchmakingKeys.Ready) &&
            !changedProps.ContainsKey(PhotonTeamManager.PartyIdKey))
            return;

        RefreshLabel();
    }

    private void OnMatchingStatusChanged(string message)
    {
        _lastLobbyStatusLine = message;
        RefreshLabel();
    }

    private void OnReadyClicked()
    {
        if (LobbyManager.Instance == null)
            return;

        // 이미 Ready 상태면 취소
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.LocalPlayer != null &&
            IsPlayerMatchReady(PhotonNetwork.LocalPlayer))
        {
            LobbyManager.Instance.CancelMatchReady();
            return;
        }

        LobbyManager.Instance.RequestMatch(string.Empty);
    }

    private void RefreshLabel()
    {
        if (_label == null && _labelShadow == null)
            return;

        if (LobbyManager.Instance != null)
        {
            var startSequence = LobbyManager.Instance.GetComponent<LobbyStartSequence>();
            if (startSequence != null && startSequence.IsRunning)
            {
                SetLabelText(string.IsNullOrEmpty(_lastLobbyStatusLine)
                    ? "게임 시작 준비…"
                    : _lastLobbyStatusLine);
                return;
            }
        }

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            SetLabelText(string.IsNullOrEmpty(_lastLobbyStatusLine) ? "준비" : _lastLobbyStatusLine);
            return;
        }

        bool localReady = IsPlayerMatchReady(PhotonNetwork.LocalPlayer);

        if (!localReady)
        {
            SetLabelText("준비");
            return;
        }

        // 파티 상태: 전원 Ready → "준비 취소", 일부만 → "대기 중"
        if (LobbyPartyService.Instance != null && LobbyPartyService.Instance.LocalPlayerHasParty)
        {
            SetLabelText(AreAllPartyMembersMatchReady() ? "준비 취소" : "대기 중");
            return;
        }

        // 솔로 + Ready
        SetLabelText("준비 취소");
    }

    private void SetLabelText(string text)
    {
        if (_label != null)
            _label.text = text;
        if (_labelShadow != null)
            _labelShadow.text = text;
    }

    private static bool IsPlayerMatchReady(Player player)
    {
        return player.CustomProperties.TryGetValue(LobbyMatchmakingKeys.Ready, out object v) &&
               v is bool b && b;
    }

    private static bool AreAllPartyMembersMatchReady()
    {
        var local = PhotonNetwork.LocalPlayer;
        if (local == null)
            return false;

        string partyId = GetPartyId(local);
        if (string.IsNullOrEmpty(partyId))
            return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (GetPartyId(p) != partyId)
                continue;
            if (!IsPlayerMatchReady(p))
                return false;
        }

        return true;
    }

    private static string GetPartyId(Player player)
    {
        return player.CustomProperties.TryGetValue(PhotonTeamManager.PartyIdKey, out object v)
            ? v as string
            : null;
    }
}
