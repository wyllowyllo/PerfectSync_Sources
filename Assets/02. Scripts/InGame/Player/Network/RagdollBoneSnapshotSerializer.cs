using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    public static class RagdollBoneSnapshotSerializer
    {
        private const int MaxBoneCount = 64;

        public static void Write(RagdollBoneSnapshot snapshot, PhotonStream stream)
        {
            stream.SendNext(snapshot.BonePositions.Length);

            for (int i = 0; i < snapshot.BonePositions.Length; i++)
            {
                stream.SendNext(snapshot.BonePositions[i]);
                stream.SendNext(snapshot.BoneRotations[i]);
            }
        }

        public static RagdollBoneSnapshot Read(PhotonStream stream)
        {
            int count = (int)stream.ReceiveNext();
            if (count < 0 || count > MaxBoneCount)
                return new RagdollBoneSnapshot(System.Array.Empty<Vector3>(), System.Array.Empty<Quaternion>());

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
