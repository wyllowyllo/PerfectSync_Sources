using Core.Utilities;
using InGame.Player;
using InGame.Player.Network;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace InGame.UserInput
{
    [RequireComponent(typeof(LocalPlayerInput), typeof(RemotePlayerInput), typeof(MergedBodyController))]
    public class InputRouter : MonoBehaviourPun
    {
        // ── Fields / SerializeField ──────────────────────────────────

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;

        private LocalPlayerInput _localPlayerInput;
        private MergedBodyController mergedBodyController;
        private RemotePlayerInput _remotePlayerInput;
        private Transform _cameraTransformA;
        private UnityEngine.Camera _mainCamera;
        private bool _isHost;
        private bool? _isMyTeam;
        private bool _initialized;

        // Host → Guest 1:1 RPC 전송용 캐시
        private Photon.Realtime.Player _guestPlayer;
        private bool _guestPlayerResolved;

        // 입력 RPC 쓰로틀링
        private bool _pendingJump;
        private float _lastSendTime;
        private const float MinSendInterval = 0.033f; // 최대 30Hz

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
        public GameObject MergedBody => _mergedBody;
        public Vector3 LocalCameraForwardXZ => _cachedLocalCameraForwardXZ;

        // ── Lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            _localPlayerInput = GetComponent<LocalPlayerInput>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            mergedBodyController = GetComponent<MergedBodyController>();
        }

        private void Start()
        {
            _isHost = photonView.IsMine;
            mergedBodyController.Initialize();

            TryInitialize();
        }

        private void TryInitialize()
        {
            if (_initialized) return;
            if (!CheckIsMyTeam()) return;

            _initialized = true;

            _mainCamera = UnityEngine.Camera.main;
            _cameraTransformA = _mainCamera != null ? _mainCamera.transform : null;
        }

        // ── Update Loop ─────────────────────────────────────────────

        private void Update()
        {
            if (!_initialized)
            {
                TryInitialize();
                if (!_initialized) return;
            }

            ReadLocalInput();

            if (!InGameManager.IsLocalPlayerControllable)
                _cachedLocalWorldDir = Vector3.zero;

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

            mergedBodyController.ApplyInput(worldDirA, worldDirB, jumpA, jumpB);
        }

        // ── Input Send ──────────────────────────────────────────────

        private void SendLocalInput()
        {
            _pendingJump |= _cachedLocalJump;

            if (Time.time - _lastSendTime < MinSendInterval) return;

            if (_isHost)
            {
                var guest = ResolveGuestPlayer();
                if (guest != null)
                    photonView.RPC(nameof(RpcRemoteInput), guest, _cachedLocalWorldDir, _pendingJump, _cachedLocalCameraForwardXZ);
            }
            else
            {
                photonView.RPC(nameof(RpcRemoteInput), photonView.Owner, _cachedLocalWorldDir, _pendingJump, _cachedLocalCameraForwardXZ);
            }

            _pendingJump = false;
            _lastSendTime = Time.time;
        }

        private Photon.Realtime.Player ResolveGuestPlayer()
        {
            if (_guestPlayerResolved) return _guestPlayer;

            if (photonView.Owner == null) return null;

            int ownerTeam = PhotonTeamManager.GetTeamRaw(photonView.Owner);
            if (ownerTeam == PhotonTeamManager.TeamNone) return null;

            var members = PhotonTeamManager.Instance?.GetTeamMembers(ownerTeam);
            if (members == null) return null;

            foreach (var member in members)
            {
                if (member.ActorNumber != photonView.Owner.ActorNumber)
                {
                    _guestPlayer = member;
                    break;
                }
            }

            // Guest가 있을 때만 캐시 확정 (없으면 다음 프레임에 재탐색)
            if (_guestPlayer != null)
                _guestPlayerResolved = true;

            return _guestPlayer;
        }

        [PunRPC]
        private void RpcRemoteInput(Vector3 worldDir, bool jump, Vector3 cameraForwardXZ)
        {
            _remotePlayerInput.SetWorldDirection(worldDir, jump, cameraForwardXZ);
        }

        private bool CheckIsMyTeam()
        {
            if (_isMyTeam.HasValue) return _isMyTeam.Value;

            var owner = photonView.Owner;
            if (owner == null) return false;

            int ownerTeam = PhotonTeamManager.GetTeamRaw(owner);
            int myTeam = PhotonTeamManager.GetLocalTeamRaw();

            if (ownerTeam == PhotonTeamManager.TeamNone || myTeam == PhotonTeamManager.TeamNone)
                return false;

            _isMyTeam = (ownerTeam == myTeam);
            return _isMyTeam.Value;
        }
    }
}
