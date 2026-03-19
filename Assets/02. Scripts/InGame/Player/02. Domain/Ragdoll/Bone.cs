using UnityEngine;

namespace InGame.Player._02._Domain.Ragdoll
{
    public readonly struct Bone
    {
        public readonly Transform Transform;
        public readonly Rigidbody Rb;

        public Bone(Rigidbody rb)
        {
            Rb = rb;
            Transform = rb.transform;
        }
    }
}
