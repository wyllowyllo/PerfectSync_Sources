using UnityEngine;

namespace Player.Domain
{
    public interface IControllableBody
    {
        void ApplyInput(Vector3 worldDirection, bool jump);
        Vector3 Velocity { get; set; }
        Transform BodyTransform { get; }
        bool IsRagdollActive { get; }
    }
}
