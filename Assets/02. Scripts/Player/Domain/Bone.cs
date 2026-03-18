using UnityEngine;

namespace Player.Domain
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
