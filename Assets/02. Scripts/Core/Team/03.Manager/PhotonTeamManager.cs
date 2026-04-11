using System;
using System.Collections.Generic;
using System.Linq;
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

    public const string TeamSlotKey = PhotonTeamPropertyKeys.TeamSlot;
    public const int SlotNone = TeamRules.SlotNone;
    public const int SlotHost = TeamRules.SlotHost;

    public event Action<Player, int> OnPlayerTeamChanged;
    public event Action OnAllTeamsAssigned;

    private readonly PhotonTeamStateRepository _teamState = new();
    private static readonly PhotonTeamStateRepository SharedTeamRead = new();

    private bool _allTeamsAssignedNotified;

    public static int GetTeamRaw(Player player)
    {
        if (player == null)
            return TeamNone;
        return SharedTeamRead.GetTeamRaw(player);
    }

    public static int GetLocalTeamRaw() => GetTeamRaw(PhotonNetwork.LocalPlayer);

    public static int GetTeamSlot(Player player)
    {
        if (player == null) return SlotNone;
        if (player.CustomProperties.TryGetValue(TeamSlotKey, out object slotObj) && slotObj is int slot)
            return slot;
        return SlotNone;
    }

    public static int GetLocalTeamSlot() => GetTeamSlot(PhotonNetwork.LocalPlayer);

    public static bool IsLocalSlotHost() => GetLocalTeamSlot() == SlotHost;

    protected override void Awake()
    {
        base.Awake();
    }

    public bool SetTeam(int teamNumber)
    {
        if (!PhotonNetwork.InRoom)
            return false;

        if (!TeamRules.IsValidAssignedTeam(teamNumber))
            return false;

        if (IsTeamFull(teamNumber))
            return false;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { TeamKey, teamNumber } });
        return true;
    }

    public void LeaveTeam()
    {
        if (!PhotonNetwork.InRoom) return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { TeamKey, TeamNone },
            { TeamSlotKey, SlotNone }
        });
    }

    public void AssignTeamsRandomly()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        GroupPlayersByParty(out var partyGroups, out var soloPlayers);

        partyGroups.Shuffle();
        soloPlayers.Shuffle();

        AssignToTeams(partyGroups, soloPlayers);
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
        var teams = Enumerable.Range(0, MaxTeams).Select(_ => new List<Player>()).ToArray();

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
            var sorted = teams[t].OrderBy(p => p.ActorNumber).ToList();
            for (int s = 0; s < sorted.Count; s++)
            {
                int slot = s + 1;
                sorted[s].SetCustomProperties(new Hashtable
                {
                    { TeamKey, teamNumber },
                    { TeamSlotKey, slot }
                });
            }
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
            player.SetCustomProperties(new Hashtable
            {
                { TeamKey, TeamNone },
                { TeamSlotKey, SlotNone }
            });
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
        OnPlayerTeamChanged?.Invoke(targetPlayer, newTeam);

        if (!AreAllTeamsAssigned())
        {
            _allTeamsAssignedNotified = false;
            return;
        }

        if (_allTeamsAssignedNotified)
            return;

        _allTeamsAssignedNotified = true;
        OnAllTeamsAssigned?.Invoke();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        _allTeamsAssignedNotified = false;
        OnPlayerTeamChanged?.Invoke(otherPlayer, TeamNone);
    }

}
