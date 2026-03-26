using UnityEngine;
using Unity.Cinemachine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerSpawner : MonoBehaviour
{
    private readonly PhotonTeamStateRepository _teamState = new();

    [Header("Spawn Settings")]
    [SerializeField] private string _playerPrefabName = "PlayerPrefab";

    [Header("Team Spawn Points")]
    [SerializeField] private Transform[] _team1Points;
    [SerializeField] private Transform[] _team2Points;
    [SerializeField] private Transform[] _team3Points;
    [SerializeField] private Transform[] _team4Points;

    [Header("TeamCharacter Spawn")]
    [SerializeField] private string _teamCharacterPrefabName = "TeamCharacter";
    [SerializeField] private Transform[] _teamCharacterSpawnPoints = new Transform[4];
    [SerializeField] private Vector3 _fallbackBasePosition = Vector3.zero;
    [SerializeField] private float _fallbackSpacing = 5f;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera _followCamera;

    private Transform[][] _teamSpawnPoints;

    private void Awake()
    {
        _teamSpawnPoints = new[] { _team1Points, _team2Points, _team3Points, _team4Points };
    }

    public GameObject SpawnByTeam()
    {
        if (!PhotonNetwork.InRoom) return null;

        int team = _teamState.GetTeamRaw(PhotonNetwork.LocalPlayer);
        if (team == PhotonTeamManager.TeamNone)
            return null;

        int teamIndex = team - 1;
        Transform[] points = _teamSpawnPoints[teamIndex];

        int slotIndex = GetSlotIndexInTeam(team);
        if (slotIndex < 0 || slotIndex >= points.Length)
            return null;

        Transform spawnPoint = points[slotIndex];
        GameObject player = PhotonNetwork.Instantiate(_playerPrefabName, spawnPoint.position, spawnPoint.rotation);

        var rotateAbility = player.GetComponent<PlayerRotateAbility>();
        if (rotateAbility != null)
            rotateAbility.SetFollowCamera(_followCamera);

        var tracker = player.GetComponent<RaceProgressTracker>();
        if (tracker != null)
            tracker.SetTeam(team);

        return player;
    }

    public void Spawn(Vector3 position, Quaternion rotation)
    {
        if (!PhotonNetwork.InRoom) return;

        GameObject player = PhotonNetwork.Instantiate(_playerPrefabName, position, rotation);

        var rotateAbility = player.GetComponent<PlayerRotateAbility>();
        if (rotateAbility != null)
            rotateAbility.SetFollowCamera(_followCamera);
    }

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

        var tracker = character.GetComponent<RaceProgressTracker>();
        if (tracker != null)
            tracker.SetTeam(teamNumber);

        return character;
    }

    private int GetSlotIndexInTeam(int teamNumber)
    {
        int slot = 0;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (_teamState.GetTeamRaw(p) == teamNumber)
            {
                if (p == PhotonNetwork.LocalPlayer)
                    return slot;
                slot++;
            }
        }
        return -1;
    }

}
