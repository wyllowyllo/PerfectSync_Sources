using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class LobbyCharacterDisplayController : MonoBehaviour
{
    [SerializeField] private LobbyCharacterNicknameView _localCharacter;
    [SerializeField] private LobbyCharacterNicknameView _partyCharacter;

    private bool _started;

    private void OnEnable()
    {
        if (!_started)
            return;

        SubscribeEvents();

        if (IsInLobbyRoom())
            ApplyLocalNickname(PhotonNetwork.NickName);
    }

    private void Start()
    {
        SubscribeEvents();
        _started = true;

        if (IsInLobbyRoom())
            ApplyLocalNickname(PhotonNetwork.NickName);
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined += HandleLobbyRoomJoined;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.NicknameFieldSet += ApplyLocalNickname;

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyPartnerLinked += HandlePartyPartnerLinked;
            LobbyPartyService.Instance.OnPartyCleared += HandlePartyCleared;
        }
    }

    private void UnsubscribeEvents()
    {
        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined -= HandleLobbyRoomJoined;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.NicknameFieldSet -= ApplyLocalNickname;

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyPartnerLinked -= HandlePartyPartnerLinked;
            LobbyPartyService.Instance.OnPartyCleared -= HandlePartyCleared;
        }
    }

    private void HandlePartyPartnerLinked(Player partner)
    {
        if (_partyCharacter == null || partner == null)
            return;

        string nick = partner.NickName ?? string.Empty;
        _partyCharacter.SetVisible(true);
        _partyCharacter.SetNickname(nick);
    }

    private void HandlePartyCleared()
    {
        if (_partyCharacter == null)
            return;

        _partyCharacter.ClearNickname();
        _partyCharacter.SetVisible(false);
    }

    private void HandleLobbyRoomJoined()
    {
        ApplyLocalNickname(PhotonNetwork.NickName);
    }

    private void ApplyLocalNickname(string nickname)
    {
        if (_localCharacter == null)
            return;

        string display = !string.IsNullOrEmpty(nickname) ? nickname : PhotonNetwork.NickName;
        _localCharacter.SetNickname(display ?? string.Empty);
    }

    private static bool IsInLobbyRoom()
    {
        if (!PhotonNetwork.InRoom)
            return false;

        return PhotonRoomSnapshotReader.TryGetCurrent(out var snap) && snap.Kind == RoomKind.Lobby;
    }

    public void SetPartyMemberVisible(bool visible, string partyMemberNickname = null)
    {
        if (_partyCharacter == null)
            return;

        if (visible)
        {
            _partyCharacter.SetVisible(true);
            _partyCharacter.SetNickname(partyMemberNickname ?? string.Empty);
        }
        else
        {
            _partyCharacter.ClearNickname();
            _partyCharacter.SetVisible(false);
        }
    }
}
