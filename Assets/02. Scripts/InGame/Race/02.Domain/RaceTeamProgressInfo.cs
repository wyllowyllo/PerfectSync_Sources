public readonly struct RaceTeamProgressInfo
{
    public int TeamNumber { get; }
    public int MaxCheckpointPassed { get; }
    public float BestProgress { get; }
    public bool HasFinishedRace { get; }

    public RaceTeamProgressInfo(int teamNumber, int maxCheckpointPassed, float bestProgress, bool hasFinishedRace)
    {
        TeamNumber = teamNumber;
        MaxCheckpointPassed = maxCheckpointPassed;
        BestProgress = bestProgress;
        HasFinishedRace = hasFinishedRace;
    }
}
