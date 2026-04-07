using System.IO;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
#endif

public class FirebaseInitializer : MonoBehaviour
{
    private static FirebaseInitializer s_instance;
    private const string LockFileName = "firebase.lock";
    private FileStream _lockFileStream;
    private bool _isPrimaryInstance;

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
    #if UNITY_EDITOR
        if (ParrelSync.ClonesManager.IsClone())
        {
            _isPrimaryInstance = false;
            InitializeFirebaseAsync();
            return;
        }
    #endif

        _isPrimaryInstance = TryAcquireFirebaseLock();
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

        if (result != DependencyStatus.Available)
        {
            Debug.LogError($"[FirebaseInitializer] Firebase 초기화 실패: {result}");
            return;
        }

        App = FirebaseApp.DefaultInstance;

        if (_isPrimaryInstance)
        {
            Auth = FirebaseAuth.DefaultInstance;
            DB = FirebaseFirestore.DefaultInstance;
        }
        else
        {
            var secondaryApp = FirebaseApp.Create(App.Options, "SecondaryInstance");
            Auth = FirebaseAuth.GetAuth(secondaryApp);
            DB = FirebaseFirestore.GetInstance(secondaryApp);
            Debug.LogWarning("[FirebaseInitializer] 보조 인스턴스 → 별도 FirebaseApp으로 캐시 분리");
        }

        IsInitialized = true;

        string mode = _isPrimaryInstance ? "주 인스턴스" : "보조 인스턴스 (캐시 분리)";
        Debug.Log($"[FirebaseInitializer] Firebase 초기화 성공 ({mode})");
    }
#endif

    private bool TryAcquireFirebaseLock()
    {
        try
        {
            string lockPath = Path.Combine(Application.persistentDataPath, LockFileName);
            _lockFileStream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void ReleaseFirebaseLock()
    {
        if (_lockFileStream != null)
        {
            _lockFileStream.Dispose();
            _lockFileStream = null;

            try
            {
                string lockPath = Path.Combine(Application.persistentDataPath, LockFileName);
                if (File.Exists(lockPath))
                    File.Delete(lockPath);
            }
            catch { }
        }
    }

    private void OnApplicationQuit()
    {
        ReleaseFirebaseLock();
    }

    private void OnDestroy()
    {
        ReleaseFirebaseLock();
    }
}
