using UnityEngine;
using Photon.Pun;

public abstract class SingletonPunCallbacks<T> : MonoBehaviourPunCallbacks where T : SingletonPunCallbacks<T>
{
    public static T Instance { get; private set; }

    /// <summary>코드에서만 오버라이드. true면 Awake에서 DontDestroyOnLoad 호출.</summary>
    protected virtual bool PersistAcrossScenes => true;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 중복 인스턴스 감지. 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;
        if (PersistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
