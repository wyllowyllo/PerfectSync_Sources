using PlayerSystem.Ragdoll;
using UnityEngine;

namespace PlayerSystem.CameraSystem
{
    [RequireComponent(typeof(RagdollPhysicsToggle))]
    public class CameraTarget : MonoBehaviour
    {
        [SerializeField] private float _smoothSpeed = 10f;

        private Transform _followPoint;
        private Transform _hipsRoot;

        public Transform FollowPoint => _followPoint;

        private void Awake()
        {
            var followObj = new GameObject("CameraFollowPoint");
            followObj.transform.SetParent(transform);
            followObj.transform.localPosition = Vector3.zero;
            _followPoint = followObj.transform;

            var physicsToggle = GetComponent<RagdollPhysicsToggle>();
            _hipsRoot = physicsToggle.HipsRoot;
        }

        private void LateUpdate()
        {
            float t = 1f - Mathf.Exp(-_smoothSpeed * Time.deltaTime);
            _followPoint.position = Vector3.Lerp(_followPoint.position, _hipsRoot.position, t);
        }
    }
}
