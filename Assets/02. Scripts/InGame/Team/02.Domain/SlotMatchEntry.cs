using System;
using UnityEngine;

namespace InGame.Team
{
    /// <summary>
    /// 등수별 슬롯 3매치 확률 항목.
    /// </summary>
    [Serializable]
    public struct SlotMatchEntry
    {
        [Tooltip("순위 (1 = 1등)")]
        public int Rank;

        [Range(0f, 1f)]
        [Tooltip("3매치 확률 (0 ~ 1)")]
        public float MatchProbability;
    }
}
