using System;
using UnityEngine;

namespace Player.Domain
{
    public interface IPlayerInput
    {
        bool IsOwner { get; }
        Vector2 MoveInput { get; }
        bool JumpPressed { get; }

        // 래그돌 네트워크 경계. 로컬 = 즉시 invoke, PUN2 = RPC → invoke.
        event Action<Vector3, Vector3> OnImpactReceived;
        event Action OnDeathReceived;
        void SendImpact(Vector3 impulse, Vector3 hitPoint);
        void SendDeath();
    }
}
