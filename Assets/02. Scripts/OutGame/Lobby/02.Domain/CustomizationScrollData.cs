using Gpm.Ui;

public class CustomizationScrollData : InfiniteScrollData
{
    public CustomizationItemDefinition Definition { get; }

    public CustomizationScrollData(CustomizationItemDefinition definition)
    {
        Definition = definition;
    }
}
