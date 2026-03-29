using InGame.Player;
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
