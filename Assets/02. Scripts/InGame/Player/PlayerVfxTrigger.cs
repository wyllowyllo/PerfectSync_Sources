using Core.VFX;
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
        private PlayerMovement _movement;
        private HitDetector _hitDetector;
        private InGameCustomizationApplier _customizationApplier;

        private void Awake()
        {
            _movement = GetComponentInChildren<PlayerMovement>();
            _hitDetector = GetComponentInChildren<HitDetector>();
            _customizationApplier = GetComponent<InGameCustomizationApplier>();
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

            RaceRankingManager.OnTeamFinished -= HandleTeamFinished;
        }

        #region Authority → Local + Remote

        private void HandleJumped()
        {
            PlayAtFoot(EVfxId.Jump);
            photonView.RPC(nameof(RpcPlayJump), RpcTarget.Others);
        }

        private void HandleLanded()
        {
            PlayAtFoot(EVfxId.Land);
            photonView.RPC(nameof(RpcPlayLand), RpcTarget.Others);
        }

        private void HandleHit(HitData hitData)
        {
            PlayAt(EVfxId.Hit, hitData.HitPoint);
            photonView.RPC(nameof(RpcPlayHit), RpcTarget.Others, hitData.HitPoint);
        }

        private void HandleTeamFinished(int teamNumber, int place)
        {
            if (_customizationApplier == null) return;
            if (teamNumber != _customizationApplier.TeamNumber) return;

            // 모든 클라이언트에서 OnTeamFinished가 동시에 발행되므로 RPC 불필요.
            PlayAtFoot(EVfxId.Finish);
        }

        #endregion

        #region Remote RPC 수신

        [PunRPC]
        private void RpcPlayJump() => PlayAtFoot(EVfxId.Jump);

        [PunRPC]
        private void RpcPlayLand() => PlayAtFoot(EVfxId.Land);

        [PunRPC]
        private void RpcPlayHit(Vector3 hitPoint) => PlayAt(EVfxId.Hit, hitPoint);

        #endregion

        #region Local Playback

        private void PlayAtFoot(EVfxId id)
        {
            InGameVfxManager.Instance?.Emit(id, GetFootPosition(), Quaternion.identity, this);
        }

        private void PlayAt(EVfxId id, Vector3 worldPosition)
        {
            InGameVfxManager.Instance?.Emit(id, worldPosition, Quaternion.identity, this);
        }

        private Vector3 GetFootPosition()
        {
            if (_movement != null && _movement.BodyTransform != null)
                return _movement.BodyTransform.position;
            return transform.position;
        }

        #endregion
    }
}
