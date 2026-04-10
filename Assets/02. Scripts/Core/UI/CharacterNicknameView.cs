using TMPro;
using UnityEngine;

public class CharacterNicknameView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private string _emptyDisplay = "";

    public void SetNickname(string nickname)
    {
        if (_nicknameText == null)
            return;

        _nicknameText.text = string.IsNullOrEmpty(nickname) ? _emptyDisplay : nickname;
    }

    public void ClearNickname()
    {
        SetNickname(string.Empty);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
