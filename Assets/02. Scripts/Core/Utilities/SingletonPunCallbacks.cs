using UnityEngine;
using Photon.Pun;

public abstract class SingletonPunCallbacks<T> : MonoBehaviourPunCallbacks where T : SingletonPunCallbacks<T>
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 중복 인스턴스 감지. 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
