using UnityEngine;

namespace PlayerSystem.Domain
{
    public interface IControllableBody
    {
        void ApplyInput(Vector3 worldDirection, bool jump);
        Vector3 Velocity { get; set; }
        Transform BodyTransform { get; }
        Transform CameraFollowPoint { get; }
        bool IsRagdollActive { get; }
    }
}
