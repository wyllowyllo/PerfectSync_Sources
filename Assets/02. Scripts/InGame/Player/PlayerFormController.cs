using Core.Utilities;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerFormController : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;

        private IControllableBody _mergedControllable;

        public Transform PrimaryBodyTransform => _mergedControllable.BodyTransform;

        private void Awake()
        {
            _mergedControllable = _mergedBody.GetComponentInChildren<IControllableBody>();
        }

        public void Initialize()
        {
            _mergedBody.SetActive(true);
        }

        public void ApplyInput(Vector3 worldDirA, Vector3 worldDirB, bool jumpA, bool jumpB)
        {
            Vector3 combined = DualInputCombiner.Combine(worldDirA, worldDirB);
            _mergedControllable.ApplyInput(combined, jumpA || jumpB);
        }

        public void LaunchAllBodies(Vector3 targetPosition)
        {
            LaunchBody(_mergedBody, targetPosition);
        }

        public bool IsAnyRagdollActive() => _mergedControllable.IsRagdollActive;

        public void ForceRecoverIfNeeded()
        {
            if (IsAnyRagdollActive())
                ForceRecoverBody(_mergedBody);
        }

        private void LaunchBody(GameObject body, Vector3 targetPosition)
        {
            if (body == null || !body.activeInHierarchy) return;

            var launchable = body.GetComponentInChildren<ILaunchable>();
            if (launchable != null && !launchable.IsLaunching)
                launchable.Launch(targetPosition);
        }

        private void ForceRecoverBody(GameObject body)
        {
            if (body == null) return;
            var ragdoll = body.GetComponentInChildren<RagdollStateMachine>();
            if (ragdoll != null && ragdoll.IsRagdollActive)
                ragdoll.ForceRecover();
        }
    }
}
