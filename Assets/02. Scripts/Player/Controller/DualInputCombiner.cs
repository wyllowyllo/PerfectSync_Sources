using UnityEngine;

namespace Player.Controller
{
    public static class DualInputCombiner
    {
        private const float MaxMergedMagnitude = 1.5f;

        public static Vector2 Combine(Vector2 inputA, Vector2 inputB)
        {
            Vector2 sum = inputA + inputB;
            return Vector2.ClampMagnitude(sum, MaxMergedMagnitude);
        }
    }
}
