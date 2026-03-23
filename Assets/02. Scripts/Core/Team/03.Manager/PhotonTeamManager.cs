using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonTeamManager : SingletonPunCallbacks<PhotonTeamManager>
{
    protected override bool PersistAcrossScenes => true;

    public const string TeamKey = PhotonTeamPropertyKeys.Team;

    public const string PartyIdKey = PhotonTeamPropertyKeys.PartyId;
    public const int TeamNone = TeamId.NoneRaw;
    public const int MaxTeams = TeamRules.MaxTeams;
    public const int PlayersPerTeam = TeamRules.PlayersPerTeam;

    public event Action<Player, int> OnPlayerTeamChanged;
    public event Action OnAllTeamsAssigned;

    private readonly PhotonTeamStateRepository _teamState = new();
    private static readonly PhotonTeamStateRepository SharedTeamRead = new();

    /// <summary>
    /// 전원 팀 배정 완료 시 로그/이벤트는 한 번만 올리기 위한 래치.
    /// (같은 방에서 Team 프로퍼티가 여러 번 갱신되면 OnPlayerPropertiesUpdate가 중복 호출될 수 있음)
    /// </summary>
    private bool _allTeamsAssignedNotified;

    public static int GetTeamRaw(Player player)
    {
        if (player == null)
            return TeamNone;
        return SharedTeamRead.GetTeamRaw(player);
    }

    public static int GetLocalTeamRaw() => GetTeamRaw(PhotonNetwork.LocalPlayer);

    protected override void Awake()
    {
        base.Awake();
    }

    public bool SetTeam(int teamNumber)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[PhotonTeamManager] 방에 입장해 있지 않습니다.");
            return false;
        }

        if (!TeamRules.IsValidAssignedTeam(teamNumber))
        {
            Debug.LogWarning($"[PhotonTeamManager] 잘못된 팀 번호입니다: {teamNumber} (1~{TeamRules.MaxTeams})");
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
        var teams = new List<Player>[MaxTeams];
        for (int i = 0; i < MaxTeams; i++)
            teams[i] = new List<Player>();

        foreach (var party in partyGroups)
        {
            int targetTeam = FindTeamWithSpace(teams, party.Count);
            if (targetTeam < 0) continue;

            foreach (var player in party)
                teams[targetTeam].Add(player);
        }

        foreach (var player in soloPlayers)
        {
            int targetTeam = FindTeamWithSpace(teams, 1);
            if (targetTeam < 0) continue;

            teams[targetTeam].Add(player);
        }

        for (int t = 0; t < MaxTeams; t++)
        {
            int teamNumber = t + 1;
            foreach (var player in teams[t])
                player.SetCustomProperties(new Hashtable { { TeamKey, teamNumber } });
        }
    }

    private int FindTeamWithSpace(List<Player>[] teams, int requiredSlots)
    {
        for (int i = 0; i < teams.Length; i++)
        {
            if (teams[i].Count + requiredSlots <= PlayersPerTeam)
                return i;
        }
        return -1;
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

    public int GetPlayerTeam(Player player) => GetTeamRaw(player);

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

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        _allTeamsAssignedNotified = false;
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        _allTeamsAssignedNotified = false;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (!changedProps.ContainsKey(TeamKey)) return;

        int newTeam = (int)changedProps[TeamKey];
        Debug.Log($"[PhotonTeamManager] {targetPlayer.NickName} → 팀 {newTeam}");
        OnPlayerTeamChanged?.Invoke(targetPlayer, newTeam);

        if (!AreAllTeamsAssigned())
        {
            _allTeamsAssignedNotified = false;
            return;
        }

        if (_allTeamsAssignedNotified)
            return;

        _allTeamsAssignedNotified = true;
        Debug.Log("[PhotonTeamManager] 모든 플레이어 팀 배정 완료.");
        OnAllTeamsAssigned?.Invoke();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        _allTeamsAssignedNotified = false;
        OnPlayerTeamChanged?.Invoke(otherPlayer, TeamNone);
    }

}
