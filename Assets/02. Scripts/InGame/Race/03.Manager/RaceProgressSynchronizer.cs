using InGame.Player;
using InGame.Team;
using UnityEngine;

/// <summary>
/// TeamCharacter 루트에 부착.
/// 합체/분리 모드 전환 시 하위 body들의 RaceProgressTracker 체크포인트를 동기화한다.
/// </summary>
[RequireComponent(typeof(PlayerFormController))]
public class RaceProgressSynchronizer : MonoBehaviour
{
    private PlayerFormController _formController;
    private RaceProgressTracker[] _trackers;

    private void Awake()
    {
        _formController = GetComponent<PlayerFormController>();
        _trackers = GetComponentsInChildren<RaceProgressTracker>(true);
    }

    private void OnEnable()
    {
        _formController.OnModeChanged += HandleModeChanged;
    }

    private void OnDisable()
    {
        _formController.OnModeChanged -= HandleModeChanged;
    }

    private void HandleModeChanged(ETeamMode newMode)
    {
        int maxCheckpoint = 0;
        foreach (var tracker in _trackers)
        {
            if (tracker.CheckpointsPassed > maxCheckpoint)
                maxCheckpoint = tracker.CheckpointsPassed;
        }

        foreach (var tracker in _trackers)
            tracker.PassCheckpoint(maxCheckpoint);
    }
}
