using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyCharacterDisplayController : MonoBehaviourPunCallbacks
{
    [SerializeField] private LobbyCharacterNicknameView _localCharacter;
    [SerializeField] private LobbyCharacterNicknameView _partyCharacter;
    [Header("파티원 외형 (로컬은 LobbyManager.PartItemsActivator 공유)")]
    [SerializeField] private CustomizationPartItemsActivator _partyPartActivator;

    private bool _started;
    private bool _lobbyEventsHooked;

    public override void OnEnable()
    {
        base.OnEnable();

        if (!_started)
            return;

        SubscribeLobbyEvents();

        if (IsInLobbyRoom())
            ApplyLocalNickname(PhotonNetwork.NickName);
    }

    private void Start()
    {
        SubscribeLobbyEvents();
        _started = true;

        if (_localCharacter != null)
            _localCharacter.SetReadyCheck(false);
        if (_partyCharacter != null)
            _partyCharacter.SetReadyCheck(false);

        if (IsInLobbyRoom())
            ApplyLocalNickname(PhotonNetwork.NickName);
    }

    public override void OnDisable()
    {
        UnsubscribeLobbyEvents();
        base.OnDisable();
    }

    private void SubscribeLobbyEvents()
    {
        if (_lobbyEventsHooked)
            return;

        _lobbyEventsHooked = true;

        if (LobbyRoomConnector.Instance != null)
            LobbyRoomConnector.Instance.OnLobbyRoomJoined += HandleLobbyRoomJoined;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.NicknameFieldSet += ApplyLocalNickname;

        if (LobbyPartyService.Instance != null)
        {
            LobbyPartyService.Instance.OnPartyPartnerLinked += HandlePartyPartnerLinked;
            LobbyPartyService.Instance.OnPartyCleared += HandlePartyCleared;
        }

        SyncPartyPartnerUiIfAlreadyInParty();
    }

    /// <summary>
    /// 이미 파티 중인 채 로비에 들어오거나, <see cref="OnPartyPartnerLinked"/>가 구독 전에 발생한 경우 UI를 맞춥니다.
    /// </summary>
    private void SyncPartyPartnerUiIfAlreadyInParty()
    {
        if (LobbyPartyService.Instance == null ||
            !LobbyPartyService.Instance.LocalPlayerHasParty ||
            !LobbyPartyService.Instance.TryGetPartyPartner(out Player partner) ||
            partner == null)
            return;

        HandlePartyPartnerLinked(partner);
    }

    private void UnsubscribeLobbyEvents()
    {
        if (!_lobbyEventsHooked)
            return;

        _lobbyEventsHooked = false;

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

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (targetPlayer == null || changedProps == null || changedProps.Count == 0)
            return;

        bool nicknameChanged = changedProps.ContainsKey(ActorProperties.PlayerName);
        bool customizationChanged = HasCustomizationPropertyChange(changedProps);
        bool readyChanged = changedProps.ContainsKey(LobbyMatchmakingKeys.Ready);

        if (readyChanged)
            RefreshReadyChecks();

        if (!nicknameChanged && !customizationChanged)
            return;

        Player partyPartner = null;
        bool hasPartyPartner = LobbyPartyService.Instance != null &&
                               LobbyPartyService.Instance.LocalPlayerHasParty &&
                               LobbyPartyService.Instance.TryGetPartyPartner(out partyPartner) &&
                               partyPartner != null;

        if (nicknameChanged && _partyCharacter != null && hasPartyPartner &&
            targetPlayer.ActorNumber == partyPartner.ActorNumber)
        {
            _partyCharacter.SetNickname(partyPartner.NickName ?? string.Empty);
        }

        if (!customizationChanged)
            return;

        if (targetPlayer.IsLocal)
        {
            CustomizationPartItemsActivator localActivator = LobbyManager.Instance != null
                ? LobbyManager.Instance.PartItemsActivator
                : null;
            LobbyCustomizationPhotonApplier.ApplyFromPlayer(PhotonNetwork.LocalPlayer, localActivator);
            return;
        }

        if (_partyPartActivator == null || !hasPartyPartner ||
            targetPlayer.ActorNumber != partyPartner.ActorNumber)
            return;

        LobbyCustomizationPhotonApplier.ApplyFromPlayer(partyPartner, _partyPartActivator);
    }

    private static bool HasCustomizationPropertyChange(Hashtable changedProps)
    {
        foreach (CharacterCustomizationPart part in Enum.GetValues(typeof(CharacterCustomizationPart)))
        {
            if (changedProps.ContainsKey(CustomizationPhotonKeys.GetKey(part)))
                return true;
        }

        return false;
    }

    private void HandlePartyPartnerLinked(Player partner)
    {
        if (_partyCharacter == null || partner == null)
            return;

        string nick = partner.NickName ?? string.Empty;
        _partyCharacter.SetVisible(true);
        _partyCharacter.SetNickname(nick);

        CustomizationPartItemsActivator localActivator = LobbyManager.Instance != null
            ? LobbyManager.Instance.PartItemsActivator
            : null;
        LobbyCustomizationPhotonApplier.ApplyFromPlayer(PhotonNetwork.LocalPlayer, localActivator);
        LobbyCustomizationPhotonApplier.ApplyFromPlayer(partner, _partyPartActivator);
    }

    private void HandlePartyCleared()
    {
        if (_partyCharacter != null)
        {
            _partyCharacter.ClearNickname();
            _partyCharacter.SetVisible(false);
            _partyCharacter.SetReadyCheck(false);
        }

        if (_partyPartActivator != null)
            _partyPartActivator.ApplyDefaultCustomization();
    }

    private void RefreshReadyChecks()
    {
        bool localReady = PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                LobbyMatchmakingKeys.Ready, out object lv) && lv is bool lb && lb;

        if (_localCharacter != null)
            _localCharacter.SetReadyCheck(localReady);

        bool partnerReady = false;
        if (LobbyPartyService.Instance != null &&
            LobbyPartyService.Instance.LocalPlayerHasParty &&
            LobbyPartyService.Instance.TryGetPartyPartner(out Player partner) &&
            partner != null)
        {
            partnerReady = partner.CustomProperties.TryGetValue(
                LobbyMatchmakingKeys.Ready, out object pv) && pv is bool pb && pb;
        }

        if (_partyCharacter != null)
            _partyCharacter.SetReadyCheck(partnerReady);
    }

    private void HandleLobbyRoomJoined()
    {
        ApplyLocalNickname(PhotonNetwork.NickName);
        SyncPartyPartnerUiIfAlreadyInParty();
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
