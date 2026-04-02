using InGame.Player.Ragdoll;
using InGame.UserInput;
using Photon.Pun;
using UnityEngine;

namespace InGame.Gimmick
{
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            var photonView = other.GetComponentInParent<PhotonView>();
            if (photonView == null || !photonView.IsMine) return;

            // Dead 상태면 중복 사망 방지.
            var ragdoll = other.GetComponentInParent<RagdollStateMachine>();
            if (ragdoll != null && ragdoll.CurrentState == ERagdollState.Dead) return;

            var input = other.GetComponentInParent<LocalPlayerInput>();
            if (input == null) return;

            input.SendDeath();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            var col = GetComponent<BoxCollider>();
            if (col != null)
                Gizmos.DrawCube(transform.position + col.center, col.size);
        }
    }
}
