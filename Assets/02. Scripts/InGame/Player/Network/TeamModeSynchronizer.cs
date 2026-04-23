using System;
using InGame.Player.Ragdoll;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Network
{
    /// <summary>
    /// 슬롯머신 및 무적 모드 RPC 브로드캐스트 담당 클래스.
    /// </summary>
    public class TeamModeSynchronizer : MonoBehaviourPun
    {
        // ── 슬롯머신 스핀 시작 (결과 미정) ──────────────────────

        public event Action OnSlotSpinStarted;

        public void BroadcastSlotSpinStart()
        {
            photonView.RPC(nameof(RpcSlotSpinStart), RpcTarget.All);
        }

        [PunRPC]
        private void RpcSlotSpinStart()
        {
            OnSlotSpinStarted?.Invoke();
        }

        // ── 슬롯머신 결과 확정 ──────────────────────────────────

        public event Action<int[], bool> OnSlotResultReceived;

        public void BroadcastSlotResult(int[] symbols, bool isMatch)
        {
            photonView.RPC(nameof(RpcSlotResult), RpcTarget.All,
                symbols[0], symbols[1], symbols[2], isMatch);
        }

        [PunRPC]
        private void RpcSlotResult(int s0, int s1, int s2, bool isMatch)
        {
            int[] symbols = { s0, s1, s2 };
            OnSlotResultReceived?.Invoke(symbols, isMatch);
        }

        // ── 슬롯머신 종료 (착지 후 제거) ────────────────────────

        public event Action OnSlotFinishReceived;

        public void BroadcastSlotFinish()
        {
            photonView.RPC(nameof(RpcSlotFinish), RpcTarget.All);
        }

        [PunRPC]
        private void RpcSlotFinish()
        {
            OnSlotFinishReceived?.Invoke();
        }

        // ── 무적 모드 상태 변경 ─────────────────────────────────

        public event Action<bool> OnInvincibleModeChanged;

        public void BroadcastInvincibleMode(bool active)
        {
            photonView.RPC(nameof(RpcSetInvincibleMode), RpcTarget.All, active);
        }

        [PunRPC]
        private void RpcSetInvincibleMode(bool active)
        {
            OnInvincibleModeChanged?.Invoke(active);
        }

        // ── 무적 넉백 (가해자 → 피해자) ─────────────────────────

        /// <summary>RPC 수신 시 넉백 방향을 전달. 공격자 측 피드백 연출용.</summary>
        public event Action<Vector3> OnInvincibleHitApplied;

        public void BroadcastInvincibleHit(int victimViewID, Vector3 knockback, Vector3 hitPoint, Vector3 torque, byte response)
        {
            photonView.RPC(nameof(RpcInvincibleHit), RpcTarget.All,
                victimViewID, knockback, hitPoint, torque, response);
        }

        [PunRPC]
        private void RpcInvincibleHit(int victimViewID, Vector3 knockback, Vector3 hitPoint, Vector3 torque, byte response)
        {
            var victimView = PhotonView.Find(victimViewID);
            if (victimView == null) return;

            var ragdoll = victimView.GetComponentInChildren<RagdollStateMachine>();
            if (ragdoll == null) return;

            var hit = new HitData(knockback, hitPoint, torque, (EHitResponse)response);
            ragdoll.ApplyHit(hit);

            // 피해자 측 피격 연출용 경로. HitDetector.OnHitDetected를 우회하므로 별도 통지.
            var victimHitDetector = victimView.GetComponentInChildren<HitDetector>();
            victimHitDetector?.NotifyInvincibleHit(hit);

            OnInvincibleHitApplied?.Invoke(knockback.normalized);
        }

        // ── 무적 장애물 파괴 피드백 ─────────────────────────────

        /// <summary>RPC 수신 시 파괴 방향 전달. 비-authority 측 연출 릴레이용.</summary>
        public event Action<Vector3> OnObstacleDestroyFeedback;

        public void BroadcastObstacleDestroyFeedback(Vector3 direction)
        {
            photonView.RPC(nameof(RpcObstacleDestroyFeedback), RpcTarget.All, direction);
        }

        [PunRPC]
        private void RpcObstacleDestroyFeedback(Vector3 direction)
        {
            OnObstacleDestroyFeedback?.Invoke(direction);
        }
    }
}
