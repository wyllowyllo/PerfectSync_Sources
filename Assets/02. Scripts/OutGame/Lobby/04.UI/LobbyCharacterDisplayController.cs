using Photon.Pun;
using UnityEngine;

/// <summary>
/// 로비 씬 부모 오브젝트에 붙여, 로컬·파티원 캐릭터의 닉네임 표시를 묶어 관리합니다.
/// 로비 Photon 방에 입장하면 <see cref="PhotonNetwork.NickName"/>으로 로컬 닉네임을 갱신합니다.
/// </summary>
public class LobbyCharacterDisplayController : MonoBehaviour
{
    [SerializeField] private LobbyCharacterNicknameView _localCharacter;
    [SerializeField] private LobbyCharacterNicknameView _partyCharacter;

    /// <summary>첫 <see cref="Start"/> 이후에만 <see cref="OnEnable"/>에서 재구독합니다(비활성→활성 시 Start는 다시 안 돎).</summary>
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
    }

    private void UnsubscribeEvents()
    {
        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined -= HandleLobbyRoomJoined;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.NicknameFieldSet -= ApplyLocalNickname;
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

    /// <summary>
    /// 파티원 캐릭터 표시(초대 수락 등 이후 UI에서 호출).
    /// </summary>
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
