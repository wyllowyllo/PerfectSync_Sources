using InGame.Player.Network;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// 키 입력 테스트용 매니저.
    /// Return 키로 무적 모드를 토글한다.
    /// </summary>
    public class ModeSwitchTestManager : MonoBehaviour
    {
        private TeamModeSynchronizer _synchronizer;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Return)) return;

            if (InGameManager.Instance != null && !InGameManager.IsLocalPlayerControllable)
                return;

            if (_synchronizer == null)
                _synchronizer = FindLocalSynchronizer();

            if (_synchronizer == null)
            {
                Debug.LogWarning("[ModeSwitchTest] TeamModeSynchronizer를 찾을 수 없습니다.");
                return;
            }

            var controller = _synchronizer.GetComponent<Team.InvincibleModeController>();
            bool next = controller == null || !controller.IsInvincible;

            Debug.Log($"[ModeSwitchTest] 무적 모드 {(next ? "ON" : "OFF")}");
            _synchronizer.BroadcastInvincibleMode(next);
        }

        private static TeamModeSynchronizer FindLocalSynchronizer()
        {
            foreach (var sync in FindObjectsByType<TeamModeSynchronizer>(FindObjectsSortMode.None))
            {
                if (sync.photonView != null && sync.photonView.IsMine)
                    return sync;
            }
            return null;
        }
    }
}
