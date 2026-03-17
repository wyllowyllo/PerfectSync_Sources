using UnityEngine;

namespace Player.Controller
{
    public interface IControllableBody
    {
        void ApplyInput(Vector2 move, bool jump);
        void SetCameraTransform(Transform cameraTransform);
        Vector3 Velocity { get; set; }
        Transform BodyTransform { get; }
        bool IsRagdollActive { get; }
    }
}
