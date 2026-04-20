using System;
using System.Collections;
using DG.Tweening;
using InGame.Player;
using InGame.Race.Platform;
using InGame.UserInput;
using Photon.Pun;
using UnityEngine;

namespace InGame.Gimmick
{
    public class RespawnHandler : MonoBehaviour, IInvincibilitySource
    {
        [Header("Respawn")]
        [SerializeField] private float _respawnDelay = 1.0f;

        [Header("Spawn Punch")]
        [SerializeField] private float _punchOvershoot = 1.15f;
        [SerializeField] private float _punchInDuration = 0.08f;
        [SerializeField] private float _punchOutDuration = 0.1f;

        [Header("Respawn Invincibility")]
        [SerializeField] private float _invincibilityDuration = 2f;

        private LocalPlayerInput _input;
        private MergedBodyController _formController;
        private bool _isRespawning;
        private bool _isRespawnInvincible;
        private Tween _scaleTween;

        public bool IsInvincible => _isRespawnInvincible;

        public event Action<Vector3> OnRespawnInvincibleStart;
        public event Action<Vector3, Quaternion> OnCameraResetRequested;

        private void Start()
        {
            _input = GetComponent<LocalPlayerInput>();
            _formController = GetComponent<MergedBodyController>();
            _input.OnDeathReceived += HandleDeath;
            _input.OnRecoveryReceived += HandleRecovery;
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnDeathReceived -= HandleDeath;
                _input.OnRecoveryReceived -= HandleRecovery;
            }
            _scaleTween?.Kill();
        }

        private void HandleDeath()
        {
            var photonView = GetComponent<PhotonView>();
            if (photonView == null || !photonView.IsMine) return;
            if (_isRespawning) return;

            StartCoroutine(RespawnCoroutine());
        }

        // F1 긴급 복구 키 처리. 사망 리스폰과 동일한 코루틴 재사용.
        // SendRespawn()이 코루틴 내부에서 TriggerRecovery()를 호출하여 현재 상태에 맞게 복구됨.
        private void HandleRecovery()
        {
            var photonView = GetComponent<PhotonView>();
            if (photonView == null || !photonView.IsMine) return;
            if (_isRespawning) return;

            StartCoroutine(RespawnCoroutine());
        }

        private IEnumerator RespawnCoroutine()
        {
            _isRespawning = true;

            yield return new WaitForSeconds(_respawnDelay);

            _isRespawnInvincible = true;

            Vector3 respawnPosition = GetRespawnPosition();
            Quaternion respawnRotation = GetRespawnRotation();
            TeleportBodies(respawnPosition, respawnRotation);

            // 카메라를 리스폰 방향으로 즉시 리셋.
            OnCameraResetRequested?.Invoke(respawnPosition, respawnRotation);

            // 래그돌 복원 (스켈레톤 재연결)을 정상 스케일에서 먼저 수행.
            _input.SendRespawn();

            // 스켈레톤 재연결 완료 후 펀치 스케일로 즉시 등장.
            Transform bodyTransform = _formController.PrimaryBodyTransform;
            _scaleTween?.Kill();
            Vector3 originalScale = bodyTransform.localScale;
            bodyTransform.localScale = Vector3.zero;

            // VFX 버스트 시작. 래그돌 상태에서 rootBody가 펠비스로 역추적될 수 있어
            // Transform 대신 리스폰 좌표를 명시적으로 전달한다.
            OnRespawnInvincibleStart?.Invoke(respawnPosition);

            // 뿅! 펀치 스케일 (0 → 오버슛 → 원래 크기).
            _scaleTween = DOTween.Sequence()
                .Append(bodyTransform.DOScale(originalScale * _punchOvershoot, _punchInDuration)
                    .SetEase(Ease.OutQuad))
                .Append(bodyTransform.DOScale(originalScale, _punchOutDuration)
                    .SetEase(Ease.InOutQuad));

            _isRespawning = false;

            yield return new WaitForSeconds(_invincibilityDuration);

            _isRespawnInvincible = false;
        }

        private void TeleportBodies(Vector3 position, Quaternion rotation)
        {
            TeleportBody(_formController.PrimaryBodyTransform, position, rotation);
        }

        private void TeleportBody(Transform bodyTransform, Vector3 position, Quaternion rotation)
        {
            var rb = bodyTransform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                PlatformCarrier.ForceRemoveRider(rb);
                rb.position = position;
                rb.rotation = rotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                bodyTransform.SetPositionAndRotation(position, rotation);
            }
        }

        private Vector3 GetRespawnPosition()
        {
            int lastCheckpoint = GetLastCheckpointPassed();
            var checkpoint = FindCheckpoint(lastCheckpoint);
            if (checkpoint != null)
                return checkpoint.RespawnPosition;

            return transform.position;
        }

        private Quaternion GetRespawnRotation()
        {
            int lastCheckpoint = GetLastCheckpointPassed();
            var checkpoint = FindCheckpoint(lastCheckpoint);
            if (checkpoint != null)
                return checkpoint.RespawnRotation;

            return Quaternion.identity;
        }

        private int GetLastCheckpointPassed()
        {
            var trackers = GetComponentsInChildren<RaceProgressTracker>(true);
            int max = 0;
            foreach (var tracker in trackers)
            {
                if (tracker.CheckpointsPassed > max)
                    max = tracker.CheckpointsPassed;
            }
            return max;
        }

        private RaceCheckpoint FindCheckpoint(int index)
        {
            // 체크포인트 0은 시작 지점이므로 씬에 없을 수 있음.
            var checkpoints = FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None);
            foreach (var cp in checkpoints)
            {
                if (cp.CheckpointIndex == index)
                    return cp;
            }
            return null;
        }
    }
}
