using InGame.Audio;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerSoundTrigger : MonoBehaviour
    {
        [Header("Spatial SFX Profiles")]
        [SerializeField] private SpatialSfxProfile _jumpProfile;
        [SerializeField] private SpatialSfxProfile _diveProfile;
        [SerializeField] private SpatialSfxProfile _launchProfile;
        [SerializeField] private SpatialSfxProfile _hitProfile;
        [SerializeField] private SpatialSfxProfile _stumbleProfile;
        [SerializeField] private SpatialSfxProfile _ragdollProfile;

        private PlayerMovement _movement;
        private LaunchController _launchController;
        private RagdollStateMachine _ragdollStateMachine;
        private HitDetector _hitDetector;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _launchController = GetComponent<LaunchController>();
            _ragdollStateMachine = GetComponent<RagdollStateMachine>();
            _hitDetector = GetComponentInChildren<HitDetector>();
        }

        private void OnEnable()
        {
            if (_movement != null)
            {
                _movement.OnJumped += HandleJumped;
                _movement.OnDived += HandleDived;
            }

            if (_launchController != null)
                _launchController.OnLaunched += HandleLaunched;

            if (_ragdollStateMachine != null)
            {
                _ragdollStateMachine.OnStateChanged += HandleRagdollStateChanged;
                _ragdollStateMachine.OnStumblePlayed += HandleStumble;
            }

            if (_hitDetector != null)
                _hitDetector.OnHitDetected += HandleHit;
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.OnJumped -= HandleJumped;
                _movement.OnDived -= HandleDived;
            }

            if (_launchController != null)
                _launchController.OnLaunched -= HandleLaunched;

            if (_ragdollStateMachine != null)
            {
                _ragdollStateMachine.OnStateChanged -= HandleRagdollStateChanged;
                _ragdollStateMachine.OnStumblePlayed -= HandleStumble;
            }

            if (_hitDetector != null)
                _hitDetector.OnHitDetected -= HandleHit;
        }

        private void HandleJumped()
        {
            InGameSfxManager.Instance?.EmitSpatialOn(_jumpProfile, transform, this);
        }

        private void HandleDived()
        {
            InGameSfxManager.Instance?.EmitSpatialOn(_diveProfile, transform, this);
        }

        private void HandleLaunched()
        {
            InGameSfxManager.Instance?.EmitSpatialOn(_launchProfile, transform, this);
        }

        private void HandleRagdollStateChanged(ERagdollState state)
        {
            if (state != ERagdollState.Ragdolled)
                return;
            InGameSfxManager.Instance?.EmitSpatialOn(_ragdollProfile, transform, this);
        }

        private void HandleStumble()
        {
            InGameSfxManager.Instance?.EmitSpatialOn(_stumbleProfile, transform, this);
        }

        private void HandleHit(HitData hitData)
        {
            InGameSfxManager.Instance?.EmitSpatialOn(_hitProfile, transform, this);
        }
    }
}
