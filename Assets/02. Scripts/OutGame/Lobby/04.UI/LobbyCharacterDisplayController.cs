using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyCharacterDisplayController : MonoBehaviourPunCallbacks
{
    [SerializeField] private CharacterNicknameView _localCharacter;
    [SerializeField] private CharacterNicknameView _partyCharacter;
    [Header("파티원 외형 (로컬은 LobbyManager.PartItemsActivator 공유)")]
    [SerializeField] private CustomizationPartItemsActivator _partyPartActivator;

    [Header("Lobby Nickname Objects (Customization 토글)")]
    [SerializeField] private GameObject _myNicknameObject;
    [SerializeField] private GameObject _friendNicknameObject;

    [Header("Ready Check Images (옵션 · _useReadyCheck = false면 무시)")]
    [SerializeField] private bool _useReadyCheck = true;
    [SerializeField] private GameObject _localReadyCheckImage;
    [SerializeField] private GameObject _partyReadyCheckImage;

    [Header("Ready Animation (옵션 · _useReadyAnimation = false면 무시)")]
    [SerializeField] private bool _useReadyAnimation;
    [SerializeField] private Animator _localReadyAnimator;
    [SerializeField] private Animator _partyReadyAnimator;

    [Header("Ready 상태 시 비활성화할 버튼")]
    [SerializeField] private Button _customizationButton;

    private static readonly int ReadyAnimParam = Animator.StringToHash("Ready");

    private bool _started;
    private bool _lobbyEventsHooked;
    private bool _nicknameObjectsDesiredActive = true;

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

        ApplyReadyVisual(_localReadyCheckImage, false);
        ApplyReadyVisual(_partyReadyCheckImage, false);

        RefreshNicknameObjectsActive();

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

        SlidePanelStateController.SwitchToCustomizingStarted += HideNicknameObjects;
        SlidePanelStateController.SwitchToNormalCompleted += ShowNicknameObjects;

        ReadyButton.OnReadyStateChanged += HandleReadyStateChanged;

        SyncPartyPartnerUiIfAlreadyInParty();
    }

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

        SlidePanelStateController.SwitchToCustomizingStarted -= HideNicknameObjects;
        SlidePanelStateController.SwitchToNormalCompleted -= ShowNicknameObjects;

        ReadyButton.OnReadyStateChanged -= HandleReadyStateChanged;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (targetPlayer == null || changedProps == null || changedProps.Count == 0)
            return;

        bool nicknameChanged = changedProps.ContainsKey(ActorProperties.PlayerName);
        bool customizationChanged = HasCustomizationPropertyChange(changedProps);
        bool readyChanged = changedProps.ContainsKey(LobbyMatchmakingKeys.Ready);

        if (readyChanged && _useReadyCheck)
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
            if (LobbyManager.Instance != null && LobbyManager.Instance.PartItemsActivators != null)
            {
                foreach (var activator in LobbyManager.Instance.PartItemsActivators)
                {
                    if (activator != null)
                        LobbyCustomizationPhotonApplier.ApplyFromPlayer(PhotonNetwork.LocalPlayer, activator);
                }
            }
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

        RefreshNicknameObjectsActive(hasPartyOverride: true);

        if (LobbyManager.Instance != null && LobbyManager.Instance.PartItemsActivators != null)
        {
            foreach (var activator in LobbyManager.Instance.PartItemsActivators)
            {
                if (activator != null)
                    LobbyCustomizationPhotonApplier.ApplyFromPlayer(PhotonNetwork.LocalPlayer, activator);
            }
        }
        LobbyCustomizationPhotonApplier.ApplyFromPlayer(partner, _partyPartActivator);
    }

    private void HandlePartyCleared()
    {
        if (_partyCharacter != null)
        {
            _partyCharacter.ClearNickname();
            _partyCharacter.SetVisible(false);
        }
        ApplyReadyVisual(_partyReadyCheckImage, false);

        if (_useReadyAnimation && _partyReadyAnimator != null)
            _partyReadyAnimator.SetBool(ReadyAnimParam, false);

        RefreshNicknameObjectsActive(hasPartyOverride: false);

        if (_partyPartActivator != null)
            _partyPartActivator.ApplyDefaultCustomization();
    }

    private void RefreshReadyChecks()
    {
        if (!_useReadyCheck)
            return;

        bool localReady = PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                LobbyMatchmakingKeys.Ready, out object lv) && lv is bool lb && lb;

        ApplyReadyVisual(_localReadyCheckImage, localReady);

        bool partnerReady = false;
        if (LobbyPartyService.Instance != null &&
            LobbyPartyService.Instance.LocalPlayerHasParty &&
            LobbyPartyService.Instance.TryGetPartyPartner(out Player partner) &&
            partner != null)
        {
            partnerReady = partner.CustomProperties.TryGetValue(
                LobbyMatchmakingKeys.Ready, out object pv) && pv is bool pb && pb;
        }

        ApplyReadyVisual(_partyReadyCheckImage, partnerReady);

        if (_useReadyAnimation && _partyReadyAnimator != null)
            _partyReadyAnimator.SetBool(ReadyAnimParam, partnerReady);
    }

    private void HandleReadyStateChanged(bool isReady)
    {
        if (_useReadyAnimation && _localReadyAnimator != null)
            _localReadyAnimator.SetBool(ReadyAnimParam, isReady);

        if (_customizationButton != null)
            _customizationButton.interactable = !isReady;
    }

    private static void ApplyReadyVisual(GameObject image, bool visible)
    {
        if (image != null)
            image.SetActive(visible);
    }

    private void ShowNicknameObjects()
    {
        _nicknameObjectsDesiredActive = true;
        RefreshNicknameObjectsActive();
    }

    private void HideNicknameObjects()
    {
        _nicknameObjectsDesiredActive = false;
        RefreshNicknameObjectsActive();
    }

    private void RefreshNicknameObjectsActive(bool? hasPartyOverride = null)
    {
        bool hasParty = hasPartyOverride ?? (LobbyPartyService.Instance != null &&
                        LobbyPartyService.Instance.LocalPlayerHasParty);
        bool active = _nicknameObjectsDesiredActive;

        if (_myNicknameObject != null)
            _myNicknameObject.SetActive(active);
        if (_friendNicknameObject != null)
            _friendNicknameObject.SetActive(active && hasParty);
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
