using Photon.Realtime;


public static class LobbyCustomizationPhotonApplier
{
    public static void ApplyFromPlayer(Player player, CustomizationPartItemsActivator activator)
    {
        if (player == null || activator == null)
            return;

        foreach (CharacterCustomizationPart part in System.Enum.GetValues(typeof(CharacterCustomizationPart)))
        {
            if (!activator.HasSlotFor(part))
                continue;

            int index = CustomizationPhotonKeys.TryGetSlotIndex(player, part, out int idx)
                ? idx
                : DefaultSlotIndex(part);

            activator.SetPartItemIndex(part, index);
        }
    }

    private static int DefaultSlotIndex(CharacterCustomizationPart part)
    {
        return part == CharacterCustomizationPart.BodyColor ? 1 : 0;
    }
}
