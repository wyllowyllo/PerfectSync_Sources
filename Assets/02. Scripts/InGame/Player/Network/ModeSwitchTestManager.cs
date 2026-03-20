using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    /// <summary>
    /// 멀티 환경 모드 전환 테스트용 매니저.
    /// 씬에 배치하고 Input Manager의 "ModeSwitch" 버튼으로 합체/분리 전환을 트리거한다.
    /// 테스트 완료 후 제거할 것.
    /// </summary>
    public class ModeSwitchTestManager : MonoBehaviour
    {
        private TeamModeSynchronizer _synchronizer;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Return)) return;

            if (_synchronizer == null)
                _synchronizer = FindMyTeamSynchronizer();

            if (_synchronizer == null)
            {
                Debug.LogWarning("[ModeSwitchTest] TeamModeSynchronizer not found for my team.");
                return;
            }

            _synchronizer.RequestSwitch();
            Debug.Log("[ModeSwitchTest] Mode switch requested.");
        }

        private TeamModeSynchronizer FindMyTeamSynchronizer()
        {
            if (PhotonTeamManager.Instance == null) return null;

            int myTeam = PhotonTeamManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);
            if (myTeam == PhotonTeamManager.TeamNone) return null;

            foreach (var sync in FindObjectsByType<TeamModeSynchronizer>(FindObjectsSortMode.None))
            {
                var owner = sync.photonView.Owner;
                if (owner != null && PhotonTeamManager.Instance.GetPlayerTeam(owner) == myTeam)
                    return sync;
            }

            return null;
        }
    }
}
