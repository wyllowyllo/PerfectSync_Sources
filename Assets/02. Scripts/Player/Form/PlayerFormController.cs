using System;
using Player.Domain;
using Player.Common;
using UnityEngine;

namespace Player.Form
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
        private bool _pendingSwitch;
        private ETeamMode _pendingSwitchTarget;

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

        public void Initialize(ETeamMode startMode)
        {
            _mergedControllable = _mergedBody.GetComponent<IControllableBody>();
            _avatarAControllable = _avatarA.GetComponent<IControllableBody>();
            _avatarBControllable = _avatarB.GetComponent<IControllableBody>();

            _currentMode = startMode;
            ApplyMode(startMode);
        }

        public void SwitchMode(ETeamMode targetMode)
        {
            if (_currentMode == targetMode)
                return;

            if (IsAnyRagdollActive())
            {
                _pendingSwitch = true;
                _pendingSwitchTarget = targetMode;
                return;
            }

            ExecuteSwitch(targetMode);
        }

        public void ToggleMode()
        {
            ETeamMode target = _currentMode == ETeamMode.Merged ? ETeamMode.Separated : ETeamMode.Merged;
            SwitchMode(target);
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

        public void Tick()
        {
            if (_pendingSwitch && !IsAnyRagdollActive())
            {
                _pendingSwitch = false;
                ExecuteSwitch(_pendingSwitchTarget);
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
            // 위치/속도 평균 계산.
            Vector3 avgPosition = (_avatarA.transform.position + _avatarB.transform.position) * 0.5f;
            Vector3 avgVelocity = (_avatarAControllable.Velocity + _avatarBControllable.Velocity) * 0.5f;

            _mergedBody.transform.position = avgPosition;
            _mergedBody.SetActive(true);
            _mergedControllable.Velocity = avgVelocity;

            _avatarA.SetActive(false);
            _avatarB.SetActive(false);
        }

        private void SwitchToSeparated()
        {
            Vector3 basePosition = _mergedBody.transform.position;
            Vector3 velocity = _mergedControllable.Velocity;

            Vector3 right = _mergedBody.transform.right;
            Vector3 posA = basePosition - right * _separationOffset;
            Vector3 posB = basePosition + right * _separationOffset;

            _avatarA.transform.position = posA;
            _avatarB.transform.position = posB;
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
