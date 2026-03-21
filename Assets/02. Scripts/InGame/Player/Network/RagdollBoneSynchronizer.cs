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

        private bool _syncEnabled;

        // 보간 버퍼.
        private Vector3[] _previousPositions;
        private Quaternion[] _previousRotations;
        private Vector3[] _targetPositions;
        private Quaternion[] _targetRotations;
        private float _interpolationTime;
        private float _interpolationDuration;
        private bool _hasTarget;
        private int _boneCount;

        private const float MinInterpolationDuration = 0.05f;

        public void SetSyncEnabled(bool enabled)
        {
            _syncEnabled = enabled;
            _hasTarget = false;
        }

        private void Start()
        {
            _boneCount = _ragdollRig.BoneTransforms.Count;
            _previousPositions = new Vector3[_boneCount];
            _previousRotations = new Quaternion[_boneCount];
            _targetPositions = new Vector3[_boneCount];
            _targetRotations = new Quaternion[_boneCount];
        }

        private void Update()
        {
            if (photonView.IsMine) return;
            if (!_syncEnabled || !_hasTarget) return;
            if (!_ragdollStateMachine.IsPhysicsRagdoll) return;

            _interpolationTime += Time.deltaTime;
            float t = (_interpolationDuration > 0f)
                ? Mathf.Clamp01(_interpolationTime / _interpolationDuration)
                : 1f;

            var bones = _ragdollRig.BoneTransforms;
            for (int i = 0; i < _boneCount; i++)
            {
                bones[i].position = Vector3.Lerp(
                    _previousPositions[i], _targetPositions[i], t);
                bones[i].rotation = Quaternion.Slerp(
                    _previousRotations[i], _targetRotations[i], t);
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!_syncEnabled) return;
            if (_ragdollStateMachine == null) return;
            if (!_ragdollStateMachine.IsPhysicsRagdoll) return;

            var bones = _ragdollRig.BoneTransforms;

            if (stream.IsWriting)
            {
                for (int i = 0; i < _boneCount; i++)
                {
                    stream.SendNext(bones[i].position);
                    stream.SendNext(bones[i].rotation);
                }
            }
            else
            {
                // 현재 위치를 이전 스냅샷으로 저장.
                for (int i = 0; i < _boneCount; i++)
                {
                    _previousPositions[i] = bones[i].position;
                    _previousRotations[i] = bones[i].rotation;
                }

                for (int i = 0; i < _boneCount; i++)
                {
                    _targetPositions[i] = (Vector3)stream.ReceiveNext();
                    _targetRotations[i] = (Quaternion)stream.ReceiveNext();
                }

                float lag = Mathf.Abs(
                    (float)(PhotonNetwork.Time - info.SentServerTime));
                _interpolationDuration = Mathf.Max(lag, MinInterpolationDuration);
                _interpolationTime = 0f;
                _hasTarget = true;
            }
        }
    }
}
