using System.Threading.Tasks;

public static class CustomizationItemRepository
{
    private const string LabelPrefix = "cust_";

    public static Task<AddressableLoadListResult<CustomizationItemDefinition>> LoadItemsByPartAsync(
        CharacterCustomizationPart part)
    {
        string label = LabelPrefix + part;
        return AddressableAssetLoader.LoadAssetsAsync<CustomizationItemDefinition>(label);
    }
}
