using System;
using InGame.Player;
using Photon.Pun;
using UnityEngine;

namespace InGame.UserInput
{
    public class RemotePlayerInput : MonoBehaviourPun, IPlayerInput
    {
        public bool IsOwner => false;

        public Vector2 MoveInput => _moveInput;
        public bool JumpPressed
        {
            get
            {
                bool val = _jumpPressed;
                _jumpPressed = false;
                return val;
            }
        }

        public event Action<HitData, int> OnHitReceived;
        public event Action OnDeathReceived;
        public event Action OnRespawnReceived;
        public event Action OnRecoveryReceived;

        private Vector2 _moveInput;
        private bool _jumpPressed;
        private Vector3 _cameraForwardXZ;

        public Vector3 CameraForwardXZ => _cameraForwardXZ;

        /// <summary>
        /// Guest가 카메라 기준으로 변환한 월드 방향을 수신하여 저장한다.
        /// CameraRelativeConverter.Convert(_, null) 경로에서 (x, 0, z)로 복원된다.
        /// </summary>
        public void SetWorldDirection(Vector3 worldDir, bool jumpPressed, Vector3 cameraForwardXZ)
        {
            _moveInput = new Vector2(worldDir.x, worldDir.z);
            _jumpPressed |= jumpPressed;
            _cameraForwardXZ = cameraForwardXZ;
        }

        public void SendHit(HitData hit, int hitViewID)
        {
            OnHitReceived?.Invoke(hit, hitViewID);
        }

        public void SendDeath()
        {
            OnDeathReceived?.Invoke();
        }

        public void SendRespawn()
        {
            OnRespawnReceived?.Invoke();
        }

        public void SendRecovery()
        {
            OnRecoveryReceived?.Invoke();
        }
    }
}
