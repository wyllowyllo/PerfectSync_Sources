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
        private LocalPlayerInput localPlayerInput;
        private ETeamMode _currentMode;

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
            localPlayerInput = GetComponent<LocalPlayerInput>();

            _playerFormController.OnModeChanged += HandleModeChanged;
            localPlayerInput.OnImpactReceived += HandleImpact;
            localPlayerInput.OnDeathReceived += HandleDeath;
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (localPlayerInput != null)
            {
                localPlayerInput.OnImpactReceived -= HandleImpact;
                localPlayerInput.OnDeathReceived -= HandleDeath;
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

            SetRemoteOnBody(_mergedBody, !isMerged);
            SetRemoteOnBody(_avatarA, isMerged);
            SetRemoteOnBody(_avatarB, isMerged);

            _mergedBody?.GetComponent<BodyPositionSynchronizer>()?.SetSyncEnabled(isMerged);
            _avatarA?.GetComponent<BodyPositionSynchronizer>()?.SetSyncEnabled(!isMerged);
            _avatarB?.GetComponent<BodyPositionSynchronizer>()?.SetSyncEnabled(!isMerged);
        }

        private void SetRemoteOnBody(GameObject body, bool isRemote)
        {
            if (body == null) return;
            var controller = body.GetComponent<BodySimulationToggle>();
            if (controller != null)
                controller.SetRemote(isRemote);
        }

        private void HandleImpact(Vector3 impulse, Vector3 hitPoint, int hitViewID)
        {
            var hitBody = ResolveBodyByViewID(hitViewID);
            if (hitBody == null) return;
            hitBody.GetComponent<RagdollController>()?.OnHitImpact(impulse, hitPoint);
        }

        private void HandleDeath()
        {
            switch (_currentMode)
            {
                case ETeamMode.Merged:
                    _mergedBody?.GetComponent<RagdollController>()?.EnterDead();
                    break;
                case ETeamMode.Separated:
                    _avatarA?.GetComponent<RagdollController>()?.EnterDead();
                    _avatarB?.GetComponent<RagdollController>()?.EnterDead();
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
            var pv = body.GetComponent<PhotonView>();
            return pv != null && pv.ViewID == viewID;
        }
    }
}
