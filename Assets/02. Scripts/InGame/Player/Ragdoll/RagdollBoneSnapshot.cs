using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public readonly struct RagdollBoneSnapshot
    {
        // 모든 본의 world position + world rotation.
        // 래그돌 물리 시뮬 중 각 Rigidbody는 독립적으로 이동하므로
        // localRotation만으로는 정확한 위치를 재현할 수 없음.
        public readonly Vector3[] BonePositions;
        public readonly Quaternion[] BoneRotations;

        public RagdollBoneSnapshot(Vector3[] bonePositions, Quaternion[] boneRotations)
        {
            BonePositions = bonePositions;
            BoneRotations = boneRotations;
        }

        public static RagdollBoneSnapshot Capture(RagdollRig rig)
        {
            var bones = rig.BoneTransforms;
            int count = bones.Count;
            var positions = new Vector3[count];
            var rotations = new Quaternion[count];

            for (int i = 0; i < count; i++)
            {
                positions[i] = bones[i].position;
                rotations[i] = bones[i].rotation;
            }

            return new RagdollBoneSnapshot(positions, rotations);
        }

        public void WriteTo(PhotonStream stream)
        {
            stream.SendNext(BonePositions.Length);

            for (int i = 0; i < BonePositions.Length; i++)
            {
                stream.SendNext(BonePositions[i]);
                stream.SendNext(BoneRotations[i]);
            }
        }

        public static RagdollBoneSnapshot ReadFrom(PhotonStream stream)
        {
            int count = (int)stream.ReceiveNext();
            var positions = new Vector3[count];
            var rotations = new Quaternion[count];

            for (int i = 0; i < count; i++)
            {
                positions[i] = (Vector3)stream.ReceiveNext();
                rotations[i] = (Quaternion)stream.ReceiveNext();
            }

            return new RagdollBoneSnapshot(positions, rotations);
        }
    }
}
