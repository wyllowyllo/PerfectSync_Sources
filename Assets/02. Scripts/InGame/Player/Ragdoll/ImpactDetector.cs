using InGame.Player.Network;
using InGame.UserInput;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class ImpactDetector : MonoBehaviour
    {
        private const float TorqueScaleFactor = 0.15f;

        [SerializeField] private LayerMask _hazardLayers;
        [SerializeField] private float _minImpulse = 3f;
        [SerializeField] private PhotonView _photonView;
        [SerializeField] private BodySimulationToggle _bodyToggle;

        private LocalPlayerInput _localPlayerInput;

        private void Awake()
        {
            _localPlayerInput = GetComponentInParent<LocalPlayerInput>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_localPlayerInput == null) return;
            if (_photonView != null && !_photonView.IsMine) return;
            if (_bodyToggle != null && _bodyToggle.IsRemote) return;

            if ((_hazardLayers & (1 << collision.gameObject.layer)) == 0) return;

            Vector3 impulse = collision.relativeVelocity;
            if (impulse.sqrMagnitude < _minImpulse * _minImpulse) return;

            Vector3 hitPoint = collision.GetContact(0).point;
            Vector3 torque = Random.insideUnitSphere * impulse.magnitude * TorqueScaleFactor;
            _localPlayerInput.SendImpact(impulse, hitPoint, _photonView.ViewID, torque);
        }
    }
}
