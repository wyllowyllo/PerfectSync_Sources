using TMPro;
using UnityEngine;

public class LobbyCharacterNicknameView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private string _emptyDisplay = "";
    [SerializeField] private GameObject _readyCheckImage;

    private void Start()
    {
        SetReadyCheck(false);
    }

    public void SetReadyCheck(bool visible)
    {
        if (_readyCheckImage != null)
            _readyCheckImage.SetActive(visible);
    }

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
