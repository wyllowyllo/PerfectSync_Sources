using Core.VFX;
using InGame.VFX;
using UnityEngine;

namespace InGame.Team
{
    [RequireComponent(typeof(SlotMachinePresenter))]
    public class TeamModeVfxTrigger : MonoBehaviour
    {
        private SlotMachinePresenter _presenter;

        private void Awake()
        {
            _presenter = GetComponent<SlotMachinePresenter>();
        }

        private void OnEnable()
        {
            _presenter.OnSlotResultRevealed += HandleSlotResultRevealed;
        }

        private void OnDisable()
        {
            _presenter.OnSlotResultRevealed -= HandleSlotResultRevealed;
        }

        private void HandleSlotResultRevealed(Vector3 slotPosition, bool isMatch)
        {
            EVfxId id = isMatch ? EVfxId.SlotJackpot : EVfxId.SlotMiss;
            InGameVfxManager.Instance?.Emit(id, slotPosition, Quaternion.identity, this);
        }
    }
}
