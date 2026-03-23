using Photon.Realtime;

public static class PlayerFinalRankReader
{
    public static bool TryGetFinalRank(Player player, out int rank)
    {
        rank = -1;
        if (player == null)
            return false;
        if (!player.CustomProperties.TryGetValue(InGameRaceKeys.FinalRankKey, out object rankObj))
            return false;

        rank = rankObj switch
        {
            int i => i,
            byte b => b,
            short s => s,
            _ => -1
        };

        return rank >= 1;
    }
}
