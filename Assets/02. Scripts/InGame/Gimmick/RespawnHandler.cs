using System;
using System.Collections;
using InGame.Player;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using InGame.UserInput;
using Photon.Pun;
using UnityEngine;

namespace InGame.Gimmick
{
    public class RespawnHandler : MonoBehaviour, IInvincibilitySource
    {
        [Header("Respawn")]
        [SerializeField] private float _respawnDelay = 1.5f;

        [Header("Respawn Invincibility")]
        [SerializeField] private float _invincibilityDuration = 3f;

        private LocalPlayerInput _input;
        private MergedBodyController _formController;
        private bool _isRespawning;
        private bool _isRespawnInvincible;

        public bool IsInvincible => _isRespawnInvincible;

        public event Action OnRespawnInvincibleStart;
        public event Action OnRespawnInvincibleEnd;

        private void Start()
        {
            _input = GetComponent<LocalPlayerInput>();
            _formController = GetComponent<MergedBodyController>();
            _input.OnDeathReceived += HandleDeath;
        }

        private void OnDestroy()
        {
            if (_input != null)
                _input.OnDeathReceived -= HandleDeath;
        }

        private void HandleDeath()
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

            // 무적을 먼저 켜서 리스폰 직후 피격 방지.
            _isRespawnInvincible = true;
            OnRespawnInvincibleStart?.Invoke();

            Vector3 respawnPosition = GetRespawnPosition();
            Quaternion respawnRotation = GetRespawnRotation();
            TeleportBodies(respawnPosition, respawnRotation);

            _input.SendRespawn();
            _isRespawning = false;

            yield return new WaitForSeconds(_invincibilityDuration);

            _isRespawnInvincible = false;
            OnRespawnInvincibleEnd?.Invoke();
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
