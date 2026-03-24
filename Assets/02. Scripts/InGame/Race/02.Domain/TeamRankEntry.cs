using System;

[Serializable]
public struct TeamRankEntry
{
    public int TeamNumber;
    /// <summary>순위 정렬에 쓰인 당시 실시간 진행도(코스 시작~가장 가까운 스플라인 위 투영). 이름만 레거시.</summary>
    public float BestProgress;
    public int Rank;
}
