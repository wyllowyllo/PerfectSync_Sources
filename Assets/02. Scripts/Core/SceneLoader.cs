using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class SceneLoader : SingletonMonoBehaviour<SceneLoader>
{
    public void LoadSceneLocal(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneLoader] 씬 이름이 비어 있습니다.");
            return;
        }
        
        SceneManager.LoadScene(sceneName);
    }

    public void LoadScenePhoton(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneLoader] 씬 이름이 비어 있습니다.");
            return;
        }

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[SceneLoader] 방에 없어 Photon 씬 로드를 하지 않습니다.");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[SceneLoader] PhotonNetwork.LoadLevel은 마스터 클라이언트만 호출할 수 있습니다.");
            return;
        }

        PhotonNetwork.LoadLevel(sceneName);
    }
}
