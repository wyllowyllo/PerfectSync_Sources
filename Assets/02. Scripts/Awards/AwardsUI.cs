using TMPro;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Awards 씬 순위 등 UI 표시. <see cref="AwardsSceneManager.OnAwardsUiRefreshRequested"/>로만 갱신.
/// </summary>
public class AwardsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _rankText;

    private void OnEnable()
    {
        AwardsSceneManager.OnAwardsUiRefreshRequested += HandleAwardsUiRefreshRequested;
    }

    private void OnDisable()
    {
        AwardsSceneManager.OnAwardsUiRefreshRequested -= HandleAwardsUiRefreshRequested;
    }

    private void HandleAwardsUiRefreshRequested()
    {
        RefreshRankFromLocalPlayer();
    }

    private void RefreshRankFromLocalPlayer()
    {
        if (_rankText == null) return;

        if (PhotonNetwork.LocalPlayer == null ||
            !PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(InGameRaceKeys.FinalRankKey, out object rankObj))
        {
            _rankText.text = "-";
            return;
        }

        int rank = rankObj switch
        {
            int i => i,
            byte b => b,
            short s => s,
            _ => -1
        };

        _rankText.text = rank < 1 ? "-" : $"{rank}등";
    }
}
