using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// 키 입력 테스트용 매니저.
    /// Return 키로 기능을 트리거한다.
    /// 향후 다른 모드 on/off 용도로 사용 가능.
    /// </summary>
    public class ModeSwitchTestManager : MonoBehaviour
    {
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Return)) return;

            if (InGameManager.Instance != null && !InGameManager.IsLocalPlayerControllable)
                return;

            // TODO: 향후 기능 연결
            Debug.Log("[ModeSwitchTest] Return key pressed.");
        }
    }
}
