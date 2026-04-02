public static class PhotonEventCodes
{
    public const byte PartyInvite = 111;
    public const byte PartyInviteResponse = 112;
    public const byte PartyDisband = 113;
    /// <summary>같은 partyId·partyMembers를 상대 로컬 커스텀 프로퍼티에 맞춤.</summary>
    public const byte PartyStateSync = 114;
    public const byte MatchConfirmed = 120;

    public const byte ObstacleFreeze = 130;
    public const byte ObstacleUnfreeze = 131;
}
