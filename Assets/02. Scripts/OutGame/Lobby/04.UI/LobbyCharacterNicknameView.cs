using TMPro;
using UnityEngine;

/// <summary>
/// 로비 씬의 단일 캐릭터 슬롯(로컬 / 파티원 등)에 붙여 TMP 닉네임을 갱신합니다.
/// 파티원처럼 비활성으로 두는 경우 visibility 루트 필드에 해당 오브젝트를 지정하세요.
/// </summary>
public class LobbyCharacterNicknameView : MonoBehaviour
{
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private string _emptyDisplay = "";
    [Tooltip("비어 있으면 이 오브젝트의 활성 여부를 토글합니다.")]
    [SerializeField] private GameObject _visibilityRoot;

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
        GameObject target = _visibilityRoot != null ? _visibilityRoot : gameObject;
        if (target != null)
            target.SetActive(visible);
    }
}
