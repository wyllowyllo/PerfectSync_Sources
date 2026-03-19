using UnityEngine;

namespace PlayerSystem.Common
{
    public static class DualInputCombiner
    {
        private const float MaxMergedMagnitude = 1.5f;

        public static Vector3 Combine(Vector3 inputA, Vector3 inputB)
        {
            Vector3 sum = inputA + inputB;
            return Vector3.ClampMagnitude(sum, MaxMergedMagnitude);
        }
    }
}
