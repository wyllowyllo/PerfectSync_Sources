using System.Collections.Generic;
using InGame.Player.Movement;
using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    public class TeamModeManager : SingletonMonoBehaviour<TeamModeManager>
    {
        protected override bool PersistAcrossScenes => false;

        private const int SymbolCount = 6;

        [Header("3매치 확률 테이블 (테스트 후 확정)")]
        [SerializeField] private SlotMatchEntry[] _matchTable = new SlotMatchEntry[]
        {
            new SlotMatchEntry { Rank = 1, MatchProbability = 0.15f },
            new SlotMatchEntry { Rank = 2, MatchProbability = 0.30f },
            new SlotMatchEntry { Rank = 3, MatchProbability = 0.60f },
            new SlotMatchEntry { Rank = 4, MatchProbability = 0.90f },
        };

        [Header("Settings")]
        [Tooltip("같은 팀의 중복 트리거 방지 쿨다운 (초)")]
        [SerializeField] private float _triggerCooldown = 3f;

        private readonly Dictionary<int, float> _lastTriggerTime = new();
        private readonly Dictionary<int, TeamModeSynchronizer> _synchronizerCache = new();

        // ── Public API ─────────────────────────────────────────

        /// <summary>
        /// 트램펄린 탑승 시 호출. 슬롯 스핀 시작 + 정점 도달 대기.
        /// </summary>
        public void HandleTrampolineContact(int teamNumber)
        {
            if (InGameManager.Instance != null && !InGameManager.IsLocalPlayerControllable)
                return;

            if (!InGameManager.IsHostOfTeam(teamNumber))
                return;

            var synchronizer = FindTeamSynchronizer(teamNumber);
            if (synchronizer == null) return;

            if (!TryConsumeTrigger(teamNumber))
                return;

            // 스핀 시작 브로드캐스트 (결과 미정).
            synchronizer.BroadcastSlotSpinStart();

            // 정점 도달 시 결과 결정을 위해 LaunchController에 one-shot 구독.
            var launchController = synchronizer.GetComponentInChildren<LaunchController>();
            if (launchController != null)
            {
                void OnApex()
                {
                    launchController.OnApexReached -= OnApex;
                    HandleApexReached(teamNumber, synchronizer);
                }
                launchController.OnApexReached += OnApex;
            }
        }

        public int GetTeamRank(int teamNumber)
        {
            if (RaceRankingManager.Instance == null)
                return PhotonTeamManager.MaxTeams;

            foreach (var entry in RaceRankingManager.Instance.CurrentRankings)
            {
                if (entry.TeamNumber == teamNumber)
                    return entry.Rank;
            }

            return PhotonTeamManager.MaxTeams;
        }

        public float GetMatchProbability(int rank)
        {
            foreach (var entry in _matchTable)
            {
                if (entry.Rank == rank)
                    return entry.MatchProbability;
            }

            return _matchTable.Length > 0
                ? _matchTable[_matchTable.Length - 1].MatchProbability
                : 0.5f;
        }

        // ── Internal ───────────────────────────────────────────

        /// <summary>
        /// 정점(apex) 도달 시 호출. 결과 결정 + 브로드캐스트.
        /// </summary>
        private void HandleApexReached(int teamNumber, TeamModeSynchronizer synchronizer)
        {
            int rank = GetTeamRank(teamNumber);
            float probability = GetMatchProbability(rank);
            bool isMatch = Random.value < probability;
            int[] symbols = GenerateSymbols(isMatch);

            synchronizer.BroadcastSlotResult(symbols, isMatch);
        }

        private int[] GenerateSymbols(bool isMatch)
        {
            if (isMatch)
            {
                int symbol = Random.Range(0, SymbolCount);
                return new[] { symbol, symbol, symbol };
            }

            // 3개 모두 같지 않도록 생성.
            int s0 = Random.Range(0, SymbolCount);
            int s1 = Random.Range(0, SymbolCount);
            int s2 = Random.Range(0, SymbolCount);

            while (s0 == s1 && s1 == s2)
                s2 = Random.Range(0, SymbolCount);

            return new[] { s0, s1, s2 };
        }

        private bool TryConsumeTrigger(int teamNumber)
        {
            float now = Time.time;

            if (_lastTriggerTime.TryGetValue(teamNumber, out float lastTime)
                && now - lastTime < _triggerCooldown)
                return false;

            _lastTriggerTime[teamNumber] = now;
            return true;
        }

        private TeamModeSynchronizer FindTeamSynchronizer(int teamNumber)
        {
            if (_synchronizerCache.TryGetValue(teamNumber, out var cached) && cached != null)
                return cached;

            foreach (var sync in FindObjectsByType<TeamModeSynchronizer>(FindObjectsSortMode.None))
            {
                var owner = sync.photonView.Owner;
                if (owner != null && PhotonTeamManager.GetTeamRaw(owner) == teamNumber)
                {
                    _synchronizerCache[teamNumber] = sync;
                    return sync;
                }
            }

            return null;
        }
    }
}
