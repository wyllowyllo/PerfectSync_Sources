using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public readonly struct RagdollBoneSnapshot
    {
        public readonly Vector3[] BonePositions;
        public readonly Quaternion[] BoneRotations;

        public RagdollBoneSnapshot(Vector3[] bonePositions, Quaternion[] boneRotations)
        {
            BonePositions = bonePositions;
            BoneRotations = boneRotations;
        }

        public static RagdollBoneSnapshot Capture(IRagdollRig rig)
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
    }
}
