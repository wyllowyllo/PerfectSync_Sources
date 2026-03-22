using Core;
using InGame.Player.Ragdoll;
using InGame.Team._02._Domain;
using InGame.UserInput;
using Photon.Pun;
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
            _localPlayerInput.OnImpactReceived += HandleImpact;
            _localPlayerInput.OnDeathReceived += HandleDeath;
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_localPlayerInput != null)
            {
                _localPlayerInput.OnImpactReceived -= HandleImpact;
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

            FindInBody<BodyPositionSynchronizer>(_mergedBody)?.SetSyncEnabled(isMerged);
            FindInBody<BodyPositionSynchronizer>(_avatarA)?.SetSyncEnabled(!isMerged);
            FindInBody<BodyPositionSynchronizer>(_avatarB)?.SetSyncEnabled(!isMerged);

            FindInBody<RagdollBoneSynchronizer>(_mergedBody)?.SetSyncEnabled(isMerged);
            FindInBody<RagdollBoneSynchronizer>(_avatarA)?.SetSyncEnabled(!isMerged);
            FindInBody<RagdollBoneSynchronizer>(_avatarB)?.SetSyncEnabled(!isMerged);
        }

        private void SetRemoteOnBody(GameObject body, bool isRemote)
        {
            if (body == null) return;
            var toggle = FindInBody<BodySimulationToggle>(body);
            if (toggle != null)
                toggle.SetRemote(isRemote);
        }

        private void HandleImpact(Vector3 impulse, Vector3 hitPoint, int hitViewID, Vector3 torqueVector)
        {
            var hitBody = ResolveBodyByViewID(hitViewID);
            if (hitBody == null) return;
            FindInBody<RagdollStateMachine>(hitBody)?.OnHitImpact(impulse, hitPoint, torqueVector);
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

        private static T FindInBody<T>(GameObject body) where T : Component
        {
            if (body == null) return null;
            return body.GetComponentInChildren<T>();
        }
    }
}
