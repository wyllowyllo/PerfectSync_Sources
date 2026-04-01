using System;
using Photon.Pun;

namespace InGame.Player.Network
{
    /// <summary>
    /// 슬롯머신 RPC 브로드캐스트 담당 클래스.
    /// </summary>
    public class TeamModeSynchronizer : MonoBehaviourPun
    {
        public event Action<int[], bool> OnSlotSpinReceived;

        // ── 슬롯머신 스핀 ──────────────────────────────────────

        public void BroadcastSlotSpin(int[] symbols, bool isMatch)
        {
            photonView.RPC(nameof(RpcSlotSpin), RpcTarget.All,
                symbols[0], symbols[1], symbols[2], isMatch);
        }

        [PunRPC]
        private void RpcSlotSpin(int s0, int s1, int s2, bool isMatch)
        {
            int[] symbols = { s0, s1, s2 };
            OnSlotSpinReceived?.Invoke(symbols, isMatch);
        }
    }
}
