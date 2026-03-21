using InGame.Player.Ragdoll;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    public class CameraTarget : MonoBehaviour
    {
        [SerializeField] private float _smoothSpeed = 10f;
        [SerializeField] private Transform _visualPelvis;
        [SerializeField] private Transform _ragdollPelvis;
        [SerializeField] private RagdollStateMachine _ragdollController;

        private Transform _followPoint;

        public Transform FollowPoint => _followPoint;

        private void Awake()
        {
            var followObj = new GameObject("CameraFollowPoint");
            followObj.transform.SetParent(transform);
            followObj.transform.localPosition = Vector3.zero;
            _followPoint = followObj.transform;
        }

        private void LateUpdate()
        {
            Transform target = _ragdollController != null && _ragdollController.IsPhysicsRagdoll
                ? _ragdollPelvis
                : _visualPelvis;

            float t = 1f - Mathf.Exp(-_smoothSpeed * Time.deltaTime);
            _followPoint.position = Vector3.Lerp(_followPoint.position, target.position, t);
        }
    }
}
