using InGame.Player;
using InGame.Team;
using Photon.Pun;
using UnityEngine;

namespace InGame.Gimmick
{
    public class TrampolineTrigger : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _targetPoint;

        private void OnCollisionEnter(Collision collision)
        {
            var formController = collision.collider.GetComponentInParent<PlayerFormController>();
            if (formController == null) return;

            formController.LaunchAllBodies(_targetPoint.position);

            var photonView = formController.GetComponent<PhotonView>();
            if (photonView == null || photonView.Owner == null) return;

            int teamNumber = PhotonTeamManager.GetTeamRaw(photonView.Owner);
            if (teamNumber == PhotonTeamManager.TeamNone) return;

            if (TeamModeManager.Instance != null)
                TeamModeManager.Instance.HandleTrampolineTrigger(teamNumber);
        }

        private void OnDrawGizmos()
        {
            if (_targetPoint == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_targetPoint.position, 0.5f);
            Gizmos.DrawLine(transform.position, _targetPoint.position);
        }
    }
}
