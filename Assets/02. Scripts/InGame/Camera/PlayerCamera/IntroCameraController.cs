using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace InGame.Camera.PlayerCamera
{
    /// <summary>
    /// 폴가이즈 스타일 인트로 카메라.
    /// Spline 경로를 따라 맵을 보여준 뒤, Cinemachine 블렌딩으로 Follow 카메라로 전환.
    /// LookAt Spline이 설정되면 카메라 시선도 별도 경로를 따라 이동.
    /// </summary>
    public class IntroCameraController : MonoBehaviour
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera _camera;

        [Header("Dolly Path")]
        [SerializeField] private SplineContainer _dollySpline;

        [Header("Playback")]
        [SerializeField] private float _duration = 6f;
        [SerializeField] private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Look At")]
        [Tooltip("LookAt Spline이 설정되면 무시됩니다.")]
        [SerializeField] private Transform _lookTarget;

        [Tooltip("카메라 시선이 따라가는 별도 Spline 경로. 설정 시 _lookTarget보다 우선합니다.")]
        [SerializeField] private SplineContainer _lookAtSpline;

        public float Duration => _duration;
        public bool IsPlaying => _isPlaying;

        public event Action OnIntroComplete;

        private CinemachineSplineDolly _splineDolly;
        private float _elapsed;
        private bool _isPlaying;
        private bool _completed;
        private Transform _lookAtProxy;

        private const int ActivePriority = 20;
        private const int StandbyPriority = 0;

        private void Awake()
        {
            SetupSplineDolly();
            SetupHardLookAt();
            CreateLookAtProxy();
            InGameCameraManager.SetCameraPriority(_camera, StandbyPriority);
        }

        private void OnDestroy()
        {
            if (_lookAtProxy != null)
                Destroy(_lookAtProxy.gameObject);
        }

        public void Play()
        {
            if (_camera == null || _dollySpline == null) return;

            _elapsed = 0f;
            _isPlaying = true;
            _completed = false;

            if (_splineDolly != null)
                _splineDolly.CameraPosition = 0f;

            bool hasLookAtSpline = _lookAtSpline != null && _lookAtSpline.Spline != null;

            if (hasLookAtSpline)
            {
                _camera.LookAt = _lookAtProxy;
                UpdateLookAtProxy(0f);
            }
            else if (_lookTarget != null)
            {
                _camera.LookAt = _lookTarget;
            }

            InGameCameraManager.SetCameraPriority(_camera, ActivePriority);
        }

        public void Stop()
        {
            _isPlaying = false;
            _camera.LookAt = null;
            InGameCameraManager.SetCameraPriority(_camera, StandbyPriority);
        }

        private void Update()
        {
            if (!_isPlaying) return;
            if (_splineDolly == null) return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float evaluated = _easeCurve.Evaluate(t);

            _splineDolly.CameraPosition = evaluated;
            UpdateLookAtProxy(evaluated);

            if (t >= 1f && !_completed)
            {
                _completed = true;
                OnIntroComplete?.Invoke();
            }
        }

        private void CreateLookAtProxy()
        {
            var obj = new GameObject("IntroCamera_LookAtProxy");
            obj.hideFlags = HideFlags.HideAndDontSave;
            _lookAtProxy = obj.transform;
        }

        private void UpdateLookAtProxy(float t)
        {
            if (_lookAtSpline == null || _lookAtSpline.Spline == null) return;
            if (_lookAtProxy == null) return;

            _lookAtProxy.position = _lookAtSpline.EvaluatePosition(t);
        }

        private void SetupSplineDolly()
        {
            if (_camera == null) return;

            _splineDolly = _camera.GetComponent<CinemachineSplineDolly>();
            if (_splineDolly != null)
            {
                _splineDolly.Spline = _dollySpline;
            }
            else if (_dollySpline != null)
            {
                _splineDolly = _camera.gameObject.AddComponent<CinemachineSplineDolly>();
                _splineDolly.Spline = _dollySpline;
            }

            // SplineDolly가 회전을 제어하지 않도록 설정. Aim(HardLookAt)이 담당.
            if (_splineDolly != null)
                _splineDolly.CameraRotation = CinemachineSplineDolly.RotationMode.Default;
        }

        private void SetupHardLookAt()
        {
            if (_camera == null) return;
            if (_camera.GetComponent<CinemachineHardLookAt>() != null) return;

            _camera.gameObject.AddComponent<CinemachineHardLookAt>();
        }
    }
}
