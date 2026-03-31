using System;
using UnityEngine;

public static class PartyMembersJson
{
    [Serializable]
    private class Dto
    {
        public string[] ids;
    }

    public static string Build(string a, string b)
    {
        var dto = new Dto { ids = new[] { a, b } };
        return JsonUtility.ToJson(dto);
    }
}
