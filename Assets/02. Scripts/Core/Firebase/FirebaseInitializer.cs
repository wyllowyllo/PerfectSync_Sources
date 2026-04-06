using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
#endif

public class FirebaseInitializer : MonoBehaviour
{
    private static FirebaseInitializer s_instance;

    public static FirebaseInitializer Instance
    {
        get
        {
            if (s_instance == null)
            {
                var go = new GameObject(nameof(FirebaseInitializer));
                s_instance = go.AddComponent<FirebaseInitializer>();
                DontDestroyOnLoad(go);
            }
            return s_instance;
        }
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    public FirebaseApp App { get; private set; }
    public FirebaseAuth Auth { get; private set; }
    public FirebaseFirestore DB { get; private set; }
#endif

    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (s_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        InitializeFirebaseAsync();
#else
        Debug.LogWarning("[FirebaseInitializer] WebGL 빌드: Firebase 초기화 건너뜀");
        IsInitialized = false;
#endif
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private async void InitializeFirebaseAsync()
    {
        var result = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (result == DependencyStatus.Available)
        {
            App = FirebaseApp.DefaultInstance;
            Auth = FirebaseAuth.DefaultInstance;
            DB = FirebaseFirestore.DefaultInstance;
            IsInitialized = true;
            Debug.Log("[FirebaseInitializer] Firebase 초기화 성공");
        }
        else
        {
            Debug.LogError($"[FirebaseInitializer] Firebase 초기화 실패: {result}");
        }
    }
#endif
}
