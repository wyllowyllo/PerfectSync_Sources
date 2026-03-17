using Player.Test;
using Player.UserInput;
using UnityEngine;

namespace Player.Controller
{
    [RequireComponent(typeof(LocalPlayerInput), typeof(AvatarController))]
    public class TeamController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;

        [Header("Cameras")]
        [SerializeField] private TpsCameraController _cameraControllerA;
        [SerializeField] private TpsCameraController _cameraControllerB;

        private IPlayerInput _playerInputA;
        private IPlayerInput _playerInputB;
        private AvatarController avatarController;
        private Transform _cameraTransformA;
        private Transform _cameraTransformB;

        private void Start()
        {
            _playerInputA = GetComponent<LocalPlayerInput>();
            avatarController = GetComponent<AvatarController>();

            if (_cameraControllerA == null)
                _cameraControllerA = FindAnyObjectByType<TpsCameraController>();

            _cameraTransformA = _cameraControllerA != null
                ? _cameraControllerA.transform
                : Camera.main.transform;

            _cameraTransformB = _cameraControllerB != null
                ? _cameraControllerB.transform
                : null;

            avatarController.OnModeChanged += HandleModeChanged;
            avatarController.Initialize(_startMode);

            if (_cameraControllerA != null)
                _cameraControllerA.SetTarget(avatarController.PrimaryBodyTransform);

            if (_cameraControllerB != null)
                _cameraControllerB.SetTarget(avatarController.SecondaryBodyTransform);

            TeamModeManager.Instance.OnSwitchRequested += HandleSwitchRequested;
        }

        private void Update()
        {
            avatarController.Tick();
            RouteInput();
        }

        private void HandleSwitchRequested()
        {
            avatarController.ToggleMode();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            if (_cameraControllerA != null)
                _cameraControllerA.SetTarget(avatarController.PrimaryBodyTransform);

            if (_cameraControllerB != null)
                _cameraControllerB.SetTarget(avatarController.SecondaryBodyTransform);
        }

        private void RouteInput()
        {
            if (_playerInputA != null && !_playerInputA.IsOwner)
                return;

            Vector2 rawA = _playerInputA != null ? _playerInputA.MoveInput : Vector2.zero;
            bool jumpA = _playerInputA != null && _playerInputA.JumpPressed;

            Vector2 rawB = _playerInputB != null ? _playerInputB.MoveInput : Vector2.zero;
            bool jumpB = _playerInputB != null && _playerInputB.JumpPressed;

            Vector3 worldDirA = CameraRelativeConverter.Convert(rawA, _cameraTransformA);
            Vector3 worldDirB = CameraRelativeConverter.Convert(rawB, _cameraTransformB);

            avatarController.ApplyInput(worldDirA, worldDirB, jumpA, jumpB);
        }
    }
}
