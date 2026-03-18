using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonTeamManager : SingletonPunCallbacks<PhotonTeamManager>
{
    public const string TeamKey = "team";
    public const string PartyIdKey = "pid";
    public const int TeamNone = 0;
    public const int MaxTeams = 4;
    public const int PlayersPerTeam = 2;

    public event Action<Player, int> OnPlayerTeamChanged;
    public event Action OnAllTeamsAssigned;

    #region Team Selection (Custom Room)

    public bool SetTeam(int teamNumber)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PhotonTeamManager] 방에 입장해 있지 않습니다.");
            return false;
        }

        if (teamNumber < 1 || teamNumber > MaxTeams)
        {
            Debug.LogWarning($"[PhotonTeamManager] 잘못된 팀 번호입니다: {teamNumber} (1~{MaxTeams})");
            return false;
        }

        if (IsTeamFull(teamNumber))
        {
            Debug.LogWarning($"[PhotonTeamManager] 팀 {teamNumber}이(가) 이미 가득 찼습니다.");
            return false;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { TeamKey, teamNumber } });
        Debug.Log($"[PhotonTeamManager] 팀 {teamNumber} 선택 완료.");
        return true;
    }

    public void LeaveTeam()
    {
        if (!PhotonNetwork.InRoom) return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { TeamKey, TeamNone } });
        Debug.Log("[PhotonTeamManager] 팀 해제 완료.");
    }

    #endregion

    #region Auto Assignment (Random Matching)

    public void AssignTeamsRandomly()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[PhotonTeamManager] MasterClient만 팀을 자동 배정할 수 있습니다.");
            return;
        }

        GroupPlayersByParty(out var partyGroups, out var soloPlayers);

        partyGroups.Shuffle();
        soloPlayers.Shuffle();

        AssignToTeams(partyGroups, soloPlayers);

        Debug.Log($"[PhotonTeamManager] 랜덤 팀 배정 완료. " +
                  $"({PhotonNetwork.PlayerList.Length}명 → {MaxTeams}팀, 파티 그룹: {partyGroups.Count}개)");
    }

    private void GroupPlayersByParty(out List<List<Player>> partyGroups, out List<Player> soloPlayers)
    {
        var groups = new Dictionary<string, List<Player>>();
        soloPlayers = new List<Player>();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            string pid = GetPartyId(player);
            if (string.IsNullOrEmpty(pid))
            {
                soloPlayers.Add(player);
            }
            else
            {
                if (!groups.ContainsKey(pid))
                    groups[pid] = new List<Player>();
                groups[pid].Add(player);
            }
        }

        partyGroups = new List<List<Player>>(groups.Values);
    }

    private void AssignToTeams(List<List<Player>> partyGroups, List<Player> soloPlayers)
    {
        int teamNumber = 1;
        int assigned = 0;

        foreach (var party in partyGroups)
        {
            int remaining = PlayersPerTeam - assigned;
            if (party.Count > remaining && assigned > 0)
            {
                teamNumber++;
                assigned = 0;
            }

            foreach (var player in party)
            {
                player.SetCustomProperties(new Hashtable { { TeamKey, teamNumber } });
                assigned++;

                if (assigned >= PlayersPerTeam)
                {
                    teamNumber++;
                    assigned = 0;
                }
            }
        }

        foreach (var player in soloPlayers)
        {
            player.SetCustomProperties(new Hashtable { { TeamKey, teamNumber } });
            assigned++;

            if (assigned >= PlayersPerTeam)
            {
                teamNumber++;
                assigned = 0;
            }
        }
    }

    private string GetPartyId(Player player)
    {
        if (player.CustomProperties.TryGetValue(PartyIdKey, out object pidObj))
            return pidObj as string;

        return null;
    }

    public void ClearAllTeams()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            player.SetCustomProperties(new Hashtable { { TeamKey, TeamNone } });
        }
    }

    #endregion

    #region Query

    public int GetPlayerTeam(Player player)
    {
        if (player.CustomProperties.TryGetValue(TeamKey, out object teamObj))
            return (int)teamObj;

        return TeamNone;
    }

    public List<Player> GetTeamMembers(int teamNumber)
    {
        var members = new List<Player>();
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (GetPlayerTeam(player) == teamNumber)
                members.Add(player);
        }
        return members;
    }

    public bool IsTeamFull(int teamNumber)
    {
        return GetTeamMembers(teamNumber).Count >= PlayersPerTeam;
    }

    public bool AreAllTeamsAssigned()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (GetPlayerTeam(player) == TeamNone)
                return false;
        }
        return PhotonNetwork.PlayerList.Length > 0;
    }

    #endregion

    #region PUN Callbacks

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (!changedProps.ContainsKey(TeamKey)) return;

        int newTeam = (int)changedProps[TeamKey];
        Debug.Log($"[PhotonTeamManager] {targetPlayer.NickName} → 팀 {newTeam}");
        OnPlayerTeamChanged?.Invoke(targetPlayer, newTeam);

        if (newTeam != TeamNone && AreAllTeamsAssigned())
        {
            Debug.Log("[PhotonTeamManager] 모든 플레이어 팀 배정 완료.");
            OnAllTeamsAssigned?.Invoke();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        OnPlayerTeamChanged?.Invoke(otherPlayer, TeamNone);
    }

    #endregion

}
