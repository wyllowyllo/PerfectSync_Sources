using System;
using Core.Utilities;
using InGame.Player.Movement;
using InGame.Player.Ragdoll;
using InGame.Team._02._Domain;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerFormController : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        [Header("Settings")]
        [SerializeField] private float _separationOffset = 1.0f;

        private IControllableBody _mergedControllable;
        private IControllableBody _avatarAControllable;
        private IControllableBody _avatarBControllable;
        private ETeamMode _currentMode;

        // 이벤트
        public event Action<ETeamMode> OnModeChanged;
        
        // 프로퍼티
        public Transform PrimaryBodyTransform => _currentMode switch
        {
            ETeamMode.Merged => _mergedControllable.BodyTransform,
            ETeamMode.Separated => _avatarAControllable.BodyTransform,
            _ => _mergedControllable.BodyTransform
        };

        public Transform SecondaryBodyTransform => _currentMode switch
        {
            ETeamMode.Separated => _avatarBControllable.BodyTransform,
            _ => _mergedControllable.BodyTransform
        };

        public Transform PrimaryCameraFollowPoint => _currentMode switch
        {
            ETeamMode.Merged => _mergedControllable.CameraFollowPoint,
            ETeamMode.Separated => _avatarAControllable.CameraFollowPoint,
            _ => _mergedControllable.CameraFollowPoint
        };

        public Transform SecondaryCameraFollowPoint => _currentMode switch
        {
            ETeamMode.Separated => _avatarBControllable.CameraFollowPoint,
            _ => _mergedControllable.CameraFollowPoint
        };

        private void Awake()
        {
            _mergedControllable = _mergedBody.GetComponentInChildren<IControllableBody>();
            _avatarAControllable = _avatarA.GetComponentInChildren<IControllableBody>();
            _avatarBControllable = _avatarB.GetComponentInChildren<IControllableBody>();
        }

        public void Initialize(ETeamMode startMode)
        {
            _currentMode = startMode;
            ApplyMode(startMode);
        }

        public ETeamMode CurrentMode => _currentMode;

        /// 래그돌 활성 중에는 폼 전환 불가 (요청 단계 가드용)
        public bool CanChangeForm() => !IsAnyRagdollActive();

        /// 네트워크 확정 폼 전환 — 래그돌 활성 시 강제 회복 후 전환 실행
        public void ExecuteFormToggle()
        {
            ETeamMode target = _currentMode == ETeamMode.Merged ? ETeamMode.Separated : ETeamMode.Merged;
            ExecuteFormChange(target);
        }

        public void ExecuteFormChange(ETeamMode targetMode)
        {
            if (_currentMode == targetMode) return;
            if (IsAnyRagdollActive()) ForceRecoverActiveBodies();
            ExecuteSwitch(targetMode);
        }

        public void ApplyInput(Vector3 worldDirA, Vector3 worldDirB, bool jumpA, bool jumpB)
        {
            switch (_currentMode)
            {
                case ETeamMode.Merged:
                    Vector3 combined = DualInputCombiner.Combine(worldDirA, worldDirB);
                    _mergedControllable.ApplyInput(combined, jumpA || jumpB);
                    break;

                case ETeamMode.Separated:
                    _avatarAControllable.ApplyInput(worldDirA, jumpA);
                    _avatarBControllable.ApplyInput(worldDirB, jumpB);
                    break;
            }
        }

        private void ExecuteSwitch(ETeamMode targetMode)
        {
            switch (targetMode)
            {
                case ETeamMode.Merged:
                    SwitchToMerged();
                    break;
                case ETeamMode.Separated:
                    SwitchToSeparated();
                    break;
            }

            _currentMode = targetMode;
            OnModeChanged?.Invoke(targetMode);
        }

        private void SwitchToMerged()
        {
            Transform bodyA = _avatarAControllable.BodyTransform;
            Transform bodyB = _avatarBControllable.BodyTransform;
            Transform mergedBodyT = _mergedControllable.BodyTransform;

            Vector3 avgPosition = (bodyA.position + bodyB.position) * 0.5f;
            Vector3 avgVelocity = (_avatarAControllable.Velocity + _avatarBControllable.Velocity) * 0.5f;

            Vector3 avgForward = (bodyA.forward + bodyB.forward) * 0.5f;
            if (avgForward.sqrMagnitude > 0.001f)
                mergedBodyT.rotation = Quaternion.LookRotation(avgForward);

            mergedBodyT.position = avgPosition;
            _mergedBody.SetActive(true);
            _mergedControllable.Velocity = avgVelocity;

            _avatarA.SetActive(false);
            _avatarB.SetActive(false);
        }

        private void SwitchToSeparated()
        {
            Transform mergedBodyT = _mergedControllable.BodyTransform;
            Vector3 basePosition = mergedBodyT.position;
            Vector3 velocity = _mergedControllable.Velocity;

            Vector3 right = mergedBodyT.right;
            Vector3 posA = basePosition - right * _separationOffset;
            Vector3 posB = basePosition + right * _separationOffset;

            _avatarAControllable.BodyTransform.position = posA;
            _avatarBControllable.BodyTransform.position = posB;
            _avatarA.SetActive(true);
            _avatarB.SetActive(true);
            _avatarAControllable.Velocity = velocity;
            _avatarBControllable.Velocity = velocity;

            _mergedBody.SetActive(false);
        }

        private void ApplyMode(ETeamMode mode)
        {
            switch (mode)
            {
                case ETeamMode.Merged:
                    _mergedBody.SetActive(true);
                    _avatarA.SetActive(false);
                    _avatarB.SetActive(false);
                    break;

                case ETeamMode.Separated:
                    _mergedBody.SetActive(false);
                    _avatarA.SetActive(true);
                    _avatarB.SetActive(true);
                    break;
            }

            OnModeChanged?.Invoke(mode);
        }

        private void ForceRecoverActiveBodies()
        {
            ForceRecoverBody(_mergedBody);
            ForceRecoverBody(_avatarA);
            ForceRecoverBody(_avatarB);
        }

        private void ForceRecoverBody(GameObject body)
        {
            if (body == null) return;
            var ragdoll = body.GetComponentInChildren<RagdollStateMachine>();
            if (ragdoll != null && ragdoll.IsRagdollActive)
                ragdoll.ForceRecover();
        }

        private bool IsAnyRagdollActive()
        {
            return _currentMode switch
            {
                ETeamMode.Merged => _mergedControllable.IsRagdollActive,
                ETeamMode.Separated => _avatarAControllable.IsRagdollActive
                    || _avatarBControllable.IsRagdollActive,
                _ => false
            };
        }
    }
}
