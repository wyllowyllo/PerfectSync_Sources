using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class CeremonyManager : MonoBehaviourPunCallbacks
{
    [Header("시상대 캐릭터 Activator (1등~4등 순서)")]
    [SerializeField] private CustomizationPartItemsActivator[] _podiumActivators =
        new CustomizationPartItemsActivator[4];

    [Header("Timing")]
    [SerializeField] private float _showReturnUIDelaySeconds = 3f;
    [SerializeField] private float _autoReturnSeconds = 15f;

    [Header("Scene")]
    [SerializeField] private string _lobbySceneName = "Lobby";

    public static event Action OnReturnUIShowRequested;

    private bool _waitingForInput;
    private bool _isLeaving;

    public override void OnEnable()
    {
        base.OnEnable();
    }

    private void Start()
    {
        if (InGameManager.Instance != null)
            InGameManager.Instance.OnCeremonyReady += HandleCeremonyReady;
    }

    public override void OnDisable()
    {
        base.OnDisable();

        if (InGameManager.Instance != null)
            InGameManager.Instance.OnCeremonyReady -= HandleCeremonyReady;
    }

    private void HandleCeremonyReady()
    {
        ApplyRankCustomizations();
        StartCoroutine(CeremonyFlowRoutine());
    }

    private void ApplyRankCustomizations()
    {
        if (RaceRankingManager.Instance == null) return;

        IReadOnlyList<TeamRankEntry> rankings = RaceRankingManager.Instance.CurrentRankings;

        for (int i = 0; i < _podiumActivators.Length; i++)
        {
            if (_podiumActivators[i] == null) continue;

            if (i >= rankings.Count)
            {
                _podiumActivators[i].ApplyDefaultCustomization();
                continue;
            }

            ApplyTeamCustomizationToPodium(rankings[i].TeamNumber, _podiumActivators[i]);
        }
    }

    private static void ApplyTeamCustomizationToPodium(
        int teamNumber, CustomizationPartItemsActivator activator)
    {
        Photon.Realtime.Player hostPlayer = FindTeamHostPlayer(teamNumber);
        if (hostPlayer == null)
        {
            activator.ApplyDefaultCustomization();
            return;
        }

        LobbyCustomizationPhotonApplier.ApplyFromPlayer(hostPlayer, activator);
    }

    private static Photon.Realtime.Player FindTeamHostPlayer(int teamNumber)
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (PhotonTeamManager.GetTeamRaw(p) != teamNumber) continue;
            if (PhotonTeamManager.GetTeamSlot(p) == PhotonTeamManager.SlotHost)
                return p;
        }

        return null;
    }

    private IEnumerator CeremonyFlowRoutine()
    {
        yield return new WaitForSeconds(_showReturnUIDelaySeconds);

        OnReturnUIShowRequested?.Invoke();
        _waitingForInput = true;

        StartCoroutine(AutoReturnRoutine());
    }

    private void Update()
    {
        if (!_waitingForInput || _isLeaving) return;
        if (!Input.GetKeyDown(KeyCode.Space)) return;

        RequestLeave();
    }

    private IEnumerator AutoReturnRoutine()
    {
        yield return new WaitForSeconds(_autoReturnSeconds);

        if (!_isLeaving)
            RequestLeave();
    }

    private void RequestLeave()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            NavigateToLobby();
    }

    public override void OnLeftRoom()
    {
        NavigateToLobby();
    }

    private void NavigateToLobby()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadSceneLocal(_lobbySceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_lobbySceneName);
    }
}
