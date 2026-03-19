using InGame.Player.Animation;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    /// <summary>
    /// Ragdoll guard for PhotonTransformView + trigger RPC relay.
    /// Position/rotation sync is handled by PhotonTransformView,
    /// animator params by PhotonAnimatorView.
    /// This component only disables transform sync during ragdoll
    /// and forwards trigger RPCs that PhotonAnimatorView cannot handle.
    /// </summary>
    public class BodySyncBridge : MonoBehaviourPun
    {
        private PhotonTransformView _transformView;
        private IRagdoll _ragdoll;
        private PlayerAnimation _playerAnimation;

        private void Awake()
        {
            _transformView = GetComponent<PhotonTransformView>();
            _ragdoll = GetComponent<IRagdoll>();
            _playerAnimation = GetComponent<PlayerAnimation>();
        }

        private void LateUpdate()
        {
            if (_transformView == null || _ragdoll == null) return;

            // Disable transform sync during ragdoll (bones simulate locally).
            // PhotonAnimatorView stays enabled for GetUpFromBack/Belly sync.
            _transformView.enabled = !_ragdoll.IsRagdollActive;
        }

        /// <summary>
        /// Host sends trigger animation to guests via RPC.
        /// </summary>
        public void SendAnimTrigger(byte triggerId)
        {
            photonView.RPC(nameof(RpcAnimTrigger), RpcTarget.Others, triggerId);
        }

        [PunRPC]
        private void RpcAnimTrigger(byte triggerId)
        {
            if (_playerAnimation == null) return;

            switch (triggerId)
            {
                case 0: _playerAnimation.Jump(); break;
                case 1: _playerAnimation.Dive(); break;
                case 2: _playerAnimation.Land(true); break;
            }
        }
    }
}
