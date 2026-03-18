using UnityEngine;
using Unity.Cinemachine;
using Photon.Pun;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private string _playerPrefabName = "PlayerPrefab";

    [Header("Camera")]
    [SerializeField] private CinemachineCamera _followCamera;

    public void Spawn(Vector3 position, Quaternion rotation)
    {
        if (!PhotonNetwork.InRoom) return;

        GameObject player = PhotonNetwork.Instantiate(_playerPrefabName, position, rotation);

        var rotateAbility = player.GetComponent<PlayerRotateAbility>();
        if (rotateAbility != null)
            rotateAbility.SetFollowCamera(_followCamera);
    }
}
