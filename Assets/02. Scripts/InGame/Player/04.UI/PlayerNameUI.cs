using System.Collections.Generic;
using System.Globalization;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PlayerNameUI : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Color _allyColor = Color.green;
    [SerializeField] private Color _enemyColor = Color.red;

    private const int NameCharLimit = 2;

    private PhotonView _photonView;
    private int _ownerTeam;

    private void Start()
    {
        _photonView = GetComponent<PhotonView>();
        if (_photonView == null || _photonView.Owner == null) return;

        _ownerTeam = PhotonTeamManager.GetTeamRaw(_photonView.Owner);
        UpdateColor();
        UpdateTeamName();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateTeamName();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateTeamName();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(PhotonTeamManager.TeamKey)) return;

        if (_photonView != null && _photonView.Owner != null)
        {
            _ownerTeam = PhotonTeamManager.GetTeamRaw(_photonView.Owner);
            UpdateColor();
        }

        UpdateTeamName();
    }

    private void UpdateColor()
    {
        if (_ownerTeam == PhotonTeamManager.TeamNone) return;

        int myTeam = PhotonTeamManager.GetLocalTeamRaw();
        _nameText.color = (_ownerTeam == myTeam) ? _allyColor : _enemyColor;
    }

    private void UpdateTeamName()
    {
        if (_photonView == null || _photonView.Owner == null) return;
        if (_ownerTeam == PhotonTeamManager.TeamNone) return;
        if (PhotonTeamManager.Instance == null) return;

        List<Player> members = PhotonTeamManager.Instance.GetTeamMembers(_ownerTeam);
        if (members == null || members.Count == 0)
        {
            _nameText.text = "";
            return;
        }

        members.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));

        string combined;
        if (members.Count == 1)
        {
            combined = Abbreviate(members[0].NickName);
        }
        else
        {
            combined = Abbreviate(members[0].NickName) + "'" + Abbreviate(members[1].NickName);
        }

        _nameText.text = $"< {combined} >";
    }

    private static string Abbreviate(string nickname)
    {
        if (string.IsNullOrEmpty(nickname)) return "??";

        var info = new StringInfo(nickname);
        int len = Mathf.Min(NameCharLimit, info.LengthInTextElements);
        return info.SubstringByTextElements(0, len);
    }
}
