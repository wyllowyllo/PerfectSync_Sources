using Core;
using InGame.Player;
using InGame.Team._02._Domain;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    // TeamCharacter 루트에 부착.
    // 각 Body의 RootBody(Rigidbody) Transform을 추적.
    // RootBody는 Animator 영향 없이 물리로만 이동 → 애니메이션 바운싱 없음.
    //
    // === 씬 설정 가이드 ===
    // 1. Main Camera → CinemachineBrain 컴포넌트 추가.
    // 2. 새 GameObject 생성 후 다음 컴포넌트 추가:
    //    - CinemachineCamera
    //    - CinemachineOrbitalFollow (OrbitStyle: Sphere, Radius: 8)
    //    - CinemachineRotationComposer
    //    - CinemachineInputAxisController
    //    - CinemachineDeoccluder (Strategy: PullCameraForward)
    // 3. TeamCharacter 프리팹에서 각 Body의 RootBody Transform 연결.
    [DefaultExecutionOrder(ExecutionOrderConstants.CinemachineCameraManager)]
    public class CinemachineCameraManager : MonoBehaviourPun
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        [Header("Root Bodies (각 Body의 Rigidbody Transform)")]
        [SerializeField] private Transform _mergedRootBody;
        [SerializeField] private Transform _avatarARootBody;
        [SerializeField] private Transform _avatarBRootBody;

        [Header("Proxy Smoothing")]
        [SerializeField] private float _followSpeed = 12f;
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.7f, 0f);

        private Transform _proxy;
        private PlayerFormController _playerFormController;
        private Transform _activeTarget;
        private bool _isHost;

        public Transform CameraTransform { get; private set; }

        private void Awake()
        {
            var proxyObj = new GameObject("CinemachineCameraProxy");
            proxyObj.transform.SetParent(transform);
            _proxy = proxyObj.transform;
        }

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
            _isHost = photonView.IsMine;

            if (_cinemachineCamera == null)
                _cinemachineCamera = FindAnyObjectByType<CinemachineCamera>();

            if (_cinemachineCamera != null)
            {
                _cinemachineCamera.Follow = _proxy;
                _cinemachineCamera.LookAt = _proxy;
                CameraTransform = _cinemachineCamera.transform;
            }
            else
            {
                var mainCam = UnityEngine.Camera.main;
                CameraTransform = mainCam != null ? mainCam.transform : transform;
            }

            _playerFormController.OnModeChanged += HandleModeChanged;
            UpdateActiveTarget(_playerFormController.CurrentMode);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_activeTarget != null)
                _proxy.position = _activeTarget.position + _targetOffset;
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_proxy != null)
                Destroy(_proxy.gameObject);
        }

        private void LateUpdate()
        {
            if (_activeTarget == null) return;

            Vector3 targetPos = _activeTarget.position + _targetOffset;
            float t = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
            _proxy.position = Vector3.Lerp(_proxy.position, targetPos, t);
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            UpdateActiveTarget(newMode);
        }

        private void UpdateActiveTarget(ETeamMode mode)
        {
            _activeTarget = mode switch
            {
                ETeamMode.Merged => _mergedRootBody,
                ETeamMode.Separated => _isHost ? _avatarARootBody : _avatarBRootBody,
                _ => _mergedRootBody
            };
        }
    }
}
