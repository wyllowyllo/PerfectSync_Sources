using System.Collections;
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
        [SerializeField] private Vector3 _displayOffset = new(1.5f, 1.0f, 0f);

        [Header("Timing")]
        [SerializeField] private float _postLandingDelay = 1.5f;

        private TeamModeSynchronizer _synchronizer;
        private PlayerFormController _formController;
        private PhotonView _photonView;

        private SlotMachine _activeSlotMachine;
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
            if (_synchronizer != null)
                _synchronizer.OnSlotSpinReceived -= HandleSlotSpin;
        }

        private void LateUpdate()
        {
            if (_activeSlotMachine == null) return;

            Transform body = _formController.PrimaryBodyTransform;
            _activeSlotMachine.transform.position = body.position + body.TransformDirection(_displayOffset);
            _activeSlotMachine.transform.rotation = body.rotation;
        }

        private void HandleSlotSpin(int[] symbols, bool isMatch)
        {
            // 이전 슬롯머신이 남아 있으면 정리.
            if (_activeSlotMachine != null)
            {
                Destroy(_activeSlotMachine.gameObject);
                StopAllCoroutines();
            }

            _isMatch = isMatch;

            Transform body = _formController.PrimaryBodyTransform;
            Vector3 spawnPos = body.position + body.TransformDirection(_displayOffset);

            _activeSlotMachine = Instantiate(_slotMachinePrefab, spawnPos, body.rotation);
            _activeSlotMachine.Spin(symbols);
            _activeSlotMachine.OnSpinComplete += OnSpinComplete;
        }

        private void OnSpinComplete(int[] results)
        {
            if (_activeSlotMachine != null)
                _activeSlotMachine.OnSpinComplete -= OnSpinComplete;

            StartCoroutine(WaitForLandingAndFinish());
        }

        private IEnumerator WaitForLandingAndFinish()
        {
            // 활성 바디가 착지할 때까지 대기.
            yield return new WaitUntil(IsAnyActiveBodyGrounded);

            yield return new WaitForSeconds(_postLandingDelay);

            if (_activeSlotMachine != null)
            {
                Destroy(_activeSlotMachine.gameObject);
                _activeSlotMachine = null;
            }

            // 모드 전환은 Host만 실행. 3매치 → 합체, 불일치 → 분리.
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
