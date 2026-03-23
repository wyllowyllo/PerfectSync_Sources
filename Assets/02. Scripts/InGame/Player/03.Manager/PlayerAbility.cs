using UnityEngine;

public abstract class PlayerAbility : MonoBehaviour
{
    protected PlayerController Owner { get; private set; }

    protected virtual void Awake()
    {
        Owner = GetComponentInParent<PlayerController>();
    }
}
