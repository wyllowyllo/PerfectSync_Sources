using Photon.Realtime;

public sealed class PhotonTeamStateRepository
{
    public TeamId GetTeamId(Player player)
    {
        if (player.CustomProperties.TryGetValue(PhotonTeamPropertyKeys.Team, out object teamObj))
            return TeamId.FromPhotonRaw((int)teamObj);
        return TeamId.None;
    }

    public int GetTeamRaw(Player player) => GetTeamId(player).ToRaw();
}
