using InGame.Audio;
using InGame.Gimmick;
using InGame.Player.Movement;
using InGame.Player.Network;
using InGame.Player.Ragdoll;
using InGame.Team;
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
        [SerializeField] private SpatialSfxProfile _landProfile;
        [SerializeField] private SpatialSfxProfile _launchProfile;
        [SerializeField] private SpatialSfxProfile _hitProfile;
        [SerializeField] private SpatialSfxProfile _stumbleProfile;
        [SerializeField] private SpatialSfxProfile _ragdollProfile;
        [SerializeField] private SpatialSfxProfile _invincibleEnterProfile;
        [SerializeField] private SpatialSfxProfile _invincibleExitProfile;
        [SerializeField] private SpatialSfxProfile _finishProfile;
        [SerializeField] private SpatialSfxProfile _respawnProfile;

        [Header("Team References")]
        [SerializeField] private InvincibleModeController _invincibleController;

        private PlayerMovement _movement;
        private LaunchController _launchController;
        private RagdollStateMachine _ragdollStateMachine;
        private HitDetector _hitDetector;
        private RespawnHandler _respawnHandler;
        private InGameCustomizationApplier _customizationApplier;

        private void Awake()
        {
            _movement = GetComponentInChildren<PlayerMovement>();
            _launchController = GetComponentInChildren<LaunchController>();
            _ragdollStateMachine = GetComponentInChildren<RagdollStateMachine>();
            _hitDetector = GetComponentInChildren<HitDetector>();
            _respawnHandler = GetComponent<RespawnHandler>();
            _customizationApplier = GetComponent<InGameCustomizationApplier>();
        }

        private Transform FollowTransform =>
            _movement != null && _movement.BodyTransform != null
                ? _movement.BodyTransform
                : transform;

        private void OnEnable()
        {
            if (_movement != null)
            {
                _movement.OnJumped += HandleJumped;
                _movement.OnDived += HandleDived;
                _movement.OnLanded += HandleLanded;
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

            if (_invincibleController != null)
            {
                _invincibleController.OnInvincibleEnter += HandleInvincibleEnter;
                _invincibleController.OnInvincibleExit += HandleInvincibleExit;
            }

            if (_respawnHandler != null)
                _respawnHandler.OnRespawnInvincibleStart += HandleRespawned;

            RaceRankingManager.OnTeamFinished += HandleTeamFinished;
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.OnJumped -= HandleJumped;
                _movement.OnDived -= HandleDived;
                _movement.OnLanded -= HandleLanded;
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

            if (_invincibleController != null)
            {
                _invincibleController.OnInvincibleEnter -= HandleInvincibleEnter;
                _invincibleController.OnInvincibleExit -= HandleInvincibleExit;
            }

            if (_respawnHandler != null)
                _respawnHandler.OnRespawnInvincibleStart -= HandleRespawned;

            RaceRankingManager.OnTeamFinished -= HandleTeamFinished;
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

        private void HandleLanded()
        {
            PlayLandSfx();
            photonView.RPC(nameof(RpcPlayLand), RpcTarget.Others);
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
            PlayHitSfx(hitData.HitPoint);
            photonView.RPC(nameof(RpcPlayHit), RpcTarget.Others, hitData.HitPoint);
        }

        private void HandleInvincibleEnter()
            => InGameSfxManager.Instance?.EmitSpatialOn(_invincibleEnterProfile, FollowTransform, this);

        private void HandleInvincibleExit()
            => InGameSfxManager.Instance?.EmitSpatialOn(_invincibleExitProfile, FollowTransform, this);

        private void HandleRespawned(Vector3 spawnPosition)
        {
            PlayRespawnSfx(spawnPosition);
            photonView.RPC(nameof(RpcPlayRespawn), RpcTarget.Others, spawnPosition);
        }

        private void HandleTeamFinished(int teamNumber, int place)
        {
            if (_customizationApplier == null) return;
            if (teamNumber != _customizationApplier.TeamNumber) return;

            // 모든 클라이언트에서 OnTeamFinished가 동시에 발행되므로 RPC 불필요.
            PlayFinishSfx();
        }

        #endregion

        #region Remote RPC 수신

        [PunRPC]
        private void RpcPlayJump() => PlayJumpSfx();

        [PunRPC]
        private void RpcPlayDive() => PlayDiveSfx();

        [PunRPC]
        private void RpcPlayLand() => PlayLandSfx();

        [PunRPC]
        private void RpcPlayLaunch() => PlayLaunchSfx();

        [PunRPC]
        private void RpcPlayRagdoll() => PlayRagdollSfx();

        [PunRPC]
        private void RpcPlayStumble() => PlayStumbleSfx();

        [PunRPC]
        private void RpcPlayHit(Vector3 hitPoint) => PlayHitSfx(hitPoint);

        [PunRPC]
        private void RpcPlayRespawn(Vector3 position) => PlayRespawnSfx(position);

        #endregion

        #region Local Playback

        private void PlayJumpSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_jumpProfile, FollowTransform, this);
        private void PlayDiveSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_diveProfile, FollowTransform, this);
        private void PlayLandSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_landProfile, FollowTransform, this);
        private void PlayLaunchSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_launchProfile, FollowTransform, this);
        private void PlayRagdollSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_ragdollProfile, FollowTransform, this);
        private void PlayStumbleSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_stumbleProfile, FollowTransform, this);
        private void PlayHitSfx(Vector3 hitPoint) => InGameSfxManager.Instance?.EmitSpatialAt(_hitProfile, hitPoint, this);
        private void PlayFinishSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_finishProfile, FollowTransform, this);
        private void PlayRespawnSfx(Vector3 position) => InGameSfxManager.Instance?.EmitSpatialAt(_respawnProfile, position, this);

        #endregion
    }
}
