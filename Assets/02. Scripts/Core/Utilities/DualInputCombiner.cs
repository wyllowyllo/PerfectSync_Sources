using UnityEngine;

namespace Core.Utilities
{
    public static class DualInputCombiner
    {
        // 두 플레이어가 같은 방향(θ=0)일 때의 최종 배율 지정.
        // 솔로 입력(|sum|=1)은 항상 1로 보존되고, |sum|>1 구간만 선형 리매핑됨.
        public static Vector3 Combine(Vector3 inputA, Vector3 inputB, float dualBoostAtFullAlign)
        {
            Vector3 sum = inputA + inputB;
            float mag = sum.magnitude;
            if (mag <= 1f) return sum;

            float scaled = 1f + (mag - 1f) * (dualBoostAtFullAlign - 1f);
            return sum * (scaled / mag);
        }
    }
}
