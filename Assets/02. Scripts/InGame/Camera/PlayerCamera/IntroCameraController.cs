using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace InGame.Camera.PlayerCamera
{
    /// <summary>
    /// 폴가이즈 스타일 인트로 카메라.
    /// Spline 경로를 따라 맵을 보여준 뒤, Cinemachine 블렌딩으로 Follow 카메라로 전환.
    /// </summary>
    public class IntroCameraController : MonoBehaviour
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera _camera;

        [Header("Dolly Path")]
        [SerializeField] private SplineContainer _dollySpline;

        [Header("Playback")]
        [SerializeField] private float _duration = 2f;
        [SerializeField] private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Look At")]
        [SerializeField] private Transform _lookTarget;

        private CinemachineSplineDolly _splineDolly;
        private float _elapsed;
        private bool _isPlaying;

        private const int ActivePriority = 20;
        private const int StandbyPriority = 0;

        private void Awake()
        {
            SetupSplineDolly();
            InGameCameraManager.SetCameraPriority(_camera, StandbyPriority);
        }

        public void Play()
        {
            if (_camera == null || _dollySpline == null) return;

            _elapsed = 0f;
            _isPlaying = true;

            if (_splineDolly != null)
                _splineDolly.CameraPosition = 0f;

            if (_lookTarget != null)
                _camera.LookAt = _lookTarget;

            InGameCameraManager.SetCameraPriority(_camera, ActivePriority);
        }

        public void Stop()
        {
            _isPlaying = false;
            InGameCameraManager.SetCameraPriority(_camera, StandbyPriority);
        }

        private void Update()
        {
            if (!_isPlaying) return;
            if (_splineDolly == null) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            _splineDolly.CameraPosition = _easeCurve.Evaluate(t);
        }

        private void SetupSplineDolly()
        {
            if (_camera == null) return;

            _splineDolly = _camera.GetComponent<CinemachineSplineDolly>();
            if (_splineDolly != null)
            {
                _splineDolly.Spline = _dollySpline;
                return;
            }

            if (_dollySpline == null) return;

            _splineDolly = _camera.gameObject.AddComponent<CinemachineSplineDolly>();
            _splineDolly.Spline = _dollySpline;
        }
    }
}
