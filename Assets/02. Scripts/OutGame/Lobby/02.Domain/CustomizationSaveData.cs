using System;
using System.Collections.Generic;
#if !UNITY_WEBGL || UNITY_EDITOR
using Firebase.Firestore;
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
[FirestoreData]
#endif
public class CustomizationSaveData
{
#if !UNITY_WEBGL || UNITY_EDITOR
    [FirestoreProperty]
#endif
    public Dictionary<string, int> Parts { get; set; } = new Dictionary<string, int>();

#if !UNITY_WEBGL || UNITY_EDITOR
    [FirestoreProperty]
#endif
    public string Nickname { get; set; } = string.Empty;

    public void SetPartIndex(CharacterCustomizationPart part, int index)
    {
        Parts[part.ToString()] = index;
    }

    public int GetPartIndex(CharacterCustomizationPart part)
    {
        return Parts.TryGetValue(part.ToString(), out int index) ? index : GetDefaultIndex(part);
    }

    public static CustomizationSaveData CreateDefault()
    {
        var data = new CustomizationSaveData();
        foreach (CharacterCustomizationPart part in Enum.GetValues(typeof(CharacterCustomizationPart)))
            data.Parts[part.ToString()] = GetDefaultIndex(part);

        return data;
    }

    private static int GetDefaultIndex(CharacterCustomizationPart part)
    {
        return part == CharacterCustomizationPart.BodyColor ? 1 : 0;
    }
}
