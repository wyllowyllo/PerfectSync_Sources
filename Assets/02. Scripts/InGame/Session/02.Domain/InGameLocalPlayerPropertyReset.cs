using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public static class InGameLocalPlayerPropertyReset
{
    public static void ApplyForLobbyScene(bool clearTeamBecauseNotInRoom)
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null)
            return;

        var ht = new Hashtable
        {
            [InGameRaceKeys.ReadyKey] = false,
            [InGameRaceKeys.RaceDoneKey] = false,
            [InGameRaceKeys.FinalRankKey] = null
        };

        if (clearTeamBecauseNotInRoom)
            ht[PhotonTeamManager.TeamKey] = PhotonTeamManager.TeamNone;

        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }
}
