using Core;
using InGame.Player.Ragdoll;
using InGame.Team._02._Domain;
using InGame.UserInput;
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

        private void HandleImpact(Vector3 impulse, Vector3 hitPoint)
        {
            var ragdoll = GetActiveRagdollController();
            ragdoll?.OnHitImpact(impulse, hitPoint);
        }

        private void HandleDeath()
        {
            var ragdoll = GetActiveRagdollController();
            ragdoll?.EnterDead();
        }

        private RagdollController GetActiveRagdollController()
        {
            switch (_currentMode)
            {
                case ETeamMode.Merged:
                    return _mergedBody != null ? _mergedBody.GetComponent<RagdollController>() : null;
                case ETeamMode.Separated:
                    return _avatarA != null ? _avatarA.GetComponent<RagdollController>() : null;
                default:
                    return null;
            }
        }
    }
}
