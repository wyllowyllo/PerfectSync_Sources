using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

public static class CustomizationPhotonKeys
{
    public static string GetKey(CharacterCustomizationPart part)
    {
        return "cust_" + part;
    }

    public static void SetLocalPlayerSlotIndex(CharacterCustomizationPart part, int slotIndex)
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null)
            return;

        var ht = new Hashtable { { GetKey(part), slotIndex } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(ht);
    }

    public static bool TryGetSlotIndex(Player player, CharacterCustomizationPart part, out int slotIndex)
    {
        slotIndex = 0;
        if (player == null || !player.CustomProperties.TryGetValue(GetKey(part), out object v) || v == null)
            return false;

        if (v is int i)
        {
            slotIndex = i;
            return true;
        }

        return false;
    }
}
