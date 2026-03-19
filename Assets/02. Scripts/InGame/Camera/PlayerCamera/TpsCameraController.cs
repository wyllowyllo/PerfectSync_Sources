using UnityEngine;

namespace PlayerSystem.CameraSystem
{
    public class TpsCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1f, 0f);

        [Header("Orbit")]
        [SerializeField] private float _distance = 4f;
        [SerializeField] private Vector2 _pitchRange = new Vector2(-20f, 60f);

        [Header("Mouse Sensitivity")]
        [SerializeField] private float _horizontalSensitivity = 3f;
        [SerializeField] private float _verticalSensitivity = 2f;
        [SerializeField] private bool _invertY;

        private float _yaw;
        private float _pitch = 20f;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_target == null)
                return;

            Vector3 dir = transform.position - (_target.position + _targetOffset);
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            float mouseX = Input.GetAxis("Mouse X") * _horizontalSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * _verticalSensitivity;

            _yaw += mouseX;
            _pitch += _invertY ? mouseY : -mouseY;
            _pitch = Mathf.Clamp(_pitch, _pitchRange.x, _pitchRange.y);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = _target.position + _targetOffset;
            transform.position = pivot - rotation * Vector3.forward * _distance;
            transform.rotation = rotation;
        }

        public void SetTarget(Transform newTarget)
        {
            _target = newTarget;
        }
    }
}
