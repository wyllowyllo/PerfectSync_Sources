using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RaceAwardsTransition : MonoBehaviourPunCallbacks
{
    private const float DefaultDelayBeforeLoadSeconds = 3f;
    private const float DefaultMaxWaitAfterFirstRaceDoneSeconds = 45f;

    [SerializeField] private float _delayBeforeLoadSeconds = DefaultDelayBeforeLoadSeconds;
    [SerializeField] private float _maxWaitAfterFirstRaceDoneSeconds = DefaultMaxWaitAfterFirstRaceDoneSeconds;

    private bool _ceremonyScheduled;
    private bool _firstRaceDoneRecorded;
    private float _firstRaceDoneRealtime;
    private Coroutine _loadCoroutine;
    private WaitForSeconds _waitDelayBeforeLoad;

    private void Awake()
    {
        _waitDelayBeforeLoad = new WaitForSeconds(_delayBeforeLoadSeconds);
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || _ceremonyScheduled || !PhotonNetwork.InRoom)
            return;

        RefreshFirstRaceDoneFromPlayers();
        TryEvaluateTransition();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient || _ceremonyScheduled || !PhotonNetwork.InRoom)
            return;

        if (changedProps != null && changedProps.ContainsKey(InGameRaceKeys.RaceDoneKey))
        {
            RefreshFirstRaceDoneFromPlayers();
            TryEvaluateTransition();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || _ceremonyScheduled || !PhotonNetwork.InRoom)
            return;

        TryEvaluateTransition();
    }

    private static bool PlayerHasRaceDone(Player player)
    {
        if (player == null) return false;
        return player.CustomProperties.TryGetValue(InGameRaceKeys.RaceDoneKey, out object o) && o is bool b && b;
    }

    private void RefreshFirstRaceDoneFromPlayers()
    {
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (!PlayerHasRaceDone(p)) continue;
            if (_firstRaceDoneRecorded) return;
            _firstRaceDoneRecorded = true;
            _firstRaceDoneRealtime = Time.realtimeSinceStartup;
            return;
        }
    }

    private bool AreAllCurrentPlayersRaceDone()
    {
        if (PhotonNetwork.PlayerList == null || PhotonNetwork.PlayerList.Length == 0)
            return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (!PlayerHasRaceDone(p))
                return false;
        }

        return true;
    }

    private void TryEvaluateTransition()
    {
        if (AreAllCurrentPlayersRaceDone())
        {
            ScheduleCeremony();
            return;
        }

        if (_firstRaceDoneRecorded &&
            Time.realtimeSinceStartup - _firstRaceDoneRealtime >= _maxWaitAfterFirstRaceDoneSeconds)
            ScheduleCeremony();
    }

    private void ScheduleCeremony()
    {
        if (_ceremonyScheduled) return;
        _ceremonyScheduled = true;

        if (_loadCoroutine != null)
            StopCoroutine(_loadCoroutine);

        _loadCoroutine = StartCoroutine(EnterCeremonyAfterDelay());
    }

    private IEnumerator EnterCeremonyAfterDelay()
    {
        yield return _waitDelayBeforeLoad;

        if (InGameManager.Instance != null)
            InGameManager.Instance.EnterCeremony();
    }
}
