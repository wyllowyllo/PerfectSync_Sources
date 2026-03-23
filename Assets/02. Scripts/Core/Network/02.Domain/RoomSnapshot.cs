public readonly struct RoomSnapshot
{
    public bool IsValid { get; }
    public string Name { get; }
    public int PlayerCount { get; }
    public int MaxPlayers { get; }
    public RoomKind Kind { get; }

    private RoomSnapshot(bool isValid, string name, int playerCount, int maxPlayers, RoomKind kind)
    {
        IsValid = isValid;
        Name = name;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
        Kind = kind;
    }

    public static RoomSnapshot Invalid => default;

    public static RoomSnapshot Create(string name, int playerCount, int maxPlayers, RoomKind kind) =>
        new RoomSnapshot(true, name ?? string.Empty, playerCount, maxPlayers, kind);
}
