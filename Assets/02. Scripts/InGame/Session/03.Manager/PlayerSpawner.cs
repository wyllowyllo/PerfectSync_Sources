using UnityEngine;
using Photon.Pun;

public class PlayerSpawner : MonoBehaviour
{
    [Header("TeamCharacter Spawn")]
    [SerializeField] private string _teamCharacterPrefabName = "TeamCharacter";
    [SerializeField] private Transform[] _teamCharacterSpawnPoints = new Transform[4];
    [SerializeField] private Vector3 _fallbackBasePosition = Vector3.zero;
    [SerializeField] private float _fallbackSpacing = 5f;

    public GameObject SpawnTeamCharacter(int teamNumber)
    {
        if (!PhotonNetwork.InRoom) return null;

        int index = teamNumber - 1;
        if (index < 0 || index >= PhotonTeamManager.MaxTeams)
        {
            Debug.LogWarning($"[PlayerSpawner] Invalid team number: {teamNumber}");
            return null;
        }

        Vector3 position;
        Quaternion rotation;

        if (index < _teamCharacterSpawnPoints.Length && _teamCharacterSpawnPoints[index] != null)
        {
            position = _teamCharacterSpawnPoints[index].position;
            rotation = _teamCharacterSpawnPoints[index].rotation;
        }
        else
        {
            position = _fallbackBasePosition + Vector3.right * index * _fallbackSpacing;
            rotation = Quaternion.identity;
        }

        GameObject character = PhotonNetwork.Instantiate(_teamCharacterPrefabName, position, rotation);

        var trackers = character.GetComponentsInChildren<RaceProgressTracker>(true);
        foreach (var tracker in trackers)
            tracker.SetTeam(teamNumber);

        return character;
    }
}
