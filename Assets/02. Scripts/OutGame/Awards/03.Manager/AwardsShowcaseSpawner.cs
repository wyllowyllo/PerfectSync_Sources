using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public sealed class AwardsShowcaseSpawner
{
    private readonly Transform _spawnFirstSlotInTeam;
    private readonly Transform _spawnSecondSlotInTeam;
    private readonly string _playerPrefabResourceName;

    public AwardsShowcaseSpawner(Transform spawnFirstSlotInTeam, Transform spawnSecondSlotInTeam, string playerPrefabResourceName)
    {
        _spawnFirstSlotInTeam = spawnFirstSlotInTeam;
        _spawnSecondSlotInTeam = spawnSecondSlotInTeam;
        _playerPrefabResourceName = playerPrefabResourceName;
    }

    public bool TrySpawnShowcasePlayer()
    {
        if (_spawnFirstSlotInTeam == null || _spawnSecondSlotInTeam == null)
            return false;

        int team = PhotonTeamManager.GetTeamRaw(PhotonNetwork.LocalPlayer);
        if (team == PhotonTeamManager.TeamNone)
            return false;

        int slot = GetSlotIndexInTeam(team);
        if (slot < 0)
            return false;

        Transform spawn = slot == 0 ? _spawnFirstSlotInTeam : _spawnSecondSlotInTeam;
        GameObject player = PhotonNetwork.Instantiate(_playerPrefabResourceName, spawn.position, spawn.rotation);

        foreach (var move in player.GetComponentsInChildren<PlayerMoveAbility>(true))
            move.enabled = false;
        foreach (var rot in player.GetComponentsInChildren<PlayerRotateAbility>(true))
            rot.enabled = false;

        var tracker = player.GetComponentInChildren<RaceProgressTracker>(true);
        if (tracker != null)
            tracker.enabled = false;

        return true;
    }

    private static int GetSlotIndexInTeam(int teamNumber)
    {
        int slot = 0;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (PhotonTeamManager.GetTeamRaw(p) != teamNumber) continue;
            if (p == PhotonNetwork.LocalPlayer)
                return slot;
            slot++;
        }

        return -1;
    }
}
