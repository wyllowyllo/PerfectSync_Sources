using Core.Utilities;
using InGame.Camera.PlayerCamera;
using InGame.Player;
using InGame.Player.Network;
using InGame.Team._02._Domain;
using Photon.Pun;
using UnityEngine;

namespace InGame.UserInput
{
    [RequireComponent(typeof(NetworkPlayerInput), typeof(RemotePlayerInput), typeof(PlayerFormController))]
    public class NetworkInputRouter : MonoBehaviourPun
    {
        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;

        [Header("Cameras")]
        [SerializeField] private TpsCameraController _cameraControllerA;
        [SerializeField] private TpsCameraController _cameraControllerB;

        private IPlayerInput _playerInputA;
        private IPlayerInput _playerInputB;
        private PlayerFormController _playerFormController;
        private NetworkPlayerInput _networkPlayerInput;
        private RemotePlayerInput _remotePlayerInput;
        private Transform _cameraTransformA;
        private Transform _cameraTransformB;
        private bool _isHost;

        private void Start()
        {
            _networkPlayerInput = GetComponent<NetworkPlayerInput>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();

            _isHost = NetworkTestManager.Instance != null && NetworkTestManager.Instance.IsHost;

            AssignInputsByRole();
            SetupCameras();

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            if (_cameraControllerA != null)
                _cameraControllerA.SetTarget(_playerFormController.PrimaryCameraFollowPoint);

            if (_cameraControllerB != null)
                _cameraControllerB.SetTarget(_playerFormController.SecondaryCameraFollowPoint);

            if (NetworkTeamModeManager.Instance != null)
                NetworkTeamModeManager.Instance.OnSwitchRequested += HandleSwitchRequested;
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (NetworkTeamModeManager.Instance != null)
                NetworkTeamModeManager.Instance.OnSwitchRequested -= HandleSwitchRequested;
        }

        private void Update()
        {
            if (_isHost)
            {
                // Host: 게임 로직 실행 (물리 시뮬레이션의 권한자)
                _playerFormController.Tick();
                RouteInput();
            }
            else
            {
                // Guest: 로컬 입력을 읽어 Host에게 RPC 전달
                SendLocalInputToHost();
            }
        }

        private void AssignInputsByRole()
        {
            if (_isHost)
            {
                // Host: A = 로컬 입력(NetworkPlayerInput), B = Guest 입력(RemotePlayerInput)
                _playerInputA = _networkPlayerInput;
                _playerInputB = _remotePlayerInput;
            }
            else
            {
                // Guest: A = Host 입력(RemotePlayerInput), B = 로컬 입력(NetworkPlayerInput)
                _playerInputA = _remotePlayerInput;
                _playerInputB = _networkPlayerInput;
            }

            Debug.Log($"[NetworkInputRouter] Input assigned - IsHost: {_isHost}, " +
                      $"InputA: {_playerInputA.GetType().Name}, InputB: {_playerInputB.GetType().Name}");
        }

        private void SetupCameras()
        {
            if (_cameraControllerA == null)
                _cameraControllerA = FindAnyObjectByType<TpsCameraController>();

            _cameraTransformA = _cameraControllerA != null
                ? _cameraControllerA.transform
                : UnityEngine.Camera.main.transform;

            _cameraTransformB = _cameraControllerB != null
                ? _cameraControllerB.transform
                : null;
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

        /// <summary>
        /// Guest가 로컬 입력을 읽어 Host에게 RPC로 전달한다.
        /// </summary>
        private void SendLocalInputToHost()
        {
            Vector2 moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            bool jump = Input.GetButtonDown("Jump");

            photonView.RPC(nameof(RpcGuestInput), RpcTarget.MasterClient, moveInput, jump);
        }

        /// <summary>
        /// Host에서 수신: Guest의 입력을 RemotePlayerInput에 주입한다.
        /// </summary>
        [PunRPC]
        private void RpcGuestInput(Vector2 moveInput, bool jump)
        {
            _remotePlayerInput.SetInput(moveInput, jump);
        }

        private void RouteInput()
        {
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
