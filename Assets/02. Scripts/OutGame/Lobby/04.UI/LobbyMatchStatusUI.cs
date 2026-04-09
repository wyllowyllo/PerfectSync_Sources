using System.Collections;
using TMPro;
using UnityEngine;

public class LobbyMatchStatusUI : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _statusText;

    [Header("Timing")]
    [Tooltip("LobbyManager가 없을 때만 사용합니다. 있으면 LobbyManager.SecondsBeforeSendingReady와 동일하게 맞춥니다.")]
    [SerializeField] [Min(0f)] private float _minimumLookingDurationSeconds = 3f;
    [SerializeField] [Min(0f)] private float _gameFoundDisplaySeconds = 2f;
    [SerializeField] [Min(1f)] private float _preLeaveCountdownSeconds = 3f;
    [SerializeField] [Min(0.05f)] private float _dotAnimationStepSeconds = 0.35f;

    [Header("Copy")]
    [SerializeField] private string _lookingForPlayersBase = "Looking for Players";
    [SerializeField] private string _gameFoundLine = "Game Found!";
    [SerializeField] private string _playersFormat = "Players {0}/8";
    [SerializeField] private string _gameStartingFormat = "게임이 시작합니다.\n{0}";

    private LobbyManager _lobby;
    private GameMatchTransitionHandler _transitionHandler;
    private Coroutine _displayRoutine;
    private bool _wasInBatch;
    private float _gameFoundEndTime;
    private bool _gameFoundSequenceDone;
    private float _matchScreenStartedAt;
    private bool _pendingFinalCountdown;

    private void Awake()
    {
        if (_root == null)
            _root = gameObject;
        SetRootVisible(false);
    }

    private void Start()
    {
        _lobby = LobbyManager.Instance;
        if (_lobby != null)
        {
            _lobby.ShowMatchingScreenRequested += OnShowMatchingScreen;
            _lobby.ShowMainScreenRequested += OnShowMainScreen;
        }

        _transitionHandler = GameMatchTransitionHandler.Instance;
        if (_transitionHandler != null)
            _transitionHandler.OnMatchConfirmedPendingLeave += OnMatchConfirmedPendingLeave;
    }

    private void OnDisable()
    {
        if (_lobby != null)
        {
            _lobby.ShowMatchingScreenRequested -= OnShowMatchingScreen;
            _lobby.ShowMainScreenRequested -= OnShowMainScreen;
        }

        if (_transitionHandler != null)
            _transitionHandler.OnMatchConfirmedPendingLeave -= OnMatchConfirmedPendingLeave;

        StopAllCoroutines();
        _displayRoutine = null;
    }

    private void OnShowMatchingScreen()
    {
        ResetMatchUiState();
        SetRootVisible(true);
        if (_displayRoutine != null)
            StopCoroutine(_displayRoutine);
        _displayRoutine = StartCoroutine(MatchDisplayRoutine());
    }

    private void OnShowMainScreen()
    {
        StopAllCoroutines();
        _displayRoutine = null;
        _pendingFinalCountdown = false;
        SetRootVisible(false);
    }

    private void ResetMatchUiState()
    {
        _wasInBatch = false;
        _gameFoundEndTime = -1f;
        _gameFoundSequenceDone = false;
        _matchScreenStartedAt = Time.time;
        _pendingFinalCountdown = false;
    }

    private void OnMatchConfirmedPendingLeave(string _)
    {
        _pendingFinalCountdown = true;
        if (_displayRoutine == null && isActiveAndEnabled)
        {
            SetRootVisible(true);
            _displayRoutine = StartCoroutine(MatchDisplayRoutine());
        }
    }

    private IEnumerator MatchDisplayRoutine()
    {
        while (enabled)
        {
            if (_pendingFinalCountdown)
            {
                yield return FinalCountdownRoutine();
                yield break;
            }

            bool minLookDone = Time.time - _matchScreenStartedAt >= GetMinimumLookingDurationSeconds();

            if (!minLookDone)
            {
                SetLookingWithDots();
                yield return null;
                continue;
            }

            if (!MatchQueueBatchPreview.TryGetFirstBatchInfo(out var info) || !info.LocalPlayerInBatch)
            {
                if (_wasInBatch)
                {
                    _gameFoundEndTime = -1f;
                    _gameFoundSequenceDone = false;
                }

                _wasInBatch = false;
                SetLookingWithDots();
                yield return null;
                continue;
            }

            if (!_wasInBatch)
            {
                _gameFoundEndTime = -1f;
                _gameFoundSequenceDone = false;
            }

            _wasInBatch = true;

            if (!_gameFoundSequenceDone)
            {
                if (_gameFoundEndTime < 0f)
                    _gameFoundEndTime = Time.time + _gameFoundDisplaySeconds;

                if (Time.time < _gameFoundEndTime)
                {
                    SetStaticLine(_gameFoundLine);
                    yield return null;
                    continue;
                }

                _gameFoundSequenceDone = true;
            }

            int shown = Mathf.Clamp(info.FilledSlots, 0, 8);
            SetStaticLine(string.Format(_playersFormat, shown));
            yield return null;
        }
    }

    private IEnumerator FinalCountdownRoutine()
    {
        int seconds = Mathf.Max(1, Mathf.CeilToInt(_preLeaveCountdownSeconds));

        for (int i = seconds; i > 0; i--)
        {
            SetStaticLine(string.Format(_gameStartingFormat, i));
            yield return CoroutineWaitCache.OneSecond;
        }

        SetStaticLine(string.Format(_gameStartingFormat, 0));
        yield return null;

        if (_transitionHandler != null)
            _transitionHandler.CompletePendingMatchTransition();

        SetRootVisible(false);
        _displayRoutine = null;
    }

    private float GetMinimumLookingDurationSeconds()
    {
        if (_lobby != null)
            return _lobby.SecondsBeforeSendingReady;
        return _minimumLookingDurationSeconds;
    }

    private void SetLookingWithDots()
    {
        int step = Mathf.FloorToInt(Time.time / _dotAnimationStepSeconds) % 3;
        string dots = step == 0 ? "." : step == 1 ? ".." : "...";
        SetStaticLine(_lookingForPlayersBase + dots);
    }

    private void SetStaticLine(string line)
    {
        if (_statusText != null)
            _statusText.text = line;
    }

    private void SetRootVisible(bool visible)
    {
        if (_root != null)
            _root.SetActive(visible);
    }
}
