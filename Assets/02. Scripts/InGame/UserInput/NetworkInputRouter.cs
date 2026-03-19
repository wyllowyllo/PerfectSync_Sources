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

        // 애니메이션 트리거 감지 (분리 모드 전용)
        private PlayerJump _activeJumpA;
        private PlayerJump _activeJumpB;
        private BodySyncBridge _activeSyncA;
        private BodySyncBridge _activeSyncB;
        private bool _prevDivingA;
        private bool _prevDivingB;
        private bool _prevGroundedA;
        private bool _prevGroundedB;

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
            CacheActiveBodyComponents();

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

        /// <summary>
        /// 공유 시뮬레이션: Host/Guest 모두 물리 시뮬레이션 실행.
        /// 양쪽 모두 Tick → RouteInput → SendLocalInput 순서로 실행한다.
        /// </summary>
        private void Update()
        {
            _playerFormController.Tick();

            // 트리거 상태 캡처 (RouteInput 전)
            bool wasDivingA = _activeJumpA != null && _activeJumpA.IsDiving;
            bool wasDivingB = _activeJumpB != null && _activeJumpB.IsDiving;

            RouteInput();
            SendLocalInput();

            // 분리 모드에서만 비소유 아바타의 트리거 감지/전송
            if (_currentMode == ETeamMode.Separated)
                DetectAndSendTriggers(wasDivingA, wasDivingB);
        }

        /// <summary>
        /// 바디별 물리 시뮬레이션 활성화/비활성화를 모드에 따라 설정한다.
        /// 합체 모드: 양쪽 모두 MergedBody 시뮬레이션 실행
        /// 분리 모드: 각자 소유 아바타만 시뮬레이션
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
                    if (_isHost)
                    {
                        // Host: AvatarA=로컬 시뮬레이션, AvatarB=원격
                        SetRemoteOnBody(_avatarA, false);
                        SetRemoteOnBody(_avatarB, true);
                    }
                    else
                    {
                        // Guest: AvatarA=원격, AvatarB=로컬 시뮬레이션
                        SetRemoteOnBody(_avatarA, true);
                        SetRemoteOnBody(_avatarB, false);
                    }
                    break;
            }
        }

        /// <summary>
        /// 합체 모드에서 BodySyncBridge의 IPunObservable 위치 보정을 활성화/비활성화한다.
        /// </summary>
        private void RefreshSyncBridgeMode(ETeamMode mode)
        {
            bool isMerged = mode == ETeamMode.Merged;

            var mergedBridge = _mergedBody != null ? _mergedBody.GetComponent<BodySyncBridge>() : null;
            if (mergedBridge != null)
                mergedBridge.SetMergedMode(isMerged);

            var bridgeA = _avatarA != null ? _avatarA.GetComponent<BodySyncBridge>() : null;
            if (bridgeA != null)
                bridgeA.SetMergedMode(false);

            var bridgeB = _avatarB != null ? _avatarB.GetComponent<BodySyncBridge>() : null;
            if (bridgeB != null)
                bridgeB.SetMergedMode(false);
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
                _activeSyncA = _mergedBody.GetComponent<BodySyncBridge>();
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
        }

        private void HandleSwitchRequested()
        {
            _playerFormController.ToggleMode();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            _currentMode = newMode;
            SetCameraTargetByRole();
            UpdateActiveBodyReferences(newMode);
            RefreshBodySimulation(newMode);
            RefreshSyncBridgeMode(newMode);
        }

        private void UpdateActiveBodyReferences(ETeamMode mode)
        {
            switch (mode)
            {
                case ETeamMode.Merged:
                    _activeJumpA = _mergedBody != null ? _mergedBody.GetComponent<PlayerJump>() : null;
                    _activeSyncA = _mergedBody != null ? _mergedBody.GetComponent<BodySyncBridge>() : null;
                    _activeJumpB = null;
                    _activeSyncB = null;
                    break;

                case ETeamMode.Separated:
                    _activeJumpA = _avatarA != null ? _avatarA.GetComponent<PlayerJump>() : null;
                    _activeSyncA = _avatarA != null ? _avatarA.GetComponent<BodySyncBridge>() : null;
                    _activeJumpB = _avatarB != null ? _avatarB.GetComponent<PlayerJump>() : null;
                    _activeSyncB = _avatarB != null ? _avatarB.GetComponent<BodySyncBridge>() : null;
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

        private void DetectAndSendTriggers(bool wasDivingA, bool wasDivingB)
        {
            // Body A 트리거 감지
            if (_activeJumpA != null && _activeSyncA != null)
            {
                bool isDivingA = _activeJumpA.IsDiving;

                if (!wasDivingA && isDivingA)
                    _activeSyncA.SendAnimTrigger(1); // Dive

                if (wasDivingA && !isDivingA)
                    _activeSyncA.SendAnimTrigger(2); // DiveLand

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
