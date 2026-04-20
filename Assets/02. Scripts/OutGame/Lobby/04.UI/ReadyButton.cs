using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReadyButton : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text _label;

    public static event Action<bool> OnReadyStateChanged;

    private Button _button;
    private bool _currentReadyState;

    private void Start()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnReadyClicked);

        LobbyPartyService.PartyWaitStarted += HandlePartyWaitStarted;
        LobbyPartyService.PartyWaitEnded += HandlePartyWaitEnded;

        if (_label == null)
            _label = GetComponentInChildren<TMP_Text>(true);

        RefreshLabel();
    }

    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnReadyClicked);

        LobbyPartyService.PartyWaitStarted -= HandlePartyWaitStarted;
        LobbyPartyService.PartyWaitEnded -= HandlePartyWaitEnded;
    }

    private void HandlePartyWaitStarted()
    {
        if (_button != null) _button.interactable = false;
    }

    private void HandlePartyWaitEnded()
    {
        if (_button != null) _button.interactable = true;
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

    private void OnReadyClicked()
    {
        if (LobbyManager.Instance == null)
            return;

        if (PhotonNetwork.InRoom &&
            PhotonNetwork.LocalPlayer != null &&
            (IsPlayerMatchReady(PhotonNetwork.LocalPlayer) || LobbyManager.Instance.IsAwaitingReadyDelay))
        {
            LobbyManager.Instance.CancelMatchReady();
            RefreshLabel();
            return;
        }

        LobbyManager.Instance.RequestMatch(string.Empty);
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (_label == null) return;

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            SetLabelText("준비");
            UpdateReadyState(false);
            return;
        }

        bool localReady = IsPlayerMatchReady(PhotonNetwork.LocalPlayer);
        bool awaitingReadyDelay = LobbyManager.Instance != null && LobbyManager.Instance.IsAwaitingReadyDelay;
        bool inMatchmakingIntent = localReady || awaitingReadyDelay;

        SetLabelText(inMatchmakingIntent ? "준비 취소" : "준비");
        UpdateReadyState(inMatchmakingIntent);
    }

    private void UpdateReadyState(bool isReady)
    {
        if (_currentReadyState == isReady) return;
        _currentReadyState = isReady;
        OnReadyStateChanged?.Invoke(isReady);
    }

    private void SetLabelText(string text)
    {
        if (_label != null)
            _label.text = text;
    }

    private static bool IsPlayerMatchReady(Player player)
    {
        return player.CustomProperties.TryGetValue(LobbyMatchmakingKeys.Ready, out object v) &&
               v is bool b && b;
    }
}
