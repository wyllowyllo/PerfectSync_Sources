using System;
using System.Collections.Generic;
using InGame.Player;
using InGame.Player.Network;
using UnityEngine;

namespace InGame.Team
{
    /// <summary>
    /// 팀 모드 전환 매니저.
    /// 트램펄린 트리거 → 등수 기반 확률 판정 → 합체 모드 전환 플로우를 관장한다.
    ///
    /// <para><b>사용법</b></para>
    /// <list type="number">
    ///   <item>씬에 배치한다.</item>
    ///   <item>트램펄린 스크립트에서 <see cref="HandleTrampolineTrigger"/>를 호출한다.</item>
    ///   <item>슬롯머신 UI는 <see cref="OnSlotTriggered"/>, <see cref="OnSlotResult"/> 이벤트를 구독한다.</item>
    /// </list>
    /// </summary>
    public class TeamModeManager : SingletonMonoBehaviour<TeamModeManager>
    {
        protected override bool PersistAcrossScenes => false;

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

        // ── Events ─────────────────────────────────────────────

        /// <summary>슬롯머신 연출 시작 시 발생. (teamNumber, rank, matchProbability)</summary>
        public event Action<int, int, float> OnSlotTriggered;

        /// <summary>슬롯머신 결과 확정 시 발생. (teamNumber, isMatch)</summary>
        public event Action<int, bool> OnSlotResult;

        // ── Public API ─────────────────────────────────────────

        /// <summary>
        /// 트램펄린 트리거 시 호출한다.
        /// 해당 팀의 Host 클라이언트에서만 판정을 실행한다.
        /// 분리 모드가 아니거나 쿨다운 중이면 무시한다.
        /// </summary>
        public void HandleTrampolineTrigger(int teamNumber)
        {
            if (InGameManager.Instance != null && !InGameManager.IsLocalPlayerControllable)
                return;

            if (!InGameManager.IsHostOfTeam(teamNumber))
                return;

            var synchronizer = FindTeamSynchronizer(teamNumber);
            if (synchronizer == null) return;

            var formController = synchronizer.GetComponent<PlayerFormController>();
            if (formController == null || formController.CurrentMode != ETeamMode.Separated)
                return;

            // 분리 모드 두 캐릭터 중복 트리거 방지
            if (!TryConsumeTrigger(teamNumber))
                return;

            int rank = GetTeamRank(teamNumber);
            float probability = GetMatchProbability(rank);

            OnSlotTriggered?.Invoke(teamNumber, rank, probability);

            bool isMatch = UnityEngine.Random.value < probability;
            OnSlotResult?.Invoke(teamNumber, isMatch);

            if (isMatch)
                synchronizer.RequestSwitch();
        }

        /// <summary>팀의 현재 순위를 조회한다. 순위 정보가 없으면 최하위를 반환한다.</summary>
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

        /// <summary>등수에 해당하는 3매치 확률을 반환한다.</summary>
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
