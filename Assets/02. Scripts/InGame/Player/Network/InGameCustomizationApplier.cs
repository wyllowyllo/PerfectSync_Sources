using System.Collections.Generic;
using InGame.UserInput;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace InGame.Player.Network
{
    /// <summary>
    /// TeamCharacter 루트에 부착. 팀 아바타의 현재 소스 플레이어(=커스터마이징 주체)를
    /// 모든 클라이언트가 동일하게 유지하도록 RPC로 동기화하고, 카운트다운이 끝나기 전까지
    /// 팀원이 C 키로 소스를 토글할 수 있게 한다.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class InGameCustomizationApplier : MonoBehaviourPunCallbacks
    {
        [Tooltip("비워두면 자식에서 CustomizationPartItemsActivator를 자동으로 찾는다.")]
        [SerializeField] private CustomizationPartItemsActivator _activator;

        private LocalPlayerInput _localInput;
        private int _currentSourceActorNumber = -1;
        private bool _stateHandlerHooked;

        public int CurrentSourceActorNumber => _currentSourceActorNumber;

        /// <summary>
        /// 이 팀 캐릭터의 소속 팀 번호 (PhotonView.Owner 기준).
        /// 소유자가 없으면 PhotonTeamManager.TeamNone 반환.
        /// </summary>
        public int TeamNumber
        {
            get
            {
                Photon.Realtime.Player owner = photonView != null ? photonView.Owner : null;
                if (owner == null) return PhotonTeamManager.TeamNone;
                return PhotonTeamManager.GetTeamRaw(owner);
            }
        }
        
        public bool CanToggle =>
            InGameManager.Instance != null &&
            InGameManager.Instance.CurrentState != GameState.Playing &&
            InGameManager.Instance.CurrentState != GameState.RaceComplete &&
            InGameManager.Instance.CurrentState != GameState.GameOver;

        private void Start()
        {
            EnsureActivator();
            _localInput = GetComponent<LocalPlayerInput>();

            Photon.Realtime.Player owner = photonView != null ? photonView.Owner : null;
            int initialActor = owner != null ? owner.ActorNumber : -1;
            ApplyFromActor(initialActor);

            TryHookGameStateChanged();
        }

        private void Update()
        {
            if (!_stateHandlerHooked)
                TryHookGameStateChanged();

            if (_localInput == null || !_localInput.CustomizeTogglePressed)
                return;

            RequestToggle();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            UnhookGameStateChanged();
        }

        public bool IsLocalPlayerOnThisTeam()
        {
            Photon.Realtime.Player owner = photonView != null ? photonView.Owner : null;
            if (owner == null)
                return false;

            int ownerTeam = PhotonTeamManager.GetTeamRaw(owner);
            if (ownerTeam == PhotonTeamManager.TeamNone)
                return false;

            return PhotonTeamManager.GetLocalTeamRaw() == ownerTeam;
        }

        public void RequestToggle()
        {
            if (!CanToggle)
                return;

            if (!IsLocalPlayerOnThisTeam())
                return;

            int nextActor = ComputeNextSource();
            if (nextActor == _currentSourceActorNumber)
                return;

            photonView.RPC(nameof(RPC_SetSource), RpcTarget.All, nextActor);
        }

        [PunRPC]
        private void RPC_SetSource(int actorNumber)
        {
            if (!CanToggle)
                return;

            ApplyFromActor(actorNumber);
        }

        public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
        {
            base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

            if (targetPlayer == null || changedProps == null || changedProps.Count == 0)
                return;

            if (targetPlayer.ActorNumber != _currentSourceActorNumber)
                return;

            if (!HasCustomizationPropertyChange(changedProps))
                return;

            ApplyFromActor(_currentSourceActorNumber);
        }

        private void ApplyFromActor(int actorNumber)
        {
            EnsureActivator();
            if (_activator == null)
                return;

            _currentSourceActorNumber = actorNumber;

            Photon.Realtime.Player player = ResolvePlayer(actorNumber);
            if (player == null)
            {
                _activator.ApplyDefaultCustomization();
                return;
            }

            LobbyCustomizationPhotonApplier.ApplyFromPlayer(player, _activator);
        }

        private Photon.Realtime.Player ResolvePlayer(int actorNumber)
        {
            if (PhotonNetwork.CurrentRoom != null)
            {
                Photon.Realtime.Player direct = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
                if (direct != null)
                    return direct;
            }

            return photonView != null ? photonView.Owner : null;
        }

        private int ComputeNextSource()
        {
            Photon.Realtime.Player owner = photonView != null ? photonView.Owner : null;
            if (owner == null)
                return _currentSourceActorNumber;

            int ownerTeam = PhotonTeamManager.GetTeamRaw(owner);
            if (ownerTeam == PhotonTeamManager.TeamNone || PhotonTeamManager.Instance == null)
                return _currentSourceActorNumber;

            List<Photon.Realtime.Player> members = PhotonTeamManager.Instance.GetTeamMembers(ownerTeam);
            if (members == null || members.Count == 0)
                return _currentSourceActorNumber;

            members.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));

            int currentIdx = -1;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].ActorNumber == _currentSourceActorNumber)
                {
                    currentIdx = i;
                    break;
                }
            }

            if (currentIdx < 0)
                return members[0].ActorNumber;

            int nextIdx = (currentIdx + 1) % members.Count;
            return members[nextIdx].ActorNumber;
        }

        private void EnsureActivator()
        {
            if (_activator != null)
                return;

            _activator = GetComponentInChildren<CustomizationPartItemsActivator>(true);
            if (_activator == null)
            {
                Debug.LogWarning(
                    $"[{nameof(InGameCustomizationApplier)}] 자식에서 {nameof(CustomizationPartItemsActivator)}를 찾지 못했습니다. " +
                    $"MergedBody 하위에 액티베이터를 부착하고 엔트리를 구성하세요.",
                    this);
            }
        }

        private void TryHookGameStateChanged()
        {
            if (_stateHandlerHooked || InGameManager.Instance == null)
                return;

            InGameManager.Instance.OnGameStateChanged += HandleStateChanged;
            _stateHandlerHooked = true;
        }

        private void UnhookGameStateChanged()
        {
            if (!_stateHandlerHooked)
                return;

            if (InGameManager.Instance != null)
                InGameManager.Instance.OnGameStateChanged -= HandleStateChanged;

            _stateHandlerHooked = false;
        }

        private void HandleStateChanged(GameState newState)
        {
            // 토글 잠금은 CanToggle에서 판정하므로 여기서는 별도 작업 없음.
            // 추후 UI 연결 시 newState 전환을 사용할 훅 지점.
        }

        private static bool HasCustomizationPropertyChange(Hashtable changedProps)
        {
            foreach (CharacterCustomizationPart part in System.Enum.GetValues(typeof(CharacterCustomizationPart)))
            {
                if (changedProps.ContainsKey(CustomizationPhotonKeys.GetKey(part)))
                    return true;
            }
            return false;
        }
    }
}
