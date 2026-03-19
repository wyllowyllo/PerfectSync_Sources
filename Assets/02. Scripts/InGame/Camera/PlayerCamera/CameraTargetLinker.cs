using InGame.Player;
using InGame.Team._02._Domain;
using Photon.Pun;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    [DefaultExecutionOrder(-10)]
    public class CameraTargetLinker : MonoBehaviourPun
    {
        [SerializeField] private TpsCameraController _cameraControllerA;

        private PlayerFormController _playerFormController;
        private bool _isHost;

        public Transform CameraTransform { get; private set; }

        private void Start()
        {
            _playerFormController = GetComponent<PlayerFormController>();
            _isHost = photonView.IsMine;

            if (_cameraControllerA == null)
                _cameraControllerA = FindAnyObjectByType<TpsCameraController>();

            CameraTransform = _cameraControllerA != null
                ? _cameraControllerA.transform
                : UnityEngine.Camera.main.transform;

            _playerFormController.OnModeChanged += HandleModeChanged;
            SetCameraTargetByRole();
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            SetCameraTargetByRole();
        }

        private void SetCameraTargetByRole()
        {
            if (_cameraControllerA == null) return;

            var target = _isHost
                ? _playerFormController.PrimaryCameraFollowPoint
                : _playerFormController.SecondaryCameraFollowPoint;

            _cameraControllerA.SetTarget(target);
        }
    }
}
