using System;

public readonly struct TeamId : IEquatable<TeamId>
{
    public const int NoneRaw = 0;

    private readonly int _value;

    private TeamId(int value) => _value = value;

    public static TeamId None => default;

    public static TeamId FromPhotonRaw(int raw)
    {
        if (raw == NoneRaw)
            return None;
        if (TeamRules.IsValidAssignedTeam(raw))
            return new TeamId(raw);
        return None;
    }

    public int ToRaw() => _value;

    public bool IsNone => _value == NoneRaw;

    public bool Equals(TeamId other) => _value == other._value;

    public override bool Equals(object obj) => obj is TeamId other && Equals(other);

    public override int GetHashCode() => _value;

    public static bool operator ==(TeamId a, TeamId b) => a.Equals(b);

    public static bool operator !=(TeamId a, TeamId b) => !a.Equals(b);
}
