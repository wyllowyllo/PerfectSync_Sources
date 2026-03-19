using UnityEngine;

namespace Core.Utilities
{
    public static class CameraRelativeConverter
    {
        public static Vector3 Convert(Vector2 rawInput, Transform cameraTransform)
        {
            float h = rawInput.x;
            float v = rawInput.y;

            if (cameraTransform == null)
            {
                var worldDir = new Vector3(h, 0f, v);
                return worldDir.sqrMagnitude > 1f ? worldDir.normalized : worldDir;
            }

            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 direction = camRight * h + camForward * v;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }
    }
}
