using Photon.Pun;
using TMPro;
using UnityEngine;

public class PlayerNameUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Color _allyColor = Color.green;
    [SerializeField] private Color _enemyColor = Color.red;

    private void Start()
    {
        var photonView = GetComponentInParent<PhotonView>();
        if (photonView == null || photonView.Owner == null) return;

        _nameText.text = photonView.Owner.NickName;

        int myTeam = InGameManager.GetLocalPlayerTeam();
        int ownerTeam = InGameManager.GetPlayerTeam(photonView.Owner);

        _nameText.color = (ownerTeam == myTeam) ? _allyColor : _enemyColor;
    }
}
