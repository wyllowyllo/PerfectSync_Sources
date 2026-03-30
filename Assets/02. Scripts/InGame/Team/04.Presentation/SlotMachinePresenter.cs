using System.Collections;
using DG.Tweening;
using InGame.Player;
using InGame.Player.Movement;
using InGame.Player.Network;
using Photon.Pun;
using UnityEngine;

namespace InGame.Team
{
    public class SlotMachinePresenter : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private SlotMachine _slotMachinePrefab;

        [Header("Display")]
        [SerializeField] private Vector3 _displayOffset = new(0f, 2.0f, 0f);

        [Header("Animation")]
        [SerializeField] private float _slideDistance = 3f;
        [SerializeField] private float _appearDuration = 0.4f;
        [SerializeField] private float _disappearDuration = 0.35f;

        [Header("Timing")]
        [SerializeField] private float _postLandingDelay = 1.5f;

        [Header("Hovering")]
        [SerializeField] private float _bobAmplitude = 0.15f;
        [SerializeField] private float _bobFrequency = 1.5f;
        [SerializeField] private float _followSmoothTime = 0.18f;
        [SerializeField] private float _verticalSmoothTime = 0.04f;
        [SerializeField] private float _tiltAmplitude = 3f;
        [SerializeField] private float _tiltFrequency = 0.8f;

        private TeamModeSynchronizer _synchronizer;
        private PlayerFormController _formController;
        private PhotonView _photonView;

        private SlotMachine _activeSlotMachine;
        private Tween _activeTween;
        private Quaternion _prefabBaseRotation;
        private float _slideOffset;
        private bool _isMatch;

        private float _smoothVelX;
        private float _smoothVelY;
        private float _smoothVelZ;
        private Vector3 _currentPos;
        private bool _posInitialized;

        private void Start()
        {
            _synchronizer = GetComponent<TeamModeSynchronizer>();
            _formController = GetComponent<PlayerFormController>();
            _photonView = GetComponent<PhotonView>();

            _synchronizer.OnSlotSpinReceived += HandleSlotSpin;
        }

        private void OnDestroy()
        {
            _activeTween?.Kill();

            if (_synchronizer != null)
                _synchronizer.OnSlotSpinReceived -= HandleSlotSpin;
        }

        private void LateUpdate()
        {
            if (_activeSlotMachine == null) return;

            Transform body = _formController.PrimaryBodyTransform;
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            // 앵커 위치 (슬라이드 오프셋 포함).
            Vector3 camRight = cam.transform.right;
            Vector3 anchorPos = body.position + _displayOffset + camRight * _slideOffset;

            // 상하 부유 (bobbing).
            float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f) * _bobAmplitude;
            anchorPos.y += bob;

            // 축별 SmoothDamp — 수평은 느긋하게, 수직은 빠르게 추적.
            if (!_posInitialized)
            {
                _currentPos = anchorPos;
                _smoothVelX = _smoothVelY = _smoothVelZ = 0f;
                _posInitialized = true;
            }

            _currentPos.x = Mathf.SmoothDamp(_currentPos.x, anchorPos.x, ref _smoothVelX, _followSmoothTime);
            _currentPos.y = Mathf.SmoothDamp(_currentPos.y, anchorPos.y, ref _smoothVelY, _verticalSmoothTime);
            _currentPos.z = Mathf.SmoothDamp(_currentPos.z, anchorPos.z, ref _smoothVelZ, _followSmoothTime);
            _activeSlotMachine.transform.position = _currentPos;

            // 카메라를 향한 Yaw + 미세 틸트 흔들림.
            Vector3 lookDir = cam.transform.position - _currentPos;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion yaw = Quaternion.LookRotation(lookDir);
                float tiltZ = Mathf.Sin(Time.time * _tiltFrequency * Mathf.PI * 2f) * _tiltAmplitude;
                float tiltX = Mathf.Cos(Time.time * _tiltFrequency * 0.7f * Mathf.PI * 2f) * _tiltAmplitude * 0.5f;
                Quaternion wobble = Quaternion.Euler(tiltX, 0f, tiltZ);
                _activeSlotMachine.transform.rotation = yaw * wobble * _prefabBaseRotation;
            }
        }

        private void HandleSlotSpin(int[] symbols, bool isMatch)
        {
            _activeTween?.Kill();
            if (_activeSlotMachine != null)
            {
                Destroy(_activeSlotMachine.gameObject);
                StopAllCoroutines();
            }

            _isMatch = isMatch;
            _slideOffset = _slideDistance;
            _posInitialized = false;

            Transform body = _formController.PrimaryBodyTransform;
            var cam = UnityEngine.Camera.main;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            Vector3 spawnPos = body.position + _displayOffset + camRight * _slideDistance;

            _activeSlotMachine = Instantiate(_slotMachinePrefab, spawnPos, _slotMachinePrefab.transform.rotation);
            _prefabBaseRotation = _slotMachinePrefab.transform.rotation;

            // 오른쪽에서 슬라이드인.
            _activeTween = DOTween.To(() => _slideOffset, v => _slideOffset = v, 0f, _appearDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    _activeSlotMachine.Spin(symbols);
                    _activeSlotMachine.OnSpinComplete += OnSpinComplete;
                });
        }

        private void OnSpinComplete(int[] results)
        {
            if (_activeSlotMachine != null)
                _activeSlotMachine.OnSpinComplete -= OnSpinComplete;

            StartCoroutine(WaitForLandingAndFinish());
        }

        private IEnumerator WaitForLandingAndFinish()
        {
            yield return new WaitUntil(IsAnyActiveBodyGrounded);
            yield return new WaitForSeconds(_postLandingDelay);

            // 오른쪽으로 슬라이드아웃.
            if (_activeSlotMachine != null)
            {
                _activeTween = DOTween.To(() => _slideOffset, v => _slideOffset = v, _slideDistance, _disappearDuration)
                    .SetEase(Ease.InCubic);

                yield return _activeTween.WaitForCompletion();

                Destroy(_activeSlotMachine.gameObject);
                _activeSlotMachine = null;
            }

            if (_photonView.IsMine)
            {
                var targetMode = _isMatch ? ETeamMode.Merged : ETeamMode.Separated;
                _synchronizer.RequestModeChange(targetMode);
            }
        }

        private bool IsAnyActiveBodyGrounded()
        {
            var movements = GetComponentsInChildren<PlayerMovement>();
            foreach (var movement in movements)
            {
                if (movement.gameObject.activeInHierarchy && movement.Grounded)
                    return true;
            }

            return false;
        }
    }
}
