using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmSceneRouter : MonoBehaviour
{
    [Serializable]
    private class SceneBgmEntry
    {
        public string SceneName;
        public string BgmAddress;
        public float CrossfadeDuration = 1f;
    }

    [SerializeField] private SceneBgmEntry[] _entries;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        // sceneLoaded는 구독 이후의 씬 로드만 발화. 초기 씬은 수동 트리거.
        ApplyBgmForScene(SceneManager.GetActiveScene().name);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyBgmForScene(scene.name);
    }

    private void ApplyBgmForScene(string sceneName)
    {
        if (_entries == null)
            return;

        SceneBgmEntry entry = FindEntry(sceneName);
        if (entry == null || string.IsNullOrEmpty(entry.BgmAddress))
            return;

        AudioManager.Instance?.PlayBgmByAddress(entry.BgmAddress, entry.CrossfadeDuration);
    }

    private SceneBgmEntry FindEntry(string sceneName)
    {
        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] != null && _entries[i].SceneName == sceneName)
                return _entries[i];
        }
        return null;
    }
}
