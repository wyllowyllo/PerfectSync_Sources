using System.Collections;
using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyStartSequence : MonoBehaviour
{
    private const string FallbackSceneName = "InGame_1";

    private Coroutine _loadCoroutine;

    public bool IsRunning => _loadCoroutine != null;

    public void Begin()
    {
        if (_loadCoroutine != null) return;
        _loadCoroutine = StartCoroutine(LoadSelectedMapScene());
    }

    public void Cancel()
    {
        if (_loadCoroutine == null) return;
        StopCoroutine(_loadCoroutine);
        _loadCoroutine = null;
    }

    private IEnumerator LoadSelectedMapScene()
    {
        if (SceneLoader.Instance != null)
        {
            string sceneName = GetSelectedMapSceneName();
            SceneLoader.Instance.LoadScenePhoton(sceneName);
        }

        _loadCoroutine = null;
        yield break;
    }

    private static string GetSelectedMapSceneName()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return FallbackSceneName;

        if (!PhotonNetwork.CurrentRoom.CustomProperties
                .TryGetValue(MapSelectionManager.SelectedMapKey, out object val))
            return FallbackSceneName;

        int mapNumber = (int)val;
        return $"InGame_{mapNumber}";
    }
}
