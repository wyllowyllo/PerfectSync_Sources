using InGame.Audio;
using InGame.Gimmick;
using InGame.Player.Movement;
using InGame.Player.Network;
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
        [SerializeField] private SpatialSfxProfile _invincibleAttackerProfile;
        [SerializeField] private SpatialSfxProfile _invincibleVictimHitProfile;
        [SerializeField] private SpatialSfxProfile _finishProfile;
        [SerializeField] private SpatialSfxProfile _respawnProfile;

        [Header("Team References")]
        [SerializeField] private InvincibleModeController _invincibleController;

        private PlayerMovement _movement;
        private LaunchController _launchController;
        private HitDetector _hitDetector;
        private InvincibleContactDetector _invincibleContactDetector;
        private RespawnHandler _respawnHandler;
        private InGameCustomizationApplier _customizationApplier;

        private void Awake()
        {
            _movement = GetComponentInChildren<PlayerMovement>();
            _launchController = GetComponentInChildren<LaunchController>();
            _hitDetector = GetComponentInChildren<HitDetector>(true);
            _invincibleContactDetector = GetComponentInChildren<InvincibleContactDetector>(true);
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

            if (_hitDetector != null)
            {
                _hitDetector.OnHitDetected += HandleHit;
                _hitDetector.OnInvincibleHitReceived += HandleInvincibleVictim;
            }

            if (_invincibleController != null)
            {
                _invincibleController.OnInvincibleEnter += HandleInvincibleEnter;
                _invincibleController.OnInvincibleExit += HandleInvincibleExit;
            }

            if (_invincibleContactDetector != null)
            {
                _invincibleContactDetector.OnHitLocal += HandleInvincibleAttacker;
                _invincibleContactDetector.OnObstacleDestroyLocal += HandleInvincibleAttacker;
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

            if (_hitDetector != null)
            {
                _hitDetector.OnHitDetected -= HandleHit;
                _hitDetector.OnInvincibleHitReceived -= HandleInvincibleVictim;
            }

            if (_invincibleController != null)
            {
                _invincibleController.OnInvincibleEnter -= HandleInvincibleEnter;
                _invincibleController.OnInvincibleExit -= HandleInvincibleExit;
            }

            if (_invincibleContactDetector != null)
            {
                _invincibleContactDetector.OnHitLocal -= HandleInvincibleAttacker;
                _invincibleContactDetector.OnObstacleDestroyLocal -= HandleInvincibleAttacker;
            }

            if (_respawnHandler != null)
                _respawnHandler.OnRespawnInvincibleStart -= HandleRespawned;

            RaceRankingManager.OnTeamFinished -= HandleTeamFinished;

            // 무적 도중 컴포넌트가 비활성/파괴되어도 BGM이 머플 상태로 남지 않도록 원복.
            if (IsLocalOnSameTeam())
                InGameBgmController.Instance?.StopInvincibleBgm();
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

        private void HandleHit(HitData hitData)
        {
            PlayHitSfx(hitData.HitPoint);
            photonView.RPC(nameof(RpcPlayHit), RpcTarget.Others, hitData.HitPoint);
        }

        private void HandleInvincibleEnter()
        {
            if (!IsLocalOnSameTeam()) return;
            InGameBgmController.Instance?.PlayInvincibleBgm();
        }

        private void HandleInvincibleExit()
        {
            if (!IsLocalOnSameTeam()) return;
            InGameBgmController.Instance?.StopInvincibleBgm();
        }

        private bool IsLocalOnSameTeam() =>
            _customizationApplier != null && _customizationApplier.IsLocalPlayerOnThisTeam();

        // 가해자 공용 (플레이어 넉백 + 장애물 파괴).
        private void HandleInvincibleAttacker()
        {
            if (_invincibleAttackerProfile == null) return;
            InGameSfxManager.Instance?.EmitSpatialOn(_invincibleAttackerProfile, FollowTransform, this);
        }

        // 피해자 전용 (RPC 수신 경로).
        private void HandleInvincibleVictim(HitData hit)
        {
            if (_invincibleVictimHitProfile == null) return;
            InGameSfxManager.Instance?.EmitSpatialAt(_invincibleVictimHitProfile, hit.HitPoint, this);
        }

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
        private void RpcPlayHit(Vector3 hitPoint) => PlayHitSfx(hitPoint);

        [PunRPC]
        private void RpcPlayRespawn(Vector3 position) => PlayRespawnSfx(position);

        #endregion

        #region Local Playback

        private void PlayJumpSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_jumpProfile, FollowTransform, this);
        private void PlayDiveSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_diveProfile, FollowTransform, this);
        private void PlayLandSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_landProfile, FollowTransform, this);
        private void PlayLaunchSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_launchProfile, FollowTransform, this);
        private void PlayHitSfx(Vector3 hitPoint) => InGameSfxManager.Instance?.EmitSpatialAt(_hitProfile, hitPoint, this);
        private void PlayFinishSfx() => InGameSfxManager.Instance?.EmitSpatialOn(_finishProfile, FollowTransform, this);
        private void PlayRespawnSfx(Vector3 position) => InGameSfxManager.Instance?.EmitSpatialAt(_respawnProfile, position, this);

        #endregion
    }
}
