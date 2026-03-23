using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyStartSequence : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string _inGameSceneName = "InGame";

    [Header("Settings")]
    [SerializeField] private float _countdownSeconds = 3f;

    private Coroutine _countdownCoroutine;

    public bool IsRunning => _countdownCoroutine != null;

    public void Begin(Action<string> onStatusLine)
    {
        if (_countdownCoroutine != null) return;
        _countdownCoroutine = StartCoroutine(CountdownAndLoadScene(onStatusLine));
    }

    public void Cancel()
    {
        if (_countdownCoroutine == null) return;
        StopCoroutine(_countdownCoroutine);
        _countdownCoroutine = null;
    }

    private IEnumerator CountdownAndLoadScene(Action<string> onStatusLine)
    {
        float remaining = _countdownSeconds;

        while (remaining > 0f)
        {
            onStatusLine?.Invoke($"{Mathf.CeilToInt(remaining)}초 후에 게임을 시작합니다.");
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        onStatusLine?.Invoke("게임을 시작합니다...");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScenePhoton(_inGameSceneName);
        else
            Debug.LogError("[LobbyStartSequence] SceneLoader 싱글톤이 없습니다. 로비 씬에 SceneLoader를 배치하세요.");

        _countdownCoroutine = null;
    }
}
