public static class CharacterCustomizationPartNames
{
    public static string GetDisplayName(CharacterCustomizationPart part)
    {
        switch (part)
        {
            case CharacterCustomizationPart.Hair:
                return "헤어";
            case CharacterCustomizationPart.Hat:
                return "모자";
            case CharacterCustomizationPart.Horn:
                return "뿔";
            case CharacterCustomizationPart.Ears:
                return "귀";
            case CharacterCustomizationPart.Eyes:
                return "눈";
            case CharacterCustomizationPart.Nose:
                return "코";
            case CharacterCustomizationPart.Mouth:
                return "입";
            case CharacterCustomizationPart.BodyColor:
                return "바디 색상";
            case CharacterCustomizationPart.Body:
                return "바디 파츠";
            case CharacterCustomizationPart.Gloves:
                return "장갑";
            case CharacterCustomizationPart.Tail:
                return "꼬리";
            default:
                return part.ToString();
        }
    }
}
