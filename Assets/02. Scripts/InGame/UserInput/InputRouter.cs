using Core.Utilities;
using InGame.Player;
using InGame.Player.Network;
using InGame.Team;
using Photon.Pun;
using UnityEngine;

namespace InGame.UserInput
{
    [RequireComponent(typeof(LocalPlayerInput), typeof(RemotePlayerInput), typeof(PlayerFormController))]
    public class InputRouter : MonoBehaviourPun
    {
        // ── Fields / SerializeField ──────────────────────────────────

        [Header("Settings")]
        [SerializeField] private ETeamMode _startMode = ETeamMode.Merged;

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;

        private LocalPlayerInput _localPlayerInput;
        private PlayerFormController _playerFormController;
        private RemotePlayerInput _remotePlayerInput;
        private TeamModeSynchronizer _teamModeSynchronizer;
        private Transform _cameraTransformA;
        private UnityEngine.Camera _mainCamera;
        private bool _isHost;
        private ETeamMode _currentMode;

        // 입력 RPC 쓰로틀링
        private bool _pendingJump;
        private float _lastSendTime;
        private const float MinSendInterval = 0.05f; // 최대 20Hz

        // 프레임당 1회 읽기 캐시
        private Vector3 _cachedLocalWorldDir;
        private bool _cachedLocalJump;
        private Vector3 _cachedLocalCameraForwardXZ;

        // 라우팅된 입력 (외부 관찰용)
        public Vector3 RoutedDirA { get; private set; }
        public Vector3 RoutedDirB { get; private set; }
        public bool RoutedJumpA { get; private set; }
        public bool RoutedJumpB { get; private set; }

        public bool IsHost => _isHost;
        public ETeamMode CurrentMode => _currentMode;
        public GameObject MergedBody => _mergedBody;
        public Vector3 LocalCameraForwardXZ => _cachedLocalCameraForwardXZ;

        // ── Lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _localPlayerInput = GetComponent<LocalPlayerInput>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();
            _teamModeSynchronizer = GetComponent<TeamModeSynchronizer>();
        }

        private void Start()
        {
            _isHost = photonView.IsMine;
            _currentMode = _startMode;

            _mainCamera = UnityEngine.Camera.main;
            _cameraTransformA = _mainCamera != null ? _mainCamera.transform : null;

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            if (_teamModeSynchronizer != null)
                _teamModeSynchronizer.OnSwitchRequested += HandleSwitchRequested;
        }

        // ── Update Loop ─────────────────────────────────────────────

        private void Update()
        {
            ReadLocalInput();
            RouteInput();
            SendLocalInput();
        }

        private void ReadLocalInput()
        {
            Vector2 rawInput = _localPlayerInput.MoveInput;
            bool jump = _localPlayerInput.JumpPressed;

            _cachedLocalWorldDir = CameraRelativeConverter.Convert(rawInput, _cameraTransformA);
            _cachedLocalJump = jump;

            Vector3 camFwd = _cameraTransformA != null ? _cameraTransformA.forward : Vector3.forward;
            camFwd.y = 0f;
            _cachedLocalCameraForwardXZ = camFwd.sqrMagnitude > 0.001f ? camFwd.normalized : Vector3.forward;
        }

        private void RouteInput()
        {
            Vector3 worldDirA, worldDirB;
            bool jumpA, jumpB;

            if (_isHost)
            {
                worldDirA = _cachedLocalWorldDir;
                jumpA = _cachedLocalJump;
                worldDirB = CameraRelativeConverter.Convert(_remotePlayerInput.MoveInput, null);
                jumpB = _remotePlayerInput.JumpPressed;
            }
            else
            {
                worldDirA = CameraRelativeConverter.Convert(_remotePlayerInput.MoveInput, null);
                jumpA = _remotePlayerInput.JumpPressed;
                worldDirB = _cachedLocalWorldDir;
                jumpB = _cachedLocalJump;
            }

            RoutedDirA = worldDirA;
            RoutedDirB = worldDirB;
            RoutedJumpA = jumpA;
            RoutedJumpB = jumpB;

            // Host-authoritative: Guest는 로컬 물리 입력 적용 안 함.
            if (!_isHost)
                return;

            _playerFormController.ApplyInput(worldDirA, worldDirB, jumpA, jumpB);
        }

        // ── Input Send ──────────────────────────────────────────────

        private void SendLocalInput()
        {
            _pendingJump |= _cachedLocalJump;

            if (Time.time - _lastSendTime < MinSendInterval) return;

            if (_isHost)
                photonView.RPC(nameof(RpcRemoteInput), RpcTarget.Others, _cachedLocalWorldDir, _pendingJump, _cachedLocalCameraForwardXZ);
            else
                photonView.RPC(nameof(RpcRemoteInput), photonView.Owner, _cachedLocalWorldDir, _pendingJump, _cachedLocalCameraForwardXZ);

            _pendingJump = false;
            _lastSendTime = Time.time;
        }

        [PunRPC]
        private void RpcRemoteInput(Vector3 worldDir, bool jump, Vector3 cameraForwardXZ)
        {
            _remotePlayerInput.SetWorldDirection(worldDir, jump, cameraForwardXZ);
        }

        // ── Mode ─────────────────────────────────────────────────────

        private void HandleSwitchRequested()
        {
            _playerFormController.ExecuteFormToggle();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            _currentMode = newMode;
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_teamModeSynchronizer != null)
                _teamModeSynchronizer.OnSwitchRequested -= HandleSwitchRequested;
        }
    }
}
