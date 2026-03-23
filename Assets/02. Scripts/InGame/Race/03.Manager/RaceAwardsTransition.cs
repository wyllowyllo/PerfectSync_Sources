using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RaceAwardsTransition : MonoBehaviourPunCallbacks
{
    [SerializeField] private string _awardsSceneName = "Awards";
    [SerializeField] private float _delayBeforeLoadSeconds = 3f;
    [Tooltip("방에 첫 raceDone이 올라온 뒤, 전원이 안 모여도 이 시간이 지나면 Awards로 진행.")]
    [SerializeField] private float _maxWaitAfterFirstRaceDoneSeconds = 45f;

    private bool _awardsLoadScheduled;
    private bool _firstRaceDoneRecorded;
    private float _firstRaceDoneRealtime;
    private Coroutine _loadCoroutine;

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || _awardsLoadScheduled || !PhotonNetwork.InRoom)
            return;

        RefreshFirstRaceDoneFromPlayers();
        TryEvaluateTransition();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient || _awardsLoadScheduled || !PhotonNetwork.InRoom)
            return;

        if (changedProps != null && changedProps.ContainsKey(InGameRaceKeys.RaceDoneKey))
        {
            RefreshFirstRaceDoneFromPlayers();
            TryEvaluateTransition();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || _awardsLoadScheduled || !PhotonNetwork.InRoom)
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
            Debug.Log("[RaceAwardsTransition] 전원 raceDone → Awards 스케줄");
            ScheduleAwardsLoad();
            return;
        }

        if (_firstRaceDoneRecorded &&
            Time.realtimeSinceStartup - _firstRaceDoneRealtime >= _maxWaitAfterFirstRaceDoneSeconds)
        {
            Debug.LogWarning("[RaceAwardsTransition] 첫 raceDone 이후 최대 대기 초과 → Awards 스케줄");
            ScheduleAwardsLoad();
        }
    }

    private void ScheduleAwardsLoad()
    {
        if (_awardsLoadScheduled) return;
        _awardsLoadScheduled = true;

        if (_loadCoroutine != null)
            StopCoroutine(_loadCoroutine);

        _loadCoroutine = StartCoroutine(LoadAwardsAfterDelay());
    }

    private IEnumerator LoadAwardsAfterDelay()
    {
        yield return new WaitForSeconds(_delayBeforeLoadSeconds);

        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[RaceAwardsTransition] SceneLoader.Instance가 없습니다.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(_awardsSceneName))
        {
            Debug.LogError("[RaceAwardsTransition] Awards 씬 이름이 비어 있습니다.");
            yield break;
        }

        SceneLoader.Instance.LoadScenePhoton(_awardsSceneName);
    }
}
