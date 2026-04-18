public static class PhotonEventCodes
{
    public const byte PartyInvite = 111;
    public const byte PartyInviteResponse = 112;
    public const byte PartyDisband = 113;
    /// <summary>같은 partyId·partyMembers를 상대 로컬 커스텀 프로퍼티에 맞춤.</summary>
    public const byte PartyStateSync = 114;
    public const byte MatchConfirmed = 120;

    public const byte ObstacleDestroy = 130;
    public const byte ObstacleRespawn = 131;
    public const byte ObstaclePositionSync = 132;
    public const byte ObstacleHide = 133;
    public const byte ObstacleRespawnWarning = 134;
    public const byte ObstacleReveal = 135;
}
