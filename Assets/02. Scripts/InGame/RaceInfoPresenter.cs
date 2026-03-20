using System.Collections;
using UnityEngine;

/// <summary>
/// 인게임/레이스 이벤트를 구독하고 <see cref="RaceInfoUI"/> 및 <see cref="InGameManager"/> 상태를 갱신.
/// </summary>
public class RaceInfoPresenter : MonoBehaviour
{
    [SerializeField] private RaceInfoUI _raceInfoUI;
    [SerializeField] private int _finishWindowSeconds = 10;

    private Coroutine _finishWindowRoutine;
    private bool _localRaceFinished;

    private void Start()
    {
        if (InGameManager.Instance != null)
        {
            InGameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            InGameManager.Instance.OnRaceCountdownTick += HandleRaceCountdownTick;
        }

        RaceRankingManager.OnFirstPlaceFinished += HandleFirstPlaceFinishedStatic;
        RaceRankingManager.OnTeamFinished += HandleTeamFinished;
    }

    private void OnDestroy()
    {
        if (InGameManager.Instance != null)
        {
            InGameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            InGameManager.Instance.OnRaceCountdownTick -= HandleRaceCountdownTick;
        }

        RaceRankingManager.OnFirstPlaceFinished -= HandleFirstPlaceFinishedStatic;
        RaceRankingManager.OnTeamFinished -= HandleTeamFinished;

        StopFinishWindowRoutine();
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
            _raceInfoUI?.ShowStart();
    }

    private void HandleRaceCountdownTick(int value)
    {
        _raceInfoUI?.ShowCountdown(value);
    }

    private void HandleFirstPlaceFinishedStatic(int firstTeam)
    {
        int localTeam = InGameManager.GetLocalPlayerTeam();
        if (localTeam == PhotonTeamManager.TeamNone) return;

        if (firstTeam == localTeam)
        {
            _localRaceFinished = true;
            StopFinishWindowRoutine();
            _raceInfoUI?.ShowWinner();
            InGameManager.Instance?.EnterLocalRaceComplete();
            return;
        }

        if (InGameManager.Instance == null || InGameManager.Instance.CurrentState != GameState.Playing)
            return;
        if (_localRaceFinished) return;

        StopFinishWindowRoutine();
        _finishWindowRoutine = StartCoroutine(FinishWindowRoutine());
    }

    private void HandleTeamFinished(int team, int finishPlace)
    {
        int localTeam = InGameManager.GetLocalPlayerTeam();
        if (team != localTeam) return;
        if (finishPlace == 1) return;

        if (InGameManager.Instance == null || InGameManager.Instance.CurrentState != GameState.Playing)
            return;

        _localRaceFinished = true;
        StopFinishWindowRoutine();
        _raceInfoUI?.ShowFinish();
        InGameManager.Instance.EnterLocalRaceComplete();
    }

    private IEnumerator FinishWindowRoutine()
    {
        int remaining = _finishWindowSeconds;

        while (remaining > 0)
        {
            if (_localRaceFinished) yield break;
            if (InGameManager.Instance == null || InGameManager.Instance.CurrentState != GameState.Playing)
                yield break;

            _raceInfoUI?.ShowFinishWindowSeconds(remaining);
            yield return new WaitForSeconds(1f);
            remaining--;
        }

        if (_localRaceFinished) yield break;
        if (InGameManager.Instance == null || InGameManager.Instance.CurrentState != GameState.Playing)
            yield break;

        _raceInfoUI?.ShowGameOver();
        InGameManager.Instance.EnterLocalGameOver();
    }

    private void StopFinishWindowRoutine()
    {
        if (_finishWindowRoutine != null)
        {
            StopCoroutine(_finishWindowRoutine);
            _finishWindowRoutine = null;
        }
    }
}
