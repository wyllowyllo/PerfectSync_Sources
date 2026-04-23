using Core;
using Unity.Cinemachine;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    [DefaultExecutionOrder(ExecutionOrderConstants.CinemachineCameraManager)]
    public class InGameCameraManager : SingletonMonoBehaviour<InGameCameraManager>
    {
        [Header("Scene Cameras")]
        [SerializeField] private CinemachineCamera _followCamera;
        [SerializeField] private CinemachineInputAxisController _followCameraInput;
        [SerializeField] private IntroCameraController _introCamera;
        [SerializeField] private CinemachineCamera _ceremonyCamera;

        protected override bool PersistAcrossScenes => false;

        private const int ActivePriority = 10;
        private const int StandbyPriority = 0;

        public CinemachineCamera FollowCamera => _followCamera;
        public IntroCameraController IntroCamera => _introCamera;

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            if (_introCamera != null)
                _introCamera.OnIntroComplete += HandleIntroComplete;

            if (InGameManager.Instance != null)
            {
                InGameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                InGameManager.Instance.OnLocalPauseChanged += HandleLocalPauseChanged;
                HandleGameStateChanged(InGameManager.Instance.CurrentState);
                HandleLocalPauseChanged(InGameManager.Instance.IsLocalPaused);
            }
        }

        private void OnDisable()
        {
            if (_introCamera != null)
                _introCamera.OnIntroComplete -= HandleIntroComplete;

            if (InGameManager.Instance != null)
            {
                InGameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                InGameManager.Instance.OnLocalPauseChanged -= HandleLocalPauseChanged;
            }
        }

        private void HandleLocalPauseChanged(bool paused)
        {
            if (_followCameraInput != null)
                _followCameraInput.enabled = !paused;
        }

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Loading:
                    DeactivateAll();
                    break;

                case GameState.Intro:
                    ActivateIntro();
                    break;

                case GameState.Countdown:
                case GameState.Playing:
                case GameState.RaceComplete:
                case GameState.GameOver:
                    ActivateFollow();
                    break;

                case GameState.Ceremony:
                    ActivateCeremony();
                    break;
            }
        }

        private void ActivateIntro()
        {
            SetCameraPriority(_followCamera, StandbyPriority);

            if (_introCamera != null)
            {
                _introCamera.Play();
            }
            else if (InGameManager.Instance != null)
            {
                // Intro 카메라가 없으면 즉시 완료 신호.
                InGameManager.Instance.NotifyLocalIntroDone();
            }
        }

        private void HandleIntroComplete()
        {
            if (InGameManager.Instance != null)
                InGameManager.Instance.NotifyLocalIntroDone();
        }

        private void ActivateFollow()
        {
            if (_introCamera != null)
                _introCamera.Stop();

            SetCameraPriority(_followCamera, ActivePriority);
        }

        private void ActivateCeremony()
        {
            if (_introCamera != null)
                _introCamera.Stop();

            SetCameraPriority(_followCamera, StandbyPriority);
            SetCameraPriority(_ceremonyCamera, ActivePriority);
        }

        private void DeactivateAll()
        {
            SetCameraPriority(_followCamera, StandbyPriority);

            if (_introCamera != null)
                _introCamera.Stop();
        }

        public static void SetCameraPriority(CinemachineCamera camera, int priority)
        {
            if (camera == null) return;
            camera.Priority.Enabled = true;
            camera.Priority.Value = priority;
        }
    }
}
