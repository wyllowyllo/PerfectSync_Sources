using Core.Utilities;
using InGame.Camera.PlayerCamera;
using InGame.Player;
using InGame.Player.Network;
using InGame.Team._02._Domain;
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

        [Header("Cameras")]
        [SerializeField] private TpsCameraController _cameraControllerA;

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;

        private LocalPlayerInput _localPlayerInput;
        private PlayerFormController _playerFormController;
        private RemotePlayerInput _remotePlayerInput;
        private TeamModeSynchronizer _teamModeSynchronizer;
        private Transform _cameraTransformA;
        private bool _isHost;
        private ETeamMode _currentMode;

        // 입력 RPC 쓰로틀링
        private bool _pendingJump;
        private float _lastSendTime;
        private const float MinSendInterval = 0.05f; // 최대 20Hz

        // 프레임당 1회 읽기 캐시
        private Vector3 _cachedLocalWorldDir;
        private bool _cachedLocalJump;

        // [InputViz]
        private Vector3 _cachedLocalCameraForwardXZ;
        private CoopInputVisualizer _coopInputVisualizer;
        // [/InputViz]

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

            SetupCameras();
            _coopInputVisualizer = GetComponent<CoopInputVisualizer>(); // [InputViz]

            _playerFormController.OnModeChanged += HandleModeChanged;
            _playerFormController.Initialize(_startMode);

            SetCameraTargetByRole();

            if (_teamModeSynchronizer != null)
                _teamModeSynchronizer.OnSwitchRequested += HandleSwitchRequested;
        }



        // ── Update Loop ─────────────────────────────────────────────

        private void Update()
        {
            _playerFormController.Tick();
            ReadLocalInput();
            RouteInput();
            SendLocalInput();
        }

        private void ReadLocalInput()
        {
            Vector2 rawInput;
            bool jump;


            rawInput = _localPlayerInput.MoveInput;
            jump = _localPlayerInput.JumpPressed;
            

            _cachedLocalWorldDir = CameraRelativeConverter.Convert(rawInput, _cameraTransformA);
            _cachedLocalJump = jump;

            // [InputViz] 카메라 전방 XZ 캐싱
            Vector3 camFwd = _cameraTransformA != null ? _cameraTransformA.forward : Vector3.forward;
            camFwd.y = 0f;
            _cachedLocalCameraForwardXZ = camFwd.sqrMagnitude > 0.001f ? camFwd.normalized : Vector3.forward;
            // [/InputViz]
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

            // [InputViz]
            if (_coopInputVisualizer != null)
                _coopInputVisualizer.UpdateInputState(worldDirA, worldDirB, jumpA, jumpB);
            // [/InputViz]

            _playerFormController.ApplyInput(worldDirA, worldDirB, jumpA, jumpB);
        }

        // ── Input Send ──────────────────────────────────────────────

        private void SendLocalInput()
        {
            _pendingJump |= _cachedLocalJump;

            if (Time.time - _lastSendTime < MinSendInterval) return;

            if (_isHost)
                photonView.RPC(nameof(RpcRemoteInput), RpcTarget.Others, _cachedLocalWorldDir, _pendingJump, _cachedLocalCameraForwardXZ); // [InputViz] cameraForwardXZ 추가
            else
                photonView.RPC(nameof(RpcRemoteInput), photonView.Owner, _cachedLocalWorldDir, _pendingJump, _cachedLocalCameraForwardXZ); // [InputViz] cameraForwardXZ 추가

            _pendingJump = false;
            _lastSendTime = Time.time;
        }

        [PunRPC]
        private void RpcRemoteInput(Vector3 worldDir, bool jump, Vector3 cameraForwardXZ) // [InputViz] cameraForwardXZ 추가
        {
            _remotePlayerInput.SetWorldDirection(worldDir, jump, cameraForwardXZ); // [InputViz] cameraForwardXZ 전달
        }

        // ── Mode ─────────────────────────────────────────────────────

        private void HandleSwitchRequested()
        {
            _playerFormController.ExecuteFormToggle();
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            _currentMode = newMode;
            SetCameraTargetByRole();
        }

        // [InputViz]
        public Vector3 LocalCameraForwardXZ => _cachedLocalCameraForwardXZ;
        public bool IsHost => _isHost;
        public ETeamMode CurrentMode => _currentMode;
        public GameObject MergedBody => _mergedBody;
        // [/InputViz]

        // ── Camera ───────────────────────────────────────────────────

        private void SetupCameras()
        {
            if (_cameraControllerA == null)
                _cameraControllerA = FindAnyObjectByType<TpsCameraController>();

            _cameraTransformA = _cameraControllerA != null
                ? _cameraControllerA.transform
                : UnityEngine.Camera.main.transform;
        }

        private void SetCameraTargetByRole()
        {
            if (_cameraControllerA == null) return;

            var target = _isHost
                ? _playerFormController.PrimaryCameraFollowPoint
                : _playerFormController.SecondaryCameraFollowPoint;

            _cameraControllerA.SetTarget(target);
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
