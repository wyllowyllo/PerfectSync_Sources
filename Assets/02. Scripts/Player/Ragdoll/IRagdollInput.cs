using System;
using UnityEngine;

public enum ERagdollState { Animated, Ragdoll, BlendToAnim, Dead }

public interface IRagdollInput
{
    public ERagdollState CurrentState { get; }
    public bool IsRagdollActive => ((CurrentState == ERagdollState.Ragdoll) || (CurrentState == ERagdollState.BlendToAnim));

    public void OnHitImpact(Vector3 impulse, Vector3 hitPoint);

    public void OnDeath();

    public event Action<Vector3, Vector3> OnImpactTriggered;
    public event Action OnDeathTriggered;
}
