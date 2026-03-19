using InGame.Camera.PlayerCamera;
using InGame.UserInput;
using PlayerSystem.Domain;
using PlayerSystem.Form;
using PlayerSystem.Common;
using PlayerSystem.Test;
using UnityEngine;

namespace PlayerSystem.PlayerInput
{
    [RequireComponent(typeof(LocalPlayerInput), typeof(PlayerFormController))]
    public class PlayerInputRouter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;

        [Header("Cameras")]
        [SerializeField] private TpsCameraController _cameraControllerA;
        [SerializeField] private TpsCameraController _cameraControllerB;

        private IPlayerInput _playerInputA;
        private IPlayerInput _playerInputB;
        private PlayerFormController _playerFormController;
        private Transform _cameraTransformA;
        private Transform _cameraTransformB;

        private void Start()
        {
            _playerInputA = GetComponent<LocalPlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();

            if (_cameraControllerA == null)
                _cameraControllerA = FindAnyObjectByType<TpsCameraController>();

            _cameraTransformA = _cameraControllerA != null
                ? _cameraControllerA.transform
                : Camera.main.transform;

            _cameraTransformB = _cameraControllerB != null
                ? _cameraControllerB.transform
                : null;

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            if (_cameraControllerA != null)
                _cameraControllerA.SetTarget(_playerFormController.PrimaryCameraFollowPoint);

            if (_cameraControllerB != null)
                _cameraControllerB.SetTarget(_playerFormController.SecondaryCameraFollowPoint);

            TeamModeManager.Instance.OnSwitchRequested += HandleSwitchRequested;
        }
        

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (TeamModeManager.Instance != null)
                TeamModeManager.Instance.OnSwitchRequested -= HandleSwitchRequested;
        }

        private void Update()
        {
            _playerFormController.Tick();
            RouteInput();
        }

        private void HandleSwitchRequested()
        {
            _playerFormController.ToggleMode();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            if (_cameraControllerA != null)
                _cameraControllerA.SetTarget(_playerFormController.PrimaryCameraFollowPoint);

            if (_cameraControllerB != null)
                _cameraControllerB.SetTarget(_playerFormController.SecondaryCameraFollowPoint);
        }

        private void RouteInput()
        {
            if (_playerInputA != null && !_playerInputA.IsOwner)
                return;

            Vector2 inputA = _playerInputA != null ? _playerInputA.MoveInput : Vector2.zero;
            bool jumpA = _playerInputA != null && _playerInputA.JumpPressed;

            Vector2 inputB = _playerInputB != null ? _playerInputB.MoveInput : Vector2.zero;
            bool jumpB = _playerInputB != null && _playerInputB.JumpPressed;

            Vector3 worldDirA = CameraRelativeConverter.Convert(inputA, _cameraTransformA);
            Vector3 worldDirB = CameraRelativeConverter.Convert(inputB, _cameraTransformB);

            _playerFormController.ApplyInput(worldDirA, worldDirB, jumpA, jumpB);
        }
    }
}
