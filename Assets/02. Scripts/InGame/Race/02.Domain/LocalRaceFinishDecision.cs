public struct LocalRaceFinishDecision
{
    public static LocalRaceFinishDecision None => default;

    public bool StopFinishWindowCoroutine { get; set; }
    public bool StartFinishWindowCoroutine { get; set; }

    public bool ShowWinner { get; set; }
    public bool ShowFinish { get; set; }
    public bool ShowGameOver { get; set; }

    public int DisplayFinishWindowSeconds { get; set; }

    public bool RequestRaceComplete { get; set; }
    public bool RequestGameOver { get; set; }
}
