using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public interface IRagdollRig
    {
        void ActivatePhysics(Vector3 inheritedVelocity);
        void ActivateKinematic();
        void DeactivateRagdoll();
        bool IsSettled(float settleVelocity);
        IReadOnlyList<Rigidbody> Rigidbodies { get; }
        IReadOnlyList<Transform> BoneTransforms { get; }
        Transform PelvisTransform { get; }
    }
}
