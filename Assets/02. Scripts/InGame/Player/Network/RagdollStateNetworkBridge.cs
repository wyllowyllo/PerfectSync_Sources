using Core;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.RagdollStateNetworkBridge)]
    [RequireComponent(typeof(RagdollStateMachine))]
    [RequireComponent(typeof(RagdollBoneReceiver))]
    public class RagdollStateNetworkBridge : MonoBehaviourPun
    {
        private RagdollStateMachine _ragdollStateMachine;
        private RagdollBoneReceiver _boneReceiver;
        private bool _isAuthority = true;
        private ERagdollState _previousState = ERagdollState.Animated;

        private void Awake()
        {
            _ragdollStateMachine = GetComponent<RagdollStateMachine>();
            _boneReceiver = GetComponent<RagdollBoneReceiver>();
        }

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;

            if (_ragdollStateMachine != null)
                _ragdollStateMachine.SetAuthority(isAuthority);
        }

        private void OnEnable()
        {
            if (_ragdollStateMachine != null)
            {
                _ragdollStateMachine.OnStateChanged += HandleStateChanged;
                _ragdollStateMachine.OnStumblePlayed += HandleStumblePlayed;
            }
        }

        private void OnDisable()
        {
            if (_ragdollStateMachine != null)
            {
                _ragdollStateMachine.OnStateChanged -= HandleStateChanged;
                _ragdollStateMachine.OnStumblePlayed -= HandleStumblePlayed;
            }
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

                case ERagdollState.BlendToAnim:
                    Vector3 pos = _ragdollStateMachine.RecoveryPosition;
                    Quaternion rot = _ragdollStateMachine.RecoveryRotation;
                    bool faceUp = _ragdollStateMachine.IsFaceUp();
                    photonView.RPC(
                        nameof(RpcEnterBlendToAnim), RpcTarget.Others,
                        pos, rot, faceUp);
                    break;

                case ERagdollState.Animated:
                    if (_previousState == ERagdollState.Dead)
                        photonView.RPC(nameof(RpcRespawn), RpcTarget.Others);
                    else
                        photonView.RPC(nameof(RpcEnterAnimated), RpcTarget.Others);
                    break;

                case ERagdollState.Dead:
                    photonView.RPC(nameof(RpcEnterDead), RpcTarget.Others);
                    break;
            }

            _previousState = newState;
        }

        private void HandleStumblePlayed()
        {
            if (!_isAuthority) return;
            photonView.RPC(nameof(RpcPlayStumble), RpcTarget.Others);
        }

        #endregion

        #region Remote RPC 수신

        [PunRPC]
        private void RpcEnterRagdolled()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.StartReceiving();
            _ragdollStateMachine.EnterRagdolledRemote();
        }

        [PunRPC]
        private void RpcPlayStumble()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _ragdollStateMachine.PlayStumbleAnimation();
        }

        [PunRPC]
        private void RpcEnterBlendToAnim(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.StopReceiving();
            _ragdollStateMachine.EnterBlendToAnimRemote(rootPos, rootRot, isFaceUp);
        }

        [PunRPC]
        private void RpcEnterAnimated()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.StopReceiving();
            _ragdollStateMachine.EnterAnimatedRemote();
        }

        [PunRPC]
        private void RpcEnterDead()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.StartReceiving();
            _ragdollStateMachine.EnterDeadRemote();
        }

        [PunRPC]
        private void RpcRespawn()
        {
            if (_isAuthority) return;
            if (!gameObject.activeInHierarchy) return;

            _boneReceiver.StopReceiving();
            _ragdollStateMachine.RespawnRemote();
        }

        #endregion
    }
}
