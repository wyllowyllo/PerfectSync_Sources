using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// PlayerSpawner 패턴을 참고한 TeamCharacter 전용 스포너.
    /// 팀당 1개의 TeamCharacter를 Host만 스폰한다.
    /// </summary>
    public class TeamCharacterSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private string _prefabName = "TeamCharacter";

        [Header("Team Spawn Points (팀별 1개)")]
        [SerializeField] private Transform[] _teamSpawnPoints = new Transform[4];

        [Header("Fallback (스폰포인트 미설정 시)")]
        [SerializeField] private Vector3 _fallbackBasePosition = Vector3.zero;
        [SerializeField] private float _fallbackSpacing = 5f;

        public GameObject SpawnByTeam(int teamNumber)
        {
            if (!PhotonNetwork.InRoom) return null;

            int index = teamNumber - 1;
            if (index < 0 || index >= PhotonTeamManager.MaxTeams)
            {
                Debug.LogWarning($"[TeamCharacterSpawner] Invalid team number: {teamNumber}");
                return null;
            }

            Vector3 position;
            Quaternion rotation;

            if (index < _teamSpawnPoints.Length && _teamSpawnPoints[index] != null)
            {
                position = _teamSpawnPoints[index].position;
                rotation = _teamSpawnPoints[index].rotation;
            }
            else
            {
                position = _fallbackBasePosition + Vector3.right * index * _fallbackSpacing;
                rotation = Quaternion.identity;
            }

            GameObject character = PhotonNetwork.Instantiate(_prefabName, position, rotation);
            Debug.Log($"[TeamCharacterSpawner] Spawned {_prefabName} for team {teamNumber} at {position}");
            return character;
        }
    }
}
