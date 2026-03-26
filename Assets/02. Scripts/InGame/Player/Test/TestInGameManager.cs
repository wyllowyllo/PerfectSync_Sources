using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace InGame.Player.Test
{
    /// <summary>
    /// 테스트 씬 전용 인게임 세션 매니저.
    /// InGameManager 패턴을 따르며, 기존 Core 매칭 인프라
    /// (PhotonServerManager, PhotonRoomManager, PhotonTeamManager)가
    /// 접속·방·팀 배정을 완료한 상태에서 인게임 흐름만 관리한다.
    ///
    /// 흐름: 씬 로드 → 프로퍼티 초기화 → 방 닫기 → 팀 대기
    ///       → Host: TeamCharacter 스폰 / 모두: Ready 신호
    ///       → 전원 Ready → Intro → Countdown → Playing
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class TestInGameManager : SingletonPunCallbacks<TestInGameManager>
    {
        private const int DefaultCountdownSeconds = 3;
        private const float DefaultIntroDuration = 2f;

        protected override bool PersistAcrossScenes => false;

        [Header("Settings")]
        [SerializeField] private float _introDuration = DefaultIntroDuration;
        [SerializeField] private int _countdownSeconds = DefaultCountdownSeconds;

        [Header("References")]
        [SerializeField] private TeamCharacterSpawner _teamCharacterSpawner;

        public GameState CurrentState { get; private set; } = GameState.Loading;
        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnRaceCountdownTick;

        private GameObject _spawnedCharacter;
        private WaitForSeconds _waitIntro;

        // ── Lifecycle ───────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _waitIntro = new WaitForSeconds(_introDuration);
            InGameLocalPlayerPropertyReset.ApplyForLobbyScene(clearTeamBecauseNotInRoom: false);
            CloseRoomToNewJoiners();
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() =>
                PhotonNetwork.InRoom &&
                PhotonTeamManager.GetLocalTeamRaw() != PhotonTeamManager.TeamNone
            );

            CloseRoomToNewJoiners();

            int myTeam = PhotonTeamManager.GetLocalTeamRaw();

            // Host만 TeamCharacter 스폰
            if (IsHostOfTeam(myTeam))
                _spawnedCharacter = _teamCharacterSpawner.SpawnByTeam(myTeam);

            // Host, Guest 모두 Ready 신호
            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable { { InGameRaceKeys.ReadyKey, true } });
        }

        private static void CloseRoomToNewJoiners()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }

        // ── Photon Callbacks ────────────────────────────────

        public override void OnPlayerPropertiesUpdate(
            Photon.Realtime.Player targetPlayer, Hashtable changedProps)
        {
            if (!changedProps.ContainsKey(InGameRaceKeys.ReadyKey)) return;
            if (CurrentState != GameState.Loading) return;

            if (AreAllPlayersReady() && PhotonNetwork.IsMasterClient)
                photonView.RPC(nameof(RPC_StartIntro), RpcTarget.All);
        }

        // ── Game Flow ───────────────────────────────────────

        [PunRPC]
        private void RPC_StartIntro()
        {
            StartCoroutine(GameFlowRoutine());
        }

        private IEnumerator GameFlowRoutine()
        {
            SetState(GameState.Intro);
            yield return _waitIntro;

            SetState(GameState.Countdown);
            for (int i = _countdownSeconds; i > 0; i--)
            {
                OnRaceCountdownTick?.Invoke(i);
                yield return CoroutineWaitCache.OneSecond;
            }

            SetState(GameState.Playing);
        }

        private void SetState(GameState newState)
        {
            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);
        }

        private bool AreAllPlayersReady()
        {
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!player.CustomProperties.TryGetValue(
                        InGameRaceKeys.ReadyKey, out object readyObj) || !(bool)readyObj)
                    return false;
            }
            return PhotonNetwork.PlayerList.Length > 0;
        }

        // ── Team Utility ────────────────────────────────────

        public bool IsHostOfMyTeam()
        {
            int myTeam = PhotonTeamManager.GetLocalTeamRaw();
            if (myTeam == PhotonTeamManager.TeamNone) return false;
            return IsHostOfTeam(myTeam);
        }

        public static bool IsHostOfTeam(int teamNumber)
        {
            if (PhotonTeamManager.Instance == null) return false;

            var members = PhotonTeamManager.Instance.GetTeamMembers(teamNumber);
            if (members.Count == 0) return false;

            int minActor = int.MaxValue;
            foreach (var member in members)
            {
                if (member.ActorNumber < minActor)
                    minActor = member.ActorNumber;
            }

            return PhotonNetwork.LocalPlayer.ActorNumber == minActor;
        }

        public static int GetGuestActorNumber(int teamNumber)
        {
            if (PhotonTeamManager.Instance == null) return -1;

            var members = PhotonTeamManager.Instance.GetTeamMembers(teamNumber);
            if (members.Count < 2) return -1;

            int minActor = int.MaxValue;
            foreach (var member in members)
            {
                if (member.ActorNumber < minActor)
                    minActor = member.ActorNumber;
            }

            foreach (var member in members)
            {
                if (member.ActorNumber != minActor)
                    return member.ActorNumber;
            }

            return -1;
        }
    }
}
