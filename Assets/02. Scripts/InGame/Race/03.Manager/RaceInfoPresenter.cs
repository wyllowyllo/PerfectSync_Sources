using System.Collections;
using UnityEngine;

public class RaceInfoPresenter : MonoBehaviour
{
    [SerializeField] private RaceInfoUI _raceInfoUI;
    [SerializeField] private int _finishWindowSeconds = 10;

    private LocalPlayerRaceFinishRules _rules;
    private Coroutine _finishWindowRoutine;

    private void Awake()
    {
        _rules = new LocalPlayerRaceFinishRules(_finishWindowSeconds);
    }

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
        bool playing = InGameManager.Instance != null && InGameManager.Instance.CurrentState == GameState.Playing;
        var d = _rules.OnFirstPlaceFinished(
            firstTeam,
            PhotonTeamManager.GetLocalTeamRaw(),
            PhotonTeamManager.TeamNone,
            playing);

        Apply(d);
    }

    private void HandleTeamFinished(int team, int finishPlace)
    {
        bool playing = InGameManager.Instance != null && InGameManager.Instance.CurrentState == GameState.Playing;
        var d = _rules.OnLocalTeamFinishedWithPlace(
            team,
            finishPlace,
            PhotonTeamManager.GetLocalTeamRaw(),
            PhotonTeamManager.TeamNone,
            playing);

        Apply(d);
    }

    private void Apply(LocalRaceFinishDecision d)
    {
        if (d.StopFinishWindowCoroutine)
            StopFinishWindowRoutine();

        if (d.DisplayFinishWindowSeconds > 0)
            _raceInfoUI?.ShowFinishWindowSeconds(d.DisplayFinishWindowSeconds);

        if (d.ShowWinner)
            _raceInfoUI?.ShowWinner();
        if (d.ShowFinish)
            _raceInfoUI?.ShowFinish();
        if (d.ShowGameOver)
            _raceInfoUI?.ShowGameOver();

        if (d.RequestRaceComplete)
            InGameManager.Instance?.EnterLocalRaceComplete();
        if (d.RequestGameOver)
            InGameManager.Instance?.EnterLocalGameOver();

        if (d.StartFinishWindowCoroutine)
            _finishWindowRoutine = StartCoroutine(FinishWindowRoutine());
    }

    private IEnumerator FinishWindowRoutine()
    {
        while (InGameManager.Instance != null && InGameManager.Instance.CurrentState == GameState.Playing)
        {
            yield return new WaitForSeconds(1f);

            bool playing = InGameManager.Instance != null && InGameManager.Instance.CurrentState == GameState.Playing;
            var d = _rules.OnFinishWindowSecondElapsed(playing);
            Apply(d);

            if (d.RequestGameOver || d.RequestRaceComplete)
                yield break;
            if (!playing)
                yield break;
            if (!_rules.IsFinishWindowCounting)
                yield break;
        }
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
