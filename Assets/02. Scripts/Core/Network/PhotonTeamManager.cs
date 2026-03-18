using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonTeamManager : SingletonPunCallbacks<PhotonTeamManager>
{
    public const string TEAM_KEY = "team";
    public const string PARTY_ID_KEY = "pid";
    public const int TEAM_NONE = 0;
    public const int MAX_TEAMS = 4;
    public const int PLAYERS_PER_TEAM = 2;

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

        if (teamNumber < 1 || teamNumber > MAX_TEAMS)
        {
            Debug.LogWarning($"[PhotonTeamManager] 잘못된 팀 번호입니다: {teamNumber} (1~{MAX_TEAMS})");
            return false;
        }

        if (IsTeamFull(teamNumber))
        {
            Debug.LogWarning($"[PhotonTeamManager] 팀 {teamNumber}이(가) 이미 가득 찼습니다.");
            return false;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { TEAM_KEY, teamNumber } });
        Debug.Log($"[PhotonTeamManager] 팀 {teamNumber} 선택 완료.");
        return true;
    }

    public void LeaveTeam()
    {
        if (!PhotonNetwork.InRoom) return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { TEAM_KEY, TEAM_NONE } });
        Debug.Log("[PhotonTeamManager] 팀 해제 완료.");
    }

    #endregion

    #region Auto Assignment (Random Matching)

    /// <summary>
    /// MasterClient 전용. 현재 방의 모든 플레이어를 4팀 x 2명으로 랜덤 배정합니다.
    /// 같은 partyId를 가진 플레이어는 같은 팀에 배정됩니다.
    /// </summary>
    public void AssignTeamsRandomly()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[PhotonTeamManager] MasterClient만 팀을 자동 배정할 수 있습니다.");
            return;
        }

        var allPlayers = new List<Player>(PhotonNetwork.PlayerList);
        var partyGroups = new Dictionary<string, List<Player>>();
        var soloPlayers = new List<Player>();

        foreach (var player in allPlayers)
        {
            string pid = GetPartyId(player);
            if (string.IsNullOrEmpty(pid))
            {
                soloPlayers.Add(player);
            }
            else
            {
                if (!partyGroups.ContainsKey(pid))
                    partyGroups[pid] = new List<Player>();
                partyGroups[pid].Add(player);
            }
        }

        var partyList = new List<List<Player>>(partyGroups.Values);
        partyList.Shuffle();
        soloPlayers.Shuffle();

        int teamNumber = 1;
        int assigned = 0;

        foreach (var party in partyList)
        {
            foreach (var player in party)
            {
                player.SetCustomProperties(new Hashtable { { TEAM_KEY, teamNumber } });
                assigned++;
            }

            if (assigned >= PLAYERS_PER_TEAM)
            {
                teamNumber++;
                assigned = 0;
            }
        }

        foreach (var player in soloPlayers)
        {
            player.SetCustomProperties(new Hashtable { { TEAM_KEY, teamNumber } });
            assigned++;

            if (assigned >= PLAYERS_PER_TEAM)
            {
                teamNumber++;
                assigned = 0;
            }
        }

        Debug.Log($"[PhotonTeamManager] 랜덤 팀 배정 완료. ({allPlayers.Count}명 → {MAX_TEAMS}팀, 파티 그룹: {partyGroups.Count}개)");
    }

    private string GetPartyId(Player player)
    {
        if (player.CustomProperties.TryGetValue(PARTY_ID_KEY, out object pidObj))
            return pidObj as string;

        return null;
    }

    /// <summary>
    /// 방의 모든 플레이어 팀을 초기화합니다 (team = 0).
    /// </summary>
    public void ClearAllTeams()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            player.SetCustomProperties(new Hashtable { { TEAM_KEY, TEAM_NONE } });
        }
    }

    #endregion

    #region Query

    public int GetPlayerTeam(Player player)
    {
        if (player.CustomProperties.TryGetValue(TEAM_KEY, out object teamObj))
            return (int)teamObj;

        return TEAM_NONE;
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
        return GetTeamMembers(teamNumber).Count >= PLAYERS_PER_TEAM;
    }

    public bool AreAllTeamsAssigned()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (GetPlayerTeam(player) == TEAM_NONE)
                return false;
        }
        return PhotonNetwork.PlayerList.Length > 0;
    }

    #endregion

    #region PUN Callbacks

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (!changedProps.ContainsKey(TEAM_KEY)) return;

        int newTeam = (int)changedProps[TEAM_KEY];
        Debug.Log($"[PhotonTeamManager] {targetPlayer.NickName} → 팀 {newTeam}");
        OnPlayerTeamChanged?.Invoke(targetPlayer, newTeam);

        if (newTeam != TEAM_NONE && AreAllTeamsAssigned())
        {
            Debug.Log("[PhotonTeamManager] 모든 플레이어 팀 배정 완료.");
            OnAllTeamsAssigned?.Invoke();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        OnPlayerTeamChanged?.Invoke(otherPlayer, TEAM_NONE);
    }

    #endregion

}
