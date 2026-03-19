using Core.Utilities;
using InGame.Camera.PlayerCamera;
using InGame.Player;
using InGame.Player.Movement;
using InGame.Player.Network;
using InGame.Player.Ragdoll;
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

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        private IPlayerInput _playerInputA;
        private IPlayerInput _playerInputB;
        private PlayerFormController _playerFormController;
        private NetworkPlayerInput _networkPlayerInput;
        private RemotePlayerInput _remotePlayerInput;
        private NetworkTeamModeManager _teamModeManager;
        private Transform _cameraTransformA;
        private bool _isHost;
        private ETeamMode _currentMode;

        // 입력 RPC 쓰로틀링
        private bool _pendingJump;
        private float _lastSendTime;
        private const float MinSendInterval = 0.05f; // 최대 20Hz

        private void Start()
        {
            _networkPlayerInput = GetComponent<NetworkPlayerInput>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();
            _teamModeManager = GetComponent<NetworkTeamModeManager>();

            _isHost = photonView.IsMine;
            _currentMode = _startMode;

            AssignInputsByRole();
            SetupCameras();

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            SetCameraTargetByRole();

            // 초기 바디 시뮬레이션 설정 (OnEnable 자동 평가 대체)
            RefreshBodySimulation(_startMode);
            RefreshSyncBridgeMode(_startMode);

            // 모드 전환 이벤트 구독
            if (_teamModeManager != null)
                _teamModeManager.OnSwitchRequested += HandleSwitchRequested;

            // Impact/Death 이벤트를 현재 활성 바디의 RagdollController에 연결
            _networkPlayerInput.OnImpactReceived += HandleImpact;
            _networkPlayerInput.OnDeathReceived += HandleDeath;
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_teamModeManager != null)
                _teamModeManager.OnSwitchRequested -= HandleSwitchRequested;

            if (_networkPlayerInput != null)
            {
                _networkPlayerInput.OnImpactReceived -= HandleImpact;
                _networkPlayerInput.OnDeathReceived -= HandleDeath;
            }
        }

        private void Update()
        {
            _playerFormController.Tick();
            RouteInput();
            SendLocalInput();
        }

        /// <summary>
        /// 바디별 물리 시뮬레이션 활성화/비활성화를 모드에 따라 설정한다.
        /// 합체 모드: 양쪽 모두 MergedBody 시뮬레이션 실행
        /// 분리 모드: 양쪽 모두 AvatarA/B 시뮬레이션 실행 (고무줄/래그돌을 위해)
        /// </summary>
        private void RefreshBodySimulation(ETeamMode mode)
        {
            switch (mode)
            {
                case ETeamMode.Merged:
                    // 양쪽 모두 MergedBody 물리 시뮬레이션 실행
                    SetRemoteOnBody(_mergedBody, false);
                    SetRemoteOnBody(_avatarA, true);
                    SetRemoteOnBody(_avatarB, true);
                    break;

                case ETeamMode.Separated:
                    SetRemoteOnBody(_mergedBody, true);
                    SetRemoteOnBody(_avatarA, false);  // 양쪽 모두 로컬 시뮬
                    SetRemoteOnBody(_avatarB, false);  // 양쪽 모두 로컬 시뮬
                    break;
            }
        }

        /// <summary>
        /// BodySyncBridge의 IPunObservable 위치 보정을 모드에 따라 활성화/비활성화한다.
        /// 합체 모드: MergedBody만 동기화
        /// 분리 모드: AvatarA/B 각각 동기화 (소유자가 write, 비소유자가 lerp)
        /// </summary>
        private void RefreshSyncBridgeMode(ETeamMode mode)
        {
            var mergedBridge = _mergedBody != null ? _mergedBody.GetComponent<BodySyncBridge>() : null;
            var bridgeA = _avatarA != null ? _avatarA.GetComponent<BodySyncBridge>() : null;
            var bridgeB = _avatarB != null ? _avatarB.GetComponent<BodySyncBridge>() : null;

            switch (mode)
            {
                case ETeamMode.Merged:
                    mergedBridge?.SetSyncEnabled(true);
                    bridgeA?.SetSyncEnabled(false);
                    bridgeB?.SetSyncEnabled(false);
                    break;

                case ETeamMode.Separated:
                    mergedBridge?.SetSyncEnabled(false);
                    bridgeA?.SetSyncEnabled(true);   // AvatarA: Host가 write, Guest가 lerp
                    bridgeB?.SetSyncEnabled(true);   // AvatarB: Guest가 write, Host가 lerp
                    break;
            }
        }

        private void SetRemoteOnBody(GameObject body, bool isRemote)
        {
            if (body == null) return;
            var controller = body.GetComponent<NetworkBodyController>();
            if (controller != null)
                controller.SetRemote(isRemote);
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
        }

        private void HandleSwitchRequested()
        {
            _playerFormController.ToggleMode();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            _currentMode = newMode;
            SetCameraTargetByRole();
            RefreshBodySimulation(newMode);
            RefreshSyncBridgeMode(newMode);
        }

        private void SetCameraTargetByRole()
        {
            if (_cameraControllerA == null) return;

            var target = _isHost
                ? _playerFormController.PrimaryCameraFollowPoint
                : _playerFormController.SecondaryCameraFollowPoint;

            _cameraControllerA.SetTarget(target);
        }

        #region Impact / Death 이벤트 처리

        private void HandleImpact(Vector3 impulse, Vector3 hitPoint)
        {
            var ragdoll = GetActiveRagdollController();
            ragdoll?.OnHitImpact(impulse, hitPoint);
        }

        private void HandleDeath()
        {
            var ragdoll = GetActiveRagdollController();
            ragdoll?.EnterDead();
        }

        private RagdollController GetActiveRagdollController()
        {
            switch (_currentMode)
            {
                case ETeamMode.Merged:
                    return _mergedBody != null ? _mergedBody.GetComponent<RagdollController>() : null;
                case ETeamMode.Separated:
                    return _avatarA != null ? _avatarA.GetComponent<RagdollController>() : null;
                default:
                    return null;
            }
        }

        #endregion

        #region 양방향 입력 전송

        /// <summary>
        /// 양쪽 모두 로컬 입력을 상대방에게 20Hz로 전송한다.
        /// Host → Guest: RpcHostInput
        /// Guest → Host: RpcGuestInput
        /// inputChanged 가드 제거 — 항상 20Hz 전송으로 안정적 동기화.
        /// </summary>
        private void SendLocalInput()
        {
            Vector2 moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            _pendingJump |= Input.GetButtonDown("Jump");

            bool intervalElapsed = Time.time - _lastSendTime >= MinSendInterval;
            if (!intervalElapsed) return;

            Vector3 worldDir = CameraRelativeConverter.Convert(moveInput, _cameraTransformA);

            if (_isHost)
            {
                // Host → Guest(들)에게 입력 전송
                photonView.RPC(nameof(RpcHostInput), RpcTarget.Others, worldDir, _pendingJump);
            }
            else
            {
                // Guest → 팀 Host(PhotonView 소유자)에게 입력 전송
                photonView.RPC(nameof(RpcGuestInput), photonView.Owner, worldDir, _pendingJump);
            }

            _pendingJump = false;
            _lastSendTime = Time.time;
        }

        /// <summary>
        /// Host에서 수신: Guest가 카메라 기준으로 변환한 월드 방향을 RemotePlayerInput에 주입한다.
        /// </summary>
        [PunRPC]
        private void RpcGuestInput(Vector3 worldDir, bool jump)
        {
            _remotePlayerInput.SetWorldDirection(worldDir, jump);
        }

        /// <summary>
        /// Guest에서 수신: Host가 카메라 기준으로 변환한 월드 방향을 RemotePlayerInput에 주입한다.
        /// </summary>
        [PunRPC]
        private void RpcHostInput(Vector3 worldDir, bool jump)
        {
            _remotePlayerInput.SetWorldDirection(worldDir, jump);
        }

        #endregion

        private void RouteInput()
        {
            Vector2 inputA = _playerInputA != null ? _playerInputA.MoveInput : Vector2.zero;
            bool jumpA = _playerInputA != null && _playerInputA.JumpPressed;

            Vector2 inputB = _playerInputB != null ? _playerInputB.MoveInput : Vector2.zero;
            bool jumpB = _playerInputB != null && _playerInputB.JumpPressed;

            // Host: A=로컬(카메라 변환 필요), B=원격(이미 월드 방향)
            // Guest: A=원격(이미 월드 방향), B=로컬(카메라 변환 필요)
            Transform cameraA = _isHost ? _cameraTransformA : null;
            Transform cameraB = _isHost ? null : _cameraTransformA;

            Vector3 worldDirA = CameraRelativeConverter.Convert(inputA, cameraA);
            Vector3 worldDirB = CameraRelativeConverter.Convert(inputB, cameraB);

            _playerFormController.ApplyInput(worldDirA, worldDirB, jumpA, jumpB);
        }
    }
}
