using Core;
using InGame.Gimmick;
using InGame.Player.Ragdoll;
using InGame.Team;
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

        private MergedBodyController mergedBodyController;
        private LocalPlayerInput _localPlayerInput;
        private PhotonView _photonView;

        private void Start()
        {
            mergedBodyController = GetComponent<MergedBodyController>();
            _localPlayerInput = GetComponent<LocalPlayerInput>();
            _photonView = GetComponent<PhotonView>();

            _localPlayerInput.OnHitReceived += HandleHit;
            _localPlayerInput.OnDeathReceived += HandleDeath;
            _localPlayerInput.OnRespawnReceived += HandleRespawn;

            if (_photonView != null && _photonView.IsMine)
                WireHitDetector(_mergedBody);

            ConfigureMergedMode();
        }

        private void OnDestroy()
        {
            if (_localPlayerInput != null)
            {
                _localPlayerInput.OnHitReceived -= HandleHit;
                _localPlayerInput.OnDeathReceived -= HandleDeath;
                _localPlayerInput.OnRespawnReceived -= HandleRespawn;
            }
        }

        private void ConfigureMergedMode()
        {
            bool isHost = _photonView != null && _photonView.IsMine;

            SetRemoteOnBody(_mergedBody, !isHost);
            FindInBody<BodyMovementSynchronizer>(_mergedBody)?.SetSyncEnabled(true);
            ConfigureRagdollAuthority(_mergedBody, true, isHost);
            ConfigureInvincible(isHost);
        }

        private void ConfigureInvincible(bool isHost)
        {
            var invincibleController = GetComponent<InvincibleModeController>();
            if (invincibleController != null)
                invincibleController.SetAuthority(isHost);

            var hitDetector = FindInBody<HitDetector>(_mergedBody);
            if (hitDetector != null)
            {
                if (invincibleController != null)
                    hitDetector.AddInvincibilitySource(invincibleController);

                var respawnHandler = GetComponent<RespawnHandler>();
                if (respawnHandler != null)
                    hitDetector.AddInvincibilitySource(respawnHandler);
            }

            var contactDetector = FindInBody<InvincibleContactDetector>(_mergedBody);
            if (contactDetector != null)
            {
                contactDetector.SetAuthority(isHost);
                var synchronizer = GetComponent<TeamModeSynchronizer>();
                contactDetector.Initialize(invincibleController, synchronizer);
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
            if (!MatchesViewID(_mergedBody, hitViewID)) return;

            var stateMachine = FindInBody<RagdollStateMachine>(_mergedBody);
            if (stateMachine != null)
                stateMachine.ApplyHit(hit);
        }

        private void HandleDeath()
        {
            FindInBody<RagdollStateMachine>(_mergedBody)?.EnterDead();
        }

        private void HandleRespawn()
        {
            // Dead(사망 리스폰) / 그 외(F1 긴급 복구) 모두 대응.
            FindInBody<RagdollStateMachine>(_mergedBody)?.TriggerRecovery();
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
