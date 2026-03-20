using UnityEngine;
using Unity.Cinemachine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private string _playerPrefabName = "PlayerPrefab";

    [Header("Team Spawn Points")]
    [SerializeField] private Transform[] _team1Points;
    [SerializeField] private Transform[] _team2Points;
    [SerializeField] private Transform[] _team3Points;
    [SerializeField] private Transform[] _team4Points;

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

        int team = GetPlayerTeam(PhotonNetwork.LocalPlayer);
        if (team == PhotonTeamManager.TeamNone)
        {
            Debug.LogWarning("[PlayerSpawner] 팀이 배정되지 않은 플레이어입니다.");
            return null;
        }

        int teamIndex = team - 1;
        Transform[] points = _teamSpawnPoints[teamIndex];

        int slotIndex = GetSlotIndexInTeam(team);
        if (slotIndex < 0 || slotIndex >= points.Length)
        {
            Debug.LogWarning($"[PlayerSpawner] 팀 {team}에 유효한 스폰 포인트가 없습니다. (slot: {slotIndex})");
            return null;
        }

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

    private int GetSlotIndexInTeam(int teamNumber)
    {
        int slot = 0;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (GetPlayerTeam(p) == teamNumber)
            {
                if (p == PhotonNetwork.LocalPlayer)
                    return slot;
                slot++;
            }
        }
        return -1;
    }

    private int GetPlayerTeam(Player player)
    {
        if (player.CustomProperties.TryGetValue(PhotonTeamManager.TeamKey, out object teamObj))
            return (int)teamObj;
        return PhotonTeamManager.TeamNone;
    }
}
