using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerSoundTrigger : MonoBehaviour
    {
        [Header("Spatial SFX Players")]
        [SerializeField] private SpatialSfxPlayer _jumpSfx;
        [SerializeField] private SpatialSfxPlayer _diveSfx;
        [SerializeField] private SpatialSfxPlayer _launchSfx;
        [SerializeField] private SpatialSfxPlayer _hitSfx;
        [SerializeField] private SpatialSfxPlayer _stumbleSfx;
        [SerializeField] private SpatialSfxPlayer _ragdollSfx;

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
            if (_jumpSfx != null)
                _jumpSfx.Play();
        }

        private void HandleDived()
        {
            if (_diveSfx != null)
                _diveSfx.Play();
        }

        private void HandleLaunched()
        {
            if (_launchSfx != null)
                _launchSfx.Play();
        }

        private void HandleRagdollStateChanged(ERagdollState state)
        {
            if (state == ERagdollState.Ragdolled && _ragdollSfx != null)
                _ragdollSfx.Play();
        }

        private void HandleStumble()
        {
            if (_stumbleSfx != null)
                _stumbleSfx.Play();
        }

        private void HandleHit(HitData hitData)
        {
            if (_hitSfx != null)
                _hitSfx.Play();
        }
    }
}
