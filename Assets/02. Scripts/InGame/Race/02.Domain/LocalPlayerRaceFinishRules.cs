public sealed class LocalPlayerRaceFinishRules
{
    private readonly int _finishWindowDurationSeconds;

    private bool _outcomeSealed;
    private bool _finishWindowCounting;
    private int _finishWindowRemainingDisplayed;

    public LocalPlayerRaceFinishRules(int finishWindowDurationSeconds)
    {
        _finishWindowDurationSeconds = finishWindowDurationSeconds;
    }

    public bool IsFinishWindowCounting => _finishWindowCounting;

    public LocalRaceFinishDecision OnFirstPlaceFinished(
        int firstPlaceTeamNumber,
        int localTeamNumber,
        int teamNoneValue,
        bool sessionIsPlaying)
    {
        if (localTeamNumber == teamNoneValue)
            return LocalRaceFinishDecision.None;

        if (firstPlaceTeamNumber == localTeamNumber)
        {
            _finishWindowCounting = false;
            _outcomeSealed = true;
            return new LocalRaceFinishDecision
            {
                StopFinishWindowCoroutine = true,
                ShowWinner = true,
                RequestRaceComplete = true
            };
        }

        if (!sessionIsPlaying || _outcomeSealed)
            return LocalRaceFinishDecision.None;

        _finishWindowCounting = true;
        _finishWindowRemainingDisplayed = _finishWindowDurationSeconds;

        return new LocalRaceFinishDecision
        {
            StopFinishWindowCoroutine = true,
            DisplayFinishWindowSeconds = _finishWindowRemainingDisplayed,
            StartFinishWindowCoroutine = true
        };
    }

    public LocalRaceFinishDecision OnLocalTeamFinishedWithPlace(
        int finishedTeamNumber,
        int finishPlace,
        int localTeamNumber,
        int teamNoneValue,
        bool sessionIsPlaying)
    {
        if (finishedTeamNumber != localTeamNumber || localTeamNumber == teamNoneValue)
            return LocalRaceFinishDecision.None;
        if (finishPlace == 1)
            return LocalRaceFinishDecision.None;
        if (!sessionIsPlaying || _outcomeSealed)
            return LocalRaceFinishDecision.None;

        _finishWindowCounting = false;
        _outcomeSealed = true;

        return new LocalRaceFinishDecision
        {
            StopFinishWindowCoroutine = true,
            ShowFinish = true,
            RequestRaceComplete = true
        };
    }

    public LocalRaceFinishDecision OnFinishWindowSecondElapsed(bool sessionIsPlaying)
    {
        if (!_finishWindowCounting || _outcomeSealed)
            return LocalRaceFinishDecision.None;

        if (!sessionIsPlaying)
        {
            _finishWindowCounting = false;
            return new LocalRaceFinishDecision { StopFinishWindowCoroutine = true };
        }

        _finishWindowRemainingDisplayed--;
        if (_finishWindowRemainingDisplayed > 0)
        {
            return new LocalRaceFinishDecision
            {
                DisplayFinishWindowSeconds = _finishWindowRemainingDisplayed
            };
        }

        _finishWindowCounting = false;
        _outcomeSealed = true;

        return new LocalRaceFinishDecision
        {
            ShowGameOver = true,
            RequestGameOver = true
        };
    }
}
