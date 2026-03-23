using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerCursorLockController))]
public class PlayerController : MonoBehaviour
{
    public PhotonView PhotonView { get; private set; }

    private Dictionary<Type, PlayerAbility> _abilitiesCache = new();

    private void Awake()
    {
        PhotonView = GetComponent<PhotonView>();
    }

    public T GetAbility<T>() where T : PlayerAbility
    {
        var type = typeof(T);
        if (_abilitiesCache.TryGetValue(type, out PlayerAbility ability))
            return ability as T;

        ability = GetComponent<T>();
        if (ability != null)
        {
            _abilitiesCache[type] = ability;
            return ability as T;
        }

        Debug.LogError($"[PlayerController] 어빌리티 {type.Name}을 {gameObject.name}에서 찾을 수 없습니다.");
        return null;
    }
}
