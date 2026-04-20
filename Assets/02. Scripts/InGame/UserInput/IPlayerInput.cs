using System;
using InGame.Player;
using UnityEngine;

namespace InGame.UserInput
{
    public interface IPlayerInput
    {
        bool IsOwner { get; }
        Vector2 MoveInput { get; }
        bool JumpPressed { get; }

        // 래그돌 네트워크 경계. 로컬 = 즉시 invoke, PUN2 = RPC → invoke.
        event Action<HitData, int> OnHitReceived;
        event Action OnDeathReceived;
        event Action OnRespawnReceived;
        event Action OnRecoveryReceived;
        void SendHit(HitData hit, int hitViewID);
        void SendDeath();
        void SendRespawn();
        void SendRecovery();
    }
}
