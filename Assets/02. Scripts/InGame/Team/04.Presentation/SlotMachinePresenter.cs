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
        [SerializeField] private float _slideHeight = 3f;
        [SerializeField] private float _appearDuration = 0.4f;
        [SerializeField] private float _disappearDuration = 0.35f;

        [Header("Timing")]
        [SerializeField] private float _postLandingDelay = 1.5f;

        private TeamModeSynchronizer _synchronizer;
        private PlayerFormController _formController;
        private PhotonView _photonView;

        private SlotMachine _activeSlotMachine;
        private Tween _activeTween;
        private Quaternion _prefabBaseRotation;
        private float _localOffsetY;
        private bool _isMatch;

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
            Vector3 targetPos = body.position + _displayOffset;
            targetPos.y += _localOffsetY;
            _activeSlotMachine.transform.position = targetPos;

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                Vector3 lookDir = cam.transform.position - targetPos;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion yaw = Quaternion.LookRotation(lookDir);
                    _activeSlotMachine.transform.rotation = yaw * _prefabBaseRotation;
                }
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
            _localOffsetY = _slideHeight;

            Transform body = _formController.PrimaryBodyTransform;
            Vector3 spawnPos = body.position + _displayOffset + Vector3.up * _slideHeight;

            _activeSlotMachine = Instantiate(_slotMachinePrefab, spawnPos, _slotMachinePrefab.transform.rotation);
            _prefabBaseRotation = _slotMachinePrefab.transform.rotation;

            // 위에서 내려오며 등장.
            _activeTween = DOTween.To(() => _localOffsetY, v => _localOffsetY = v, 0f, _appearDuration)
                .SetEase(Ease.OutBack)
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

            // 위로 올라가며 퇴장.
            if (_activeSlotMachine != null)
            {
                _activeTween = DOTween.To(() => _localOffsetY, v => _localOffsetY = v, _slideHeight, _disappearDuration)
                    .SetEase(Ease.InBack);

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
