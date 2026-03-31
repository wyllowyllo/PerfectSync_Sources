using Core;
using System;
using Unity.Cinemachine;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    [DefaultExecutionOrder(ExecutionOrderConstants.CinemachineCameraManager)]
    public class InGameCameraManager : SingletonMonoBehaviour<InGameCameraManager>
    {
        [Header("Scene Cameras")]
        [SerializeField] private CinemachineCamera _followCamera;

        protected override bool PersistAcrossScenes => false;

        private const int ActivePriority = 10;
        private const int StandbyPriority = 0;

        private GameState _currentState;

        public CinemachineCamera FollowCamera => _followCamera;

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnEnable()
        {
            if (InGameManager.Instance != null)
            {
                InGameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
                HandleGameStateChanged(InGameManager.Instance.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (InGameManager.Instance != null)
                InGameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void HandleGameStateChanged(GameState newState)
        {
            _currentState = newState;

            switch (newState)
            {
                case GameState.Loading:
                    DeactivateAll();
                    break;

                case GameState.Intro:
                    // 향후 인트로 카메라 활성화 지점.
                    SetCameraPriority(_followCamera, StandbyPriority);
                    break;

                case GameState.Countdown:
                case GameState.Playing:
                case GameState.RaceComplete:
                case GameState.GameOver:
                    SetCameraPriority(_followCamera, ActivePriority);
                    break;
            }
        }

        /// <summary>
        /// 특정 CinemachineCamera의 Priority를 직접 제어해야 하는 경우 사용.
        /// FollowCameraController의 래그돌 카메라 전환 등에서 호출.
        /// </summary>
        public static void SetCameraPriority(CinemachineCamera camera, int priority)
        {
            if (camera == null) return;
            camera.Priority.Enabled = true;
            camera.Priority.Value = priority;
        }

        private void DeactivateAll()
        {
            SetCameraPriority(_followCamera, StandbyPriority);
        }
    }
}
