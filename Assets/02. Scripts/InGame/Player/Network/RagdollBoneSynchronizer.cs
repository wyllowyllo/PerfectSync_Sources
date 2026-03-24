using Core;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    [DefaultExecutionOrder(ExecutionOrderConstants.RagdollBoneSynchronizer)]
    [RequireComponent(typeof(RagdollStateMachine))]
    [RequireComponent(typeof(RagdollBoneReceiver))]
    public class RagdollBoneSynchronizer : MonoBehaviourPun, IPunObservable
    {
        [SerializeField] private RagdollRig _ragdollRig;

        private RagdollStateMachine _ragdollStateMachine;
        private RagdollBoneReceiver _boneReceiver;
        private bool _syncEnabled;
        private bool _isAuthority = true;

        private void Awake()
        {
            _ragdollStateMachine = GetComponent<RagdollStateMachine>();
            _boneReceiver = GetComponent<RagdollBoneReceiver>();
        }

        public void SetSyncEnabled(bool enabled)
        {
            _syncEnabled = enabled;
        }

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!_syncEnabled) return;
            if (_ragdollStateMachine == null) return;

            if (stream.IsWriting)
            {
                WriteToStream(stream);
            }
            else
            {
                ReadFromStream(stream, info);
            }
        }

        private void WriteToStream(PhotonStream stream)
        {
            bool isRagdoll = _ragdollStateMachine.IsPhysicsRagdoll;
            stream.SendNext(isRagdoll);

            if (isRagdoll)
            {
                RagdollBoneSnapshot snapshot = RagdollBoneSnapshot.Capture(_ragdollRig);
                RagdollBoneSnapshotSerializer.Write(snapshot, stream);
            }
        }

        private void ReadFromStream(PhotonStream stream, PhotonMessageInfo _)
        {
            bool isRagdoll = (bool)stream.ReceiveNext();

            if (isRagdoll)
            {
                RagdollBoneSnapshot snapshot = RagdollBoneSnapshotSerializer.Read(stream);

                if (_boneReceiver != null)
                    _boneReceiver.ApplySnapshot(snapshot);
            }
        }
    }
}
