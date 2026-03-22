using Core;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.RagdollStateNetworkBridge)]
    public class RagdollStateNetworkBridge : MonoBehaviourPun
    {
        [SerializeField] private RagdollStateMachine _ragdollStateMachine;
        [SerializeField] private RagdollBoneReceiver _boneReceiver;

        private bool _isAuthority = true;

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;

            if (_ragdollStateMachine != null)
                _ragdollStateMachine.SetAuthority(isAuthority);
        }

        private void OnEnable()
        {
            if (_ragdollStateMachine != null)
                _ragdollStateMachine.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_ragdollStateMachine != null)
                _ragdollStateMachine.OnStateChanged -= HandleStateChanged;
        }

        // 단일 계층: 물리/BoneReceiver가 본을 직접 구동하므로 LateUpdate 복사 불필요.

        #region Authority → Remote 전송

        private void HandleStateChanged(ERagdollState newState)
        {
            if (!_isAuthority) return;

            switch (newState)
            {
                case ERagdollState.Ragdolled:
                    photonView.RPC(nameof(RpcEnterRagdolled), RpcTarget.Others);
                    break;

                case ERagdollState.Stumble:
                    photonView.RPC(nameof(RpcEnterStumble), RpcTarget.Others);
                    break;

                case ERagdollState.BlendToAnim:
                    Vector3 pos = _ragdollStateMachine.GetRecoveryPosition();
                    Quaternion rot = _ragdollStateMachine.GetRecoveryRotation();
                    bool faceUp = _ragdollStateMachine.GetIsFaceUp();
                    photonView.RPC(
                        nameof(RpcEnterBlendToAnim), RpcTarget.Others,
                        pos, rot, faceUp);
                    break;

                case ERagdollState.Animated:
                    photonView.RPC(nameof(RpcEnterAnimated), RpcTarget.Others);
                    break;

                case ERagdollState.Dead:
                    photonView.RPC(nameof(RpcEnterDead), RpcTarget.Others);
                    break;
            }
        }

        #endregion

        #region Remote RPC 수신

        [PunRPC]
        private void RpcEnterRagdolled()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.Activate();
            _ragdollStateMachine.EnterRagdolledRemote();
        }

        [PunRPC]
        private void RpcEnterStumble()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _ragdollStateMachine.EnterStumbleRemote();
        }

        [PunRPC]
        private void RpcEnterBlendToAnim(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.Deactivate();
            _ragdollStateMachine.EnterBlendToAnimRemote(rootPos, rootRot, isFaceUp);
        }

        [PunRPC]
        private void RpcEnterAnimated()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.Deactivate();
            _ragdollStateMachine.EnterAnimatedRemote();
        }

        [PunRPC]
        private void RpcEnterDead()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.Activate();
            _ragdollStateMachine.EnterDeadRemote();
        }

        #endregion
    }
}
