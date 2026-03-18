using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class InGameTestUI : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _roomInfoText;
    [SerializeField] private TMP_Text _team1Text;
    [SerializeField] private TMP_Text _team2Text;
    [SerializeField] private TMP_Text _team3Text;
    [SerializeField] private TMP_Text _team4Text;
    [SerializeField] private Button _leaveButton;

    [Header("Settings")]
    [SerializeField] private string _lobbySceneName = "Lobby";

    private TMP_Text[] _teamTexts;

    private void Start()
    {
        _teamTexts = new[] { _team1Text, _team2Text, _team3Text, _team4Text };

        _leaveButton.onClick.AddListener(OnClickLeave);

        _titleText.text = "InGame 씬 도착!";

        StartCoroutine(WaitAndRefresh());
    }

    private IEnumerator WaitAndRefresh()
    {
        yield return null;
        RefreshRoomInfo();
        RefreshTeamDisplay();
    }

    private void OnClickLeave()
    {
        PhotonRoomManager.Instance.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        PhotonNetwork.LoadLevel(_lobbySceneName);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshRoomInfo();
        RefreshTeamDisplay();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey(PhotonTeamManager.TEAM_KEY))
            RefreshTeamDisplay();
    }

    private void RefreshRoomInfo()
    {
        if (!PhotonNetwork.InRoom)
        {
            _roomInfoText.text = "방 정보 없음";
            return;
        }

        var room = PhotonNetwork.CurrentRoom;
        _roomInfoText.text = $"방 이름: {room.Name}  |  플레이어: {room.PlayerCount}/{room.MaxPlayers}";
    }

    private void RefreshTeamDisplay()
    {
        if (PhotonTeamManager.Instance == null) return;

        int localTeam = PhotonTeamManager.Instance.GetPlayerTeam(PhotonNetwork.LocalPlayer);

        for (int t = 0; t < PhotonTeamManager.MAX_TEAMS; t++)
        {
            int teamNumber = t + 1;
            var members = PhotonTeamManager.Instance.GetTeamMembers(teamNumber);
            var sb = new StringBuilder();

            bool isMyTeam = (teamNumber == localTeam);
            sb.AppendLine(isMyTeam ? $"<color=yellow>[ 팀 {teamNumber} ] ★ 내 팀</color>" : $"[ 팀 {teamNumber} ]");

            if (members.Count == 0)
            {
                sb.AppendLine("  (비어 있음)");
            }
            else
            {
                foreach (var member in members)
                {
                    bool isLocal = member.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
                    string name = isLocal ? $"<color=yellow>{member.NickName} (나)</color>" : member.NickName;
                    sb.AppendLine($"  - {name}");
                }
            }

            _teamTexts[t].text = sb.ToString();
        }
    }
}
