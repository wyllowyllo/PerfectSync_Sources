using System.Collections.Generic;

public sealed class RaceRankingComputationResult
{
    public List<TeamRankEntry> Rankings;
    public List<int> FinishOrder;
    public List<RaceFinishEvent> NewFinishes;
}
