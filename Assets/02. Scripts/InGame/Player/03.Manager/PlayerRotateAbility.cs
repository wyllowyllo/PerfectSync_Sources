using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerRotateAbility : PlayerAbility
{
    [Header("References")]
    [SerializeField] private Transform _cameraRoot;

    [Header("Sensitivity")]
    [SerializeField] private float _rotationSpeed = 200f;

    private float _yaw;
    private float _pitch;

    public void SetFollowCamera(CinemachineCamera vcam)
    {
        if (vcam != null)
            vcam.Follow = _cameraRoot;
    }

    private void Start()
    {
        if (!Owner.PhotonView.IsMine) return;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!Owner.PhotonView.IsMine) return;
        if (!InGameManager.IsLocalPlayerControllable) return;

        _yaw += Input.GetAxis("Mouse X") * _rotationSpeed * Time.deltaTime;
        _pitch -= Input.GetAxis("Mouse Y") * _rotationSpeed * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -90f, 90f);

        transform.eulerAngles = new Vector3(0f, _yaw, 0f);
        _cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
