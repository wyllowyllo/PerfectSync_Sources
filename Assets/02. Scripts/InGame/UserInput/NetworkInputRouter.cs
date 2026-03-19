using Core.Utilities;
using InGame.Camera.PlayerCamera;
using InGame.Player;
using InGame.Player.Movement;
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

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        private IPlayerInput _playerInputA;
        private IPlayerInput _playerInputB;
        private PlayerFormController _playerFormController;
        private NetworkPlayerInput _networkPlayerInput;
        private RemotePlayerInput _remotePlayerInput;
        private Transform _cameraTransformA;
        private Transform _cameraTransformB;
        private bool _isHost;

        // Guest RPC 쓰로틀링
        private Vector2 _lastSentMove;
        private bool _pendingJump;
        private float _lastSendTime;
        private const float MinSendInterval = 0.05f; // 최대 20Hz

        // 애니메이션 트리거 감지
        private PlayerJump _activeJumpA;
        private PlayerJump _activeJumpB;
        private BodyPositionSync _activeSyncA;
        private BodyPositionSync _activeSyncB;
        private bool _prevDivingA;
        private bool _prevDivingB;
        private bool _prevGroundedA;
        private bool _prevGroundedB;

        private void Start()
        {
            _networkPlayerInput = GetComponent<NetworkPlayerInput>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();

            _isHost = NetworkTestManager.Instance != null && NetworkTestManager.Instance.IsHost;

            AssignInputsByRole();
            SetupCameras();
            InitializeNetworkBodyControllers();

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            SetCameraTargetByRole();
            CacheActiveBodyComponents();

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

                // 트리거 상태 캡처 (RouteInput 전)
                bool wasDivingA = _activeJumpA != null && _activeJumpA.IsDiving;
                bool wasDivingB = _activeJumpB != null && _activeJumpB.IsDiving;

                RouteInput();

                // 트리거 감지 및 전송 (RouteInput 후)
                DetectAndSendTriggers(wasDivingA, wasDivingB);
            }
            else
            {
                // Guest: 로컬 입력을 읽어 Host에게 RPC 전달
                SendLocalInputToHost();
            }
        }

        private void InitializeNetworkBodyControllers()
        {
            bool isRemote = !_isHost;
            SetRemoteOnBody(_mergedBody, isRemote);
            SetRemoteOnBody(_avatarA, isRemote);
            SetRemoteOnBody(_avatarB, isRemote);
        }

        private void SetRemoteOnBody(GameObject body, bool isRemote)
        {
            if (body == null) return;
            var controller = body.GetComponent<NetworkBodyController>();
            if (controller != null)
                controller.SetRemote(isRemote);
        }

        private void CacheActiveBodyComponents()
        {
            if (_mergedBody != null)
            {
                _activeJumpA = _mergedBody.GetComponent<PlayerJump>();
                _activeSyncA = _mergedBody.GetComponent<BodyPositionSync>();
            }
            if (_avatarA != null)
            {
                // Separated 모드에서 사용
            }
            if (_avatarB != null)
            {
                // Separated 모드에서 사용
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
            SetCameraTargetByRole();
            UpdateActiveBodyReferences(newMode);
        }

        private void UpdateActiveBodyReferences(ETeamMode mode)
        {
            switch (mode)
            {
                case ETeamMode.Merged:
                    _activeJumpA = _mergedBody != null ? _mergedBody.GetComponent<PlayerJump>() : null;
                    _activeSyncA = _mergedBody != null ? _mergedBody.GetComponent<BodyPositionSync>() : null;
                    _activeJumpB = null;
                    _activeSyncB = null;
                    break;

                case ETeamMode.Separated:
                    _activeJumpA = _avatarA != null ? _avatarA.GetComponent<PlayerJump>() : null;
                    _activeSyncA = _avatarA != null ? _avatarA.GetComponent<BodyPositionSync>() : null;
                    _activeJumpB = _avatarB != null ? _avatarB.GetComponent<PlayerJump>() : null;
                    _activeSyncB = _avatarB != null ? _avatarB.GetComponent<BodyPositionSync>() : null;
                    break;
            }

            _prevDivingA = false;
            _prevDivingB = false;
            _prevGroundedA = true;
            _prevGroundedB = true;
        }

        private void SetCameraTargetByRole()
        {
            if (_cameraControllerA == null) return;

            var target = _isHost
                ? _playerFormController.PrimaryCameraFollowPoint
                : _playerFormController.SecondaryCameraFollowPoint;

            _cameraControllerA.SetTarget(target);
        }

        /// <summary>
        /// Guest가 로컬 입력을 읽어 Host에게 RPC로 전달한다 (쓰로틀링 적용).
        /// </summary>
        private void SendLocalInputToHost()
        {
            Vector2 moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            _pendingJump |= Input.GetButtonDown("Jump");

            bool inputChanged = moveInput != _lastSentMove || _pendingJump;
            bool intervalElapsed = Time.time - _lastSendTime >= MinSendInterval;

            if (inputChanged && intervalElapsed)
            {
                photonView.RPC(nameof(RpcGuestInput), RpcTarget.MasterClient, moveInput, _pendingJump);
                _lastSentMove = moveInput;
                _pendingJump = false;
                _lastSendTime = Time.time;
            }
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

        private void DetectAndSendTriggers(bool wasDivingA, bool wasDivingB)
        {
            // Body A 트리거 감지
            if (_activeJumpA != null && _activeSyncA != null)
            {
                bool isDivingA = _activeJumpA.IsDiving;

                // Dive 시작 감지
                if (!wasDivingA && isDivingA)
                    _activeSyncA.SendAnimTrigger(1); // Dive

                // DiveLand 감지 (Diving → Not Diving while grounded)
                if (wasDivingA && !isDivingA)
                    _activeSyncA.SendAnimTrigger(2); // DiveLand

                // Jump 감지: 점프는 PlayerJump.Jump()에서 발생하므로
                // isDiving이 아닌 상태에서 y속도 변화로 감지
                var movementA = _activeJumpA.GetComponent<PlayerMovement>();
                if (movementA != null)
                {
                    bool groundedA = movementA.Grounded;
                    if (_prevGroundedA && !groundedA && !isDivingA)
                        _activeSyncA.SendAnimTrigger(0); // Jump
                    _prevGroundedA = groundedA;
                }
            }

            // Body B 트리거 감지
            if (_activeJumpB != null && _activeSyncB != null)
            {
                bool isDivingB = _activeJumpB.IsDiving;

                if (!wasDivingB && isDivingB)
                    _activeSyncB.SendAnimTrigger(1);

                if (wasDivingB && !isDivingB)
                    _activeSyncB.SendAnimTrigger(2);

                var movementB = _activeJumpB.GetComponent<PlayerMovement>();
                if (movementB != null)
                {
                    bool groundedB = movementB.Grounded;
                    if (_prevGroundedB && !groundedB && !isDivingB)
                        _activeSyncB.SendAnimTrigger(0);
                    _prevGroundedB = groundedB;
                }
            }
        }
    }
}
