using System.Collections;
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
        [SerializeField] private IntroCameraController _introCamera;
        [SerializeField] private CinemachineBrain _brain;

        protected override bool PersistAcrossScenes => false;

        [Header("Blend")]
        [Tooltip("카메라 블렌딩 완료 후 카운트다운 시작까지 대기 시간(초).")]
        [SerializeField] private float _postBlendDelay;

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
                HandleGameStateChanged(InGameManager.Instance.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (_introCamera != null)
                _introCamera.OnIntroComplete -= HandleIntroComplete;

            if (InGameManager.Instance != null)
                InGameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
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
            }
        }

        private void ActivateIntro()
        {
            SetCameraPriority(_followCamera, StandbyPriority);

            if (_introCamera != null)
                _introCamera.Play();
        }

        private void HandleIntroComplete()
        {
            ActivateFollow();
            StartCoroutine(WaitForBlendThenNotify());
        }

        private IEnumerator WaitForBlendThenNotify()
        {
            if (_brain != null)
            {
                // 블렌딩이 시작될 때까지 한 프레임 대기.
                yield return null;
                yield return new WaitWhile(() => _brain.IsBlending);
            }

            if (_postBlendDelay > 0f)
                yield return new WaitForSeconds(_postBlendDelay);

            if (InGameManager.Instance != null)
                InGameManager.Instance.NotifyIntroComplete();
        }

        private void ActivateFollow()
        {
            if (_introCamera != null)
                _introCamera.Stop();

            SetCameraPriority(_followCamera, ActivePriority);
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
