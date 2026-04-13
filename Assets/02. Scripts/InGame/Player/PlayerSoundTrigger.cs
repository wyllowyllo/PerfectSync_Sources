using InGame.Audio;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player
{
    [RequireComponent(typeof(PhotonView))]
    public class PlayerSoundTrigger : MonoBehaviourPun
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

        #region Authority → Local + Remote

        private void HandleJumped()
        {
            PlayJumpSfx();
            photonView.RPC(nameof(RpcPlayJump), RpcTarget.Others);
        }

        private void HandleDived()
        {
            PlayDiveSfx();
            photonView.RPC(nameof(RpcPlayDive), RpcTarget.Others);
        }

        private void HandleLaunched()
        {
            PlayLaunchSfx();
            photonView.RPC(nameof(RpcPlayLaunch), RpcTarget.Others);
        }

        private void HandleRagdollStateChanged(ERagdollState state)
        {
            if (state != ERagdollState.Ragdolled)
                return;

            PlayRagdollSfx();
            photonView.RPC(nameof(RpcPlayRagdoll), RpcTarget.Others);
        }

        private void HandleStumble()
        {
            PlayStumbleSfx();
            photonView.RPC(nameof(RpcPlayStumble), RpcTarget.Others);
        }

        private void HandleHit(HitData hitData)
        {
            PlayHitSfx();
            photonView.RPC(nameof(RpcPlayHit), RpcTarget.Others);
        }

        #endregion

        #region Remote RPC 수신

        [PunRPC]
        private void RpcPlayJump() => PlayJumpSfx();

        [PunRPC]
        private void RpcPlayDive() => PlayDiveSfx();

        [PunRPC]
        private void RpcPlayLaunch() => PlayLaunchSfx();

        [PunRPC]
        private void RpcPlayRagdoll() => PlayRagdollSfx();

        [PunRPC]
        private void RpcPlayStumble() => PlayStumbleSfx();

        [PunRPC]
        private void RpcPlayHit() => PlayHitSfx();

        #endregion

        #region Local Playback

        private void PlayJumpSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_jumpProfile, transform, this);
        private void PlayDiveSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_diveProfile, transform, this);
        private void PlayLaunchSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_launchProfile, transform, this);
        private void PlayRagdollSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_ragdollProfile, transform, this);
        private void PlayStumbleSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_stumbleProfile, transform, this);
        private void PlayHitSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_hitProfile, transform, this);

        #endregion
    }
}
