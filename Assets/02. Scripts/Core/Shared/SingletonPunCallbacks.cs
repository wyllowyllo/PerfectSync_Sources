using UnityEngine;
using Photon.Pun;

public abstract class SingletonPunCallbacks<T> : MonoBehaviourPunCallbacks where T : SingletonPunCallbacks<T>
{
    public static T Instance { get; private set; }

    protected virtual bool PersistAcrossScenes => true;

    protected bool IsDuplicateInstance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            IsDuplicateInstance = true;
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
