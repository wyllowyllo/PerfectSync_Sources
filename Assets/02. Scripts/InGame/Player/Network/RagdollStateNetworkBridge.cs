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
        [SerializeField] private PoseTransfer _poseTransfer;

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

        private void LateUpdate()
        {
            // Remote: RagdollBoneReceiver가 kinematic 본을 갱신한 뒤, PoseTransfer로 비주얼 복사.
            if (_isAuthority) return;

            ERagdollState state = _ragdollStateMachine.CurrentState;
            if (state == ERagdollState.Ragdoll || state == ERagdollState.Dead)
                _poseTransfer.CopyPose();
        }

        #region Authority → Remote 전송

        private void HandleStateChanged(ERagdollState newState)
        {
            if (!_isAuthority) return;

            switch (newState)
            {
                case ERagdollState.Ragdoll:
                    photonView.RPC(nameof(RpcEnterRagdoll), RpcTarget.Others);
                    break;

                case ERagdollState.Stumble:
                    photonView.RPC(nameof(RpcEnterStumble), RpcTarget.Others);
                    break;

                case ERagdollState.Recovery:
                    Vector3 pos = _ragdollStateMachine.GetRecoveryPosition();
                    Quaternion rot = _ragdollStateMachine.GetRecoveryRotation();
                    bool faceUp = _ragdollStateMachine.GetIsFaceUp();
                    photonView.RPC(
                        nameof(RpcEnterRecovery), RpcTarget.Others,
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
        private void RpcEnterRagdoll()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.Activate();
            _ragdollStateMachine.EnterRagdollRemote();
        }

        [PunRPC]
        private void RpcEnterStumble()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _ragdollStateMachine.EnterStumbleRemote();
        }

        [PunRPC]
        private void RpcEnterRecovery(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.Deactivate();
            _ragdollStateMachine.EnterRecoveryRemote(rootPos, rootRot, isFaceUp);
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
