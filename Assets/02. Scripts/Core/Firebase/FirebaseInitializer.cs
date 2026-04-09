using System;
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

    private const string LockFilePrefix = "firebase_instance_";
    private const int MaxInstanceSlots = 8;

    private FileStream _lockFileStream;
    private bool _isPrimaryInstance;
    private int _instanceIndex = -1;

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
            _instanceIndex = AcquireAvailableSlot(startFrom: 1);
            _isPrimaryInstance = false;
            InitializeFirebaseAsync();
            return;
        }
    #endif

        _instanceIndex = AcquireAvailableSlot(startFrom: 0);
        _isPrimaryInstance = (_instanceIndex == 0);
        InitializeFirebaseAsync();
#else
        Debug.LogWarning("[FirebaseInitializer] WebGL 빌드: Firebase 초기화 건너뜀");
        IsInitialized = false;
#endif
    }

    private int AcquireAvailableSlot(int startFrom)
    {
        for (int i = startFrom; i < MaxInstanceSlots; i++)
        {
            string lockPath = Path.Combine(
                Application.persistentDataPath,
                $"{LockFilePrefix}{i}.lock");
            try
            {
                _lockFileStream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return i;
            }
            catch (IOException)
            {
                // 이미 점유된 슬롯 → 다음 슬롯 시도
            }
        }

        Debug.LogWarning("[FirebaseInitializer] 모든 슬롯 점유, fallback 인스턴스 사용");
        return -1;
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
            string appName = _instanceIndex >= 0
                ? $"FirebaseInstance_{_instanceIndex}"
                : $"FirebaseInstance_{Guid.NewGuid():N}";

            var secondaryApp = FirebaseApp.Create(App.Options, appName);
            Auth = FirebaseAuth.GetAuth(secondaryApp);
            DB = FirebaseFirestore.GetInstance(secondaryApp);
            Debug.Log($"[FirebaseInitializer] 보조 인스턴스 생성: {appName}");
        }

        IsInitialized = true;

        string mode = _isPrimaryInstance
            ? "주 인스턴스 (슬롯 0)"
            : $"보조 인스턴스 (슬롯 {_instanceIndex})";
        Debug.Log($"[FirebaseInitializer] Firebase 초기화 성공 ({mode})");
    }
#endif

    private void ReleaseFirebaseLock()
    {
        if (_lockFileStream != null)
        {
            _lockFileStream.Dispose();
            _lockFileStream = null;

            if (_instanceIndex >= 0)
            {
                try
                {
                    string lockPath = Path.Combine(
                        Application.persistentDataPath,
                        $"{LockFilePrefix}{_instanceIndex}.lock");
                    if (File.Exists(lockPath))
                        File.Delete(lockPath);
                }
                catch { }
            }
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
