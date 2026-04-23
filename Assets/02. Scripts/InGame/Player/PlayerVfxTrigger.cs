using Core.VFX;
using InGame.Gimmick;
using InGame.Player.Movement;
using InGame.Player.Network;
using InGame.VFX;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player
{
    [RequireComponent(typeof(PhotonView))]
    public class PlayerVfxTrigger : MonoBehaviourPun
    {
        [Header("Spatial VFX Profiles")]
        [SerializeField] private SpatialVfxProfile _jumpProfile;
        [SerializeField] private SpatialVfxProfile _landProfile;
        [SerializeField] private SpatialVfxProfile _hitProfile;
        [SerializeField] private SpatialVfxProfile _finishProfile;
        [SerializeField] private SpatialVfxProfile _respawnProfile;

        private PlayerMovement _movement;
        private HitDetector _hitDetector;
        private InvincibleContactDetector _invincibleContactDetector;
        private InGameCustomizationApplier _customizationApplier;
        private RespawnHandler _respawnHandler;

        private void Awake()
        {
            _movement = GetComponentInChildren<PlayerMovement>();
            _hitDetector = GetComponentInChildren<HitDetector>();
            _invincibleContactDetector = GetComponentInChildren<InvincibleContactDetector>(true);
            _customizationApplier = GetComponent<InGameCustomizationApplier>();
            _respawnHandler = GetComponent<RespawnHandler>();
        }

        private void OnEnable()
        {
            if (_movement != null)
            {
                _movement.OnJumped += HandleJumped;
                _movement.OnLanded += HandleLanded;
            }

            if (_hitDetector != null)
                _hitDetector.OnHitDetected += HandleHit;

            if (_invincibleContactDetector != null)
            {
                _invincibleContactDetector.OnHitLocal += HandleInvincibleContact;
                _invincibleContactDetector.OnObstacleDestroyLocal += HandleInvincibleContact;
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
                _movement.OnLanded -= HandleLanded;
            }

            if (_hitDetector != null)
                _hitDetector.OnHitDetected -= HandleHit;

            if (_invincibleContactDetector != null)
            {
                _invincibleContactDetector.OnHitLocal -= HandleInvincibleContact;
                _invincibleContactDetector.OnObstacleDestroyLocal -= HandleInvincibleContact;
            }

            if (_respawnHandler != null)
                _respawnHandler.OnRespawnInvincibleStart -= HandleRespawned;

            RaceRankingManager.OnTeamFinished -= HandleTeamFinished;
        }

        #region Authority → Local + Remote

        private void HandleJumped()
        {
            PlayJumpVfx();
            photonView.RPC(nameof(RpcPlayJump), RpcTarget.Others);
        }

        private void HandleLanded()
        {
            PlayLandVfx();
            photonView.RPC(nameof(RpcPlayLand), RpcTarget.Others);
        }

        private void HandleHit(HitData hitData)
        {
            PlayHitVfx(hitData.HitPoint);
            photonView.RPC(nameof(RpcPlayHit), RpcTarget.Others, hitData.HitPoint);
        }

        // 무적 모드 감지 시점에 같은 hit VFX 재생. 각 클라이언트의 InvincibleContactDetector가 개별 감지하므로 RPC 불필요.
        private void HandleInvincibleContact() => PlayHitVfx(GetFootPosition());

        private void HandleTeamFinished(int teamNumber, int place)
        {
            if (_customizationApplier == null) return;
            if (teamNumber != _customizationApplier.TeamNumber) return;

            // 모든 클라이언트에서 OnTeamFinished가 동시에 발행되므로 RPC 불필요.
            PlayFinishVfx();
        }

        private void HandleRespawned(Vector3 spawnPosition)
        {
            PlayRespawnVfx(spawnPosition);
            photonView.RPC(nameof(RpcPlayRespawn), RpcTarget.Others, spawnPosition);
        }

        #endregion

        #region Remote RPC 수신

        [PunRPC]
        private void RpcPlayJump() => PlayJumpVfx();

        [PunRPC]
        private void RpcPlayLand() => PlayLandVfx();

        [PunRPC]
        private void RpcPlayHit(Vector3 hitPoint) => PlayHitVfx(hitPoint);

        [PunRPC]
        private void RpcPlayRespawn(Vector3 position) => PlayRespawnVfx(position);

        #endregion

        #region Local Playback

        private void PlayJumpVfx() =>
            InGameVfxManager.Instance?.EmitAt(_jumpProfile, GetFootPosition(), Quaternion.identity, this);

        private void PlayLandVfx() =>
            InGameVfxManager.Instance?.EmitAt(_landProfile, GetFootPosition(), Quaternion.identity, this);

        private void PlayHitVfx(Vector3 hitPoint) =>
            InGameVfxManager.Instance?.EmitAt(_hitProfile, hitPoint, Quaternion.identity, this);

        private void PlayFinishVfx() =>
            InGameVfxManager.Instance?.EmitAt(_finishProfile, GetFootPosition(), Quaternion.identity, this);

        private void PlayRespawnVfx(Vector3 position) =>
            InGameVfxManager.Instance?.EmitAt(_respawnProfile, position, Quaternion.identity, this);

        private Vector3 GetFootPosition()
        {
            if (_movement != null && _movement.BodyTransform != null)
                return _movement.BodyTransform.position;
            return transform.position;
        }

        #endregion
    }
}
