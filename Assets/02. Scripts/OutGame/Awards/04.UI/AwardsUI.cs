using TMPro;
using Photon.Pun;
using UnityEngine;

public class AwardsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _rankText;

    private void Start()
    {
        AwardsSceneManager.RefreshUiRequested += Refresh;
    }

    private void OnDestroy()
    {
        AwardsSceneManager.RefreshUiRequested -= Refresh;
    }

    private void Refresh()
    {
        if (_rankText == null)
            return;

        if (PhotonNetwork.LocalPlayer == null || !PlayerFinalRankReader.TryGetFinalRank(PhotonNetwork.LocalPlayer, out int rank))
        {
            _rankText.text = "-";
            return;
        }

        _rankText.text = $"{rank}등";
    }
}
