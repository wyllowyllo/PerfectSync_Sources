using Core;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.RagdollBoneSynchronizer)]
    public class RagdollBoneSynchronizer : MonoBehaviourPun, IPunObservable
    {
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private RagdollStateMachine _ragdollStateMachine;

        [Header("Pelvis Correction")]
        [SerializeField] private float _blendFactor = 0.15f;
        [SerializeField] private float _catchUpTime = 0.3f;

        private bool _syncEnabled;
        private Rigidbody _pelvisRb;

        private Vector3 _targetPosition;
        private Vector3 _targetVelocity;
        private bool _hasTarget;

        public void SetSyncEnabled(bool enabled)
        {
            _syncEnabled = enabled;
            _hasTarget = false;
        }

        private void Start()
        {
            _pelvisRb = _ragdollRig.Rigidbodies[0];
        }

        private void FixedUpdate()
        {
            if (photonView.IsMine) return;
            if (!_syncEnabled || !_hasTarget) return;
            if (!_ragdollStateMachine.IsPhysicsRagdoll) return;

            // 호스트 위치 방향으로 보정 속도 계산.
            Vector3 correctionVel = (_targetPosition - _pelvisRb.position) / _catchUpTime;

            // 로컬 물리 속도와 호스트 속도 + 보정을 블렌딩.
            Vector3 blendedVel = Vector3.Lerp(
                _pelvisRb.linearVelocity,
                _targetVelocity + correctionVel,
                _blendFactor);

            _pelvisRb.linearVelocity = blendedVel;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!_syncEnabled) return;
            if (_ragdollStateMachine == null) return;
            if (!_ragdollStateMachine.IsPhysicsRagdoll) return;

            if (stream.IsWriting)
            {
                stream.SendNext(_pelvisRb.position);
                stream.SendNext(_pelvisRb.linearVelocity);
            }
            else
            {
                _targetPosition = (Vector3)stream.ReceiveNext();
                _targetVelocity = (Vector3)stream.ReceiveNext();

                // 네트워크 지연만큼 위치 예측.
                float lag = Mathf.Abs(
                    (float)(PhotonNetwork.Time - info.SentServerTime));
                _targetPosition += _targetVelocity * lag;

                _hasTarget = true;
            }
        }
    }
}
