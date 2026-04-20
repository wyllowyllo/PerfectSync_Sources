using Core.VFX;
using InGame.VFX;
using UnityEngine;

namespace InGame.Team
{
    [RequireComponent(typeof(SlotMachinePresenter))]
    public class TeamModeVfxTrigger : MonoBehaviour
    {
        [Header("Spatial VFX Profiles")]
        [SerializeField] private SpatialVfxProfile _slotJackpotProfile;
        [SerializeField] private SpatialVfxProfile _slotMissProfile;

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
            SpatialVfxProfile profile = isMatch ? _slotJackpotProfile : _slotMissProfile;
            InGameVfxManager.Instance?.EmitAt(profile, slotPosition, Quaternion.identity, this);
        }
    }
}
