using Core;
using InGame.Player.Ragdoll;
using InGame.Team;
using InGame.UserInput;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.BodyStateCoordinator)]
    public class BodyStateCoordinator : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        private PlayerFormController _playerFormController;
        private LocalPlayerInput _localPlayerInput;
        private PhotonView _photonView;
        private ETeamMode _currentMode;

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
            _localPlayerInput = GetComponent<LocalPlayerInput>();
            _photonView = GetComponent<PhotonView>();

            _playerFormController.OnModeChanged += HandleModeChanged;
            _localPlayerInput.OnHitReceived += HandleHit;
            _localPlayerInput.OnDeathReceived += HandleDeath;

            if (_photonView != null && _photonView.IsMine)
            {
                WireHitDetector(_mergedBody);
                WireHitDetector(_avatarA);
                WireHitDetector(_avatarB);
            }
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_localPlayerInput != null)
            {
                _localPlayerInput.OnHitReceived -= HandleHit;
                _localPlayerInput.OnDeathReceived -= HandleDeath;
            }
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            _currentMode = newMode;
            RefreshBodyMode(newMode);
        }

        private void RefreshBodyMode(ETeamMode mode)
        {
            bool isMerged = mode == ETeamMode.Merged;
            bool isHost = _photonView != null && _photonView.IsMine;

            // Merged + Host → local (물리 시뮬), Merged + Guest → remote (kinematic).
            SetRemoteOnBody(_mergedBody, !isMerged || !isHost);

            SetRemoteOnBody(_avatarA, isMerged);
            SetRemoteOnBody(_avatarB, isMerged);

            // Position sync.
            FindInBody<BodyMovementSynchronizer>(_mergedBody)?.SetSyncEnabled(isMerged);
            FindInBody<BodyMovementSynchronizer>(_avatarA)?.SetSyncEnabled(false);
            FindInBody<BodyMovementSynchronizer>(_avatarB)?.SetSyncEnabled(false);

            // Ragdoll bone sync + authority 설정.
            if (isMerged)
            {
                ConfigureRagdollAuthority(_mergedBody, true, isHost);
                ConfigureRagdollAuthority(_avatarA, false, false);
                ConfigureRagdollAuthority(_avatarB, false, false);

                // 합체 복귀: AvatarB 소유권을 호스트에게 반환.
                if (isHost)
                    TransferBodyOwnership(_avatarB, PhotonNetwork.LocalPlayer);
            }
            else
            {
                ConfigureRagdollAuthority(_mergedBody, false, false);
                // 분리 모드: 호스트가 AvatarA, 게스트가 AvatarB 제어.
                ConfigureRagdollAuthority(_avatarA, true, isHost);
                ConfigureRagdollAuthority(_avatarB, true, !isHost);

                // 분리 진입: AvatarB 소유권을 게스트에게 이전.
                if (isHost)
                {
                    Photon.Realtime.Player teammate = FindTeammate();
                    if (teammate != null)
                        TransferBodyOwnership(_avatarB, teammate);
                }
            }
        }

        private void ConfigureRagdollAuthority(
            GameObject body, bool syncEnabled, bool isAuthority)
        {
            if (body == null) return;

            var boneSynchronizer = FindInBody<RagdollBoneSynchronizer>(body);
            if (boneSynchronizer != null)
            {
                boneSynchronizer.SetSyncEnabled(syncEnabled);
                boneSynchronizer.SetAuthority(isAuthority);
            }

            var bridge = FindInBody<RagdollStateNetworkBridge>(body);
            if (bridge != null)
                bridge.SetAuthority(isAuthority);

            var stateMachine = FindInBody<RagdollStateMachine>(body);
            if (stateMachine != null)
                stateMachine.SetAuthority(isAuthority);

            var hitDetector = FindInBody<HitDetector>(body);
            if (hitDetector != null)
                hitDetector.SetAuthority(isAuthority);
        }

        private void SetRemoteOnBody(GameObject body, bool isRemote)
        {
            if (body == null) return;
            var toggle = FindInBody<BodySimulationToggle>(body);
            if (toggle != null)
                toggle.SetRemote(isRemote);
        }

        private void WireHitDetector(GameObject body)
        {
            if (body == null || _localPlayerInput == null) return;

            var detector = FindInBody<HitDetector>(body);
            if (detector == null) return;

            var pv = body.GetComponentInChildren<PhotonView>();
            if (pv == null) return;

            int viewID = pv.ViewID;
            detector.OnHitDetected += (hit) =>
                _localPlayerInput.SendHit(hit, viewID);
        }

        private void HandleHit(HitData hit, int hitViewID)
        {
            var hitBody = ResolveBodyByViewID(hitViewID);
            if (hitBody == null) return;

            // Authority만 hit 처리. Remote는 RagdollStateNetworkBridge RPC로 제어.
            var stateMachine = FindInBody<RagdollStateMachine>(hitBody);
            if (stateMachine != null)
                stateMachine.ApplyHit(hit);
        }

        private void HandleDeath()
        {
            switch (_currentMode)
            {
                case ETeamMode.Merged:
                    FindInBody<RagdollStateMachine>(_mergedBody)?.EnterDead();
                    break;
                case ETeamMode.Separated:
                    FindInBody<RagdollStateMachine>(_avatarA)?.EnterDead();
                    FindInBody<RagdollStateMachine>(_avatarB)?.EnterDead();
                    break;
            }
        }

        private GameObject ResolveBodyByViewID(int viewID)
        {
            if (MatchesViewID(_mergedBody, viewID)) return _mergedBody;
            if (MatchesViewID(_avatarA, viewID)) return _avatarA;
            if (MatchesViewID(_avatarB, viewID)) return _avatarB;
            return null;
        }

        private static bool MatchesViewID(GameObject body, int viewID)
        {
            if (body == null) return false;
            var pv = body.GetComponentInChildren<PhotonView>();
            return pv != null && pv.ViewID == viewID;
        }

        private Photon.Realtime.Player FindTeammate()
        {
            if (PhotonTeamManager.Instance == null) return null;

            int myTeam = PhotonTeamManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            var members = PhotonTeamManager.Instance.GetTeamMembers(myTeam);

            foreach (var member in members)
            {
                if (member.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                    return member;
            }

            return null;
        }

        private static void TransferBodyOwnership(GameObject body, Photon.Realtime.Player newOwner)
        {
            if (body == null || newOwner == null) return;

            var pv = body.GetComponentInChildren<PhotonView>();
            if (pv != null && pv.Owner != newOwner)
                pv.TransferOwnership(newOwner);
        }

        private static T FindInBody<T>(GameObject body) where T : Component
        {
            if (body == null) return null;
            return body.GetComponentInChildren<T>();
        }
    }
}
