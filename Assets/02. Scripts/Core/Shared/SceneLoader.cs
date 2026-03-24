using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class SceneLoader : SingletonMonoBehaviour<SceneLoader>
{
    protected override bool PersistAcrossScenes => true;

    protected override void Awake()
    {
        base.Awake();
    }

    public void LoadSceneLocal(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        SceneManager.LoadScene(sceneName);
    }

    public void LoadScenePhoton(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (!PhotonNetwork.InRoom)
            return;

        if (!PhotonNetwork.IsMasterClient)
            return;

        PhotonNetwork.LoadLevel(sceneName);
    }
}
