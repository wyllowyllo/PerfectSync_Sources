using System;

[Serializable]
public class MatchQueueDto
{
    public MatchQueueEntryDto[] entries;
}

[Serializable]
public class MatchQueueEntryDto
{
    public string type;
    public string[] userIds;
}
