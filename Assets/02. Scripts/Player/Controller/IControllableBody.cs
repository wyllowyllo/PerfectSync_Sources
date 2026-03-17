using UnityEngine;

namespace Player.Controller
{
    public interface IControllableBody
    {
        void ApplyInput(Vector3 worldDirection, bool jump);
        Vector3 Velocity { get; set; }
        Transform BodyTransform { get; }
        bool IsRagdollActive { get; }
    }
}
