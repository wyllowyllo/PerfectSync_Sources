using InGame.Camera.PlayerCamera;
using Unity.Cinemachine;
using UnityEngine;

public class PausePopupController : MonoBehaviour
{
    [SerializeField] private GameObject _pausePopup;

    private CinemachineInputAxisController _cameraInput;
    private bool _cameraInputResolved;

    private void Start()
    {
        ApplyModalState(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePopup();
        }
    }

    public void OpenPopup()
    {
        SetPopupActive(true);
    }

    public void ClosePopup()
    {
        SetPopupActive(false);
    }

    public void TogglePopup()
    {
        if (_pausePopup == null) return;
        SetPopupActive(!_pausePopup.activeSelf);
    }

    private void SetPopupActive(bool active)
    {
        if (_pausePopup != null)
            _pausePopup.SetActive(active);

        ApplyModalState(active);
    }

    private void ApplyModalState(bool modal)
    {
        Cursor.lockState = modal ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = modal;

        var input = ResolveCameraInput();
        if (input != null)
            input.enabled = !modal;
    }

    private CinemachineInputAxisController ResolveCameraInput()
    {
        if (_cameraInputResolved) return _cameraInput;

        var manager = InGameCameraManager.Instance;
        if (manager == null || manager.FollowCamera == null)
            return null;

        _cameraInput = manager.FollowCamera.GetComponent<CinemachineInputAxisController>();
        _cameraInputResolved = true;
        return _cameraInput;
    }
}
