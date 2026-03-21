using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public interface IRagdollRig
    {
        void Activate(Vector3 inheritedVelocity);
        void ActivateKinematic();
        void Deactivate();
        bool IsSettled(float settleVelocity);
        IReadOnlyList<Rigidbody> Rigidbodies { get; }
        IReadOnlyList<Transform> BoneTransforms { get; }
        Transform PelvisTransform { get; }
    }
}
