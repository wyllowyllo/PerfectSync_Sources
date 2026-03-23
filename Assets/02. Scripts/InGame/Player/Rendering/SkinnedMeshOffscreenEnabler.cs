using UnityEngine;

namespace InGame.Player.Rendering
{
    // SkeletonRoot보다 상위 오브젝트에 부착.
    // 하위의 모든 SkinnedMeshRenderer에 updateWhenOffscreen = true를 설정하여
    // 래그돌 중 본이 퍼져도 컬링되지 않도록 보장.
    public class SkinnedMeshOffscreenEnabler : MonoBehaviour
    {
        private void Awake()
        {
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;
        }
    }
}
