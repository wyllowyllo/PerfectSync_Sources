using System;
using InGame.Player.Animation;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class RagdollStateMachine : MonoBehaviour, IRagdoll
    {
        [Header("References")]
        [SerializeField] private Rigidbody _rootBody;
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private PoseTransfer _poseTransfer;
        [SerializeField] private PlayerAnimation _animation;
        [SerializeField] private ActiveRagdollForce _activeRagdollForce;

        [Header("Recovery")]
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Thresholds")]
        [SerializeField] private float _stumbleThreshold = 3f;
        [SerializeField] private float _ragdollThreshold = 7f;

        [Header("Stumble")]
        [SerializeField] private float _minStumbleDuration = 0.3f;
        [SerializeField] private float _maxStumbleDuration = 0.8f;
        [SerializeField] private float _stumbleDurationPerImpulse = 0.08f;
        [SerializeField] private float _stumblePushMultiplier = 0.3f;

        [Header("Ragdoll")]
        [SerializeField] private float _minRagdollDuration = 0.3f;
        [SerializeField] private float _maxRagdollDuration = 3.0f;
        [SerializeField] private float _settleVelocity = 0.5f;
        [SerializeField] private float _impactRadius = 2.0f;
        [SerializeField] private float _impactForceScale = 0.3f;

        [Header("Recovery Blend")]
        [SerializeField] private float _recoveryDuration = 0.25f;

        [Header("Re-Impact")]
        [SerializeField, Range(0f, 1f)] private float _reImpactMultiplier = 0.6f;

        [Header("Instability")]
        [SerializeField] private float _instabilityDecayRate = 4f;

        [Header("Root Body Tracking")]
        [SerializeField] private float _rootBodyTrackingSpeed = 5f;

        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactTransfer _impactTransfer;
        private float _instability;
        private float _stateTimer;
        private float _stumbleDuration;
        private bool _shouldTrackPelvis;
        private bool _isRecoveryAuthority = true;

        private const float RayOriginUpOffset = 0.5f;
        private const float MinDirectionSqrMagnitude = 0.001f;
        private const float RemoteRecoveryTimeout = 5.0f;

        public ERagdollState CurrentState => _currentState;
        public bool IsRagdollActive => _currentState != ERagdollState.Animated;

        public bool IsPhysicsRagdoll => _currentState == ERagdollState.Ragdoll ||
                                        _currentState == ERagdollState.Recovery ||
                                        _currentState == ERagdollState.Dead;

        public event Action<Vector3, Quaternion, bool> OnRecoveryDataReady;

        public void SetRecoveryAuthority(bool isAuthority)
        {
            _isRecoveryAuthority = isAuthority;
        }

        private void Start()
        {
            _impactTransfer = new RagdollImpactTransfer(_ragdollRig.Rigidbodies, _impactRadius, _impactForceScale);
            SetActiveRagdollForceActive(false);
        }

        private void Update()
        {
            DecayInstability();

            switch (_currentState)
            {
                case ERagdollState.Animated:
                    break;
                case ERagdollState.Stumble:
                    UpdateStumble();
                    break;
                case ERagdollState.Ragdoll:
                    UpdateRagdoll();
                    break;
                case ERagdollState.Recovery:
                    UpdateRecovery();
                    break;
                case ERagdollState.Dead:
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (_shouldTrackPelvis)
            {
                Vector3 pelvisPos = _ragdollRig.PelvisTransform.position;
                float t = 1f - Mathf.Exp(-_rootBodyTrackingSpeed * Time.fixedDeltaTime);
                _rootBody.MovePosition(Vector3.Lerp(_rootBody.position, pelvisPos, t));
            }
        }

        private void LateUpdate()
        {
            if (_currentState == ERagdollState.Ragdoll || _currentState == ERagdollState.Dead)
                _poseTransfer.CopyPose();
        }

        #region Public API

        public void OnHitImpact(Vector3 impulse, Vector3 hitPoint, Vector3 torqueVector)
        {
            var impact = new ImpactData(impulse, hitPoint);
            float effectiveMagnitude = impact.Magnitude + _instability;

            switch (_currentState)
            {
                case ERagdollState.Recovery:
                    if (effectiveMagnitude >= _ragdollThreshold * _reImpactMultiplier)
                        EnterRagdoll(impact, torqueVector);
                    break;

                case ERagdollState.Ragdoll:
                    if (_isRecoveryAuthority)
                    {
                        _impactTransfer.ApplyAdditionalImpact(impact, torqueVector);
                        _stateTimer = 0f;
                    }
                    break;

                case ERagdollState.Animated:
                case ERagdollState.Stumble:
                    if (effectiveMagnitude >= _ragdollThreshold)
                    {
                        EnterRagdoll(impact, torqueVector);
                    }
                    else if (impact.Magnitude >= _stumbleThreshold)
                    {
                        EnterStumble(impact);
                    }
                    break;

                case ERagdollState.Dead:
                    break;
            }
        }

        public void EnterDead()
        {
            if (_currentState == ERagdollState.Dead) return;

            _currentState = ERagdollState.Dead;

            _poseTransfer.SetDirection(EPoseDirection.AnimToRagdoll);
            _poseTransfer.CopyPose();

            if (_isRecoveryAuthority)
            {
                Vector3 inheritedVelocity = _rootBody.linearVelocity;
                _ragdollRig.Activate(inheritedVelocity);
                _impactTransfer.TransferImpact(
                    new ImpactData(Vector3.zero, _rootBody.position),
                    inheritedVelocity,
                    Vector3.zero);
            }
            else
            {
                _ragdollRig.ActivateKinematic();
            }

            _poseTransfer.SetDirection(EPoseDirection.RagdollToAnim);
            _shouldTrackPelvis = false;
            SetActiveRagdollForceActive(false);
        }

        public void ForceRecover()
        {
            if (_currentState == ERagdollState.Dead) return;

            ERagdollState prevState = _currentState;
            _currentState = ERagdollState.Animated;
            _shouldTrackPelvis = false;
            _instability = 0f;
            _stateTimer = 0f;

            if (prevState == ERagdollState.Ragdoll || prevState == ERagdollState.Recovery)
            {
                _poseTransfer.Stop();
                _ragdollRig.Deactivate();
                _poseTransfer.RestoreVisualBones();
                SetActiveRagdollForceActive(false);
            }

            _animation.ClearGetUpState();
            _animation.ClearStumbleState();
        }

        public void ApplyRemoteRecovery(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (_currentState != ERagdollState.Ragdoll && _currentState != ERagdollState.Recovery)
                return;

            if (_currentState == ERagdollState.Recovery)
            {
                _rootBody.position = rootPos;
                _rootBody.rotation = rootRot;
                return;
            }

            _shouldTrackPelvis = false;
            _poseTransfer.Stop();

            _rootBody.position = rootPos;
            _rootBody.rotation = rootRot;

            _ragdollRig.Deactivate();
            _poseTransfer.RestoreVisualBones();

            _animation.GetUp(isFaceUp);
            _currentState = ERagdollState.Recovery;
            _stateTimer = 0f;
            SetActiveRagdollForceActive(false);
        }

        // Animator의 GetUp 애니메이션이 끝나면 호출.
        public void OnGetUpComplete()
        {
            if (_currentState != ERagdollState.Recovery) return;
            TransitionToAnimated();
        }

        #endregion

        #region Stumble

        private void EnterStumble(ImpactData impact)
        {
            _currentState = ERagdollState.Stumble;
            _instability += impact.Magnitude;

            Vector3 pushDir = impact.Impulse.normalized;
            _rootBody.AddForce(pushDir * impact.Magnitude * _stumblePushMultiplier, ForceMode.Impulse);

            _animation.Stumble();

            _stumbleDuration = Mathf.Clamp(
                impact.Magnitude * _stumbleDurationPerImpulse,
                _minStumbleDuration,
                _maxStumbleDuration);
            _stateTimer = 0f;
        }

        private void UpdateStumble()
        {
            _stateTimer += Time.deltaTime;

            if (_stateTimer >= _stumbleDuration)
            {
                _currentState = ERagdollState.Animated;
                _animation.ClearStumbleState();
            }
        }

        #endregion

        #region Ragdoll

        private void EnterRagdoll(ImpactData impact, Vector3 torqueVector)
        {
            _currentState = ERagdollState.Ragdoll;
            _instability = 0f;
            _stateTimer = 0f;

            _poseTransfer.SetDirection(EPoseDirection.AnimToRagdoll);
            _poseTransfer.CopyPose();

            if (_isRecoveryAuthority)
            {
                Vector3 inheritedVelocity = _rootBody.linearVelocity;
                _ragdollRig.Activate(inheritedVelocity);
                _impactTransfer.TransferImpact(impact, inheritedVelocity, torqueVector);
                SetActiveRagdollForceActive(true);
            }
            else
            {
                _ragdollRig.ActivateKinematic();
                SetActiveRagdollForceActive(false);
            }

            _poseTransfer.SetDirection(EPoseDirection.RagdollToAnim);
            _shouldTrackPelvis = true;
        }

        private void UpdateRagdoll()
        {
            _stateTimer += Time.deltaTime;

            if (_stateTimer < _minRagdollDuration)
                return;

            if (_isRecoveryAuthority)
            {
                if (_ragdollRig.IsSettled(_settleVelocity) || _stateTimer >= _maxRagdollDuration)
                    BeginRecovery();
            }
            else
            {
                if (_stateTimer >= RemoteRecoveryTimeout)
                    BeginRecovery();
            }
        }

        private void BeginRecovery()
        {
            _shouldTrackPelvis = false;
            _poseTransfer.Stop();

            Transform pelvis = _ragdollRig.PelvisTransform;
            bool isFaceUp = (pelvis.rotation * Vector3.forward).y > 0f;
            AlignRootBodyToPelvis(pelvis);

            OnRecoveryDataReady?.Invoke(_rootBody.position, _rootBody.rotation, isFaceUp);

            _ragdollRig.Deactivate();
            _poseTransfer.RestoreVisualBones();

            _animation.GetUp(isFaceUp);
            _currentState = ERagdollState.Recovery;
            _stateTimer = 0f;
            SetActiveRagdollForceActive(false);
        }

        #endregion

        #region Recovery

        private void UpdateRecovery()
        {
            _stateTimer += Time.deltaTime;

            if (_stateTimer >= _recoveryDuration)
                TransitionToAnimated();
        }

        private void TransitionToAnimated()
        {
            _currentState = ERagdollState.Animated;
            _animation.ClearGetUpState();
        }

        #endregion

        #region Helpers

        private void DecayInstability()
        {
            if (_instability > 0f)
                _instability = Mathf.Max(0f, _instability - _instabilityDecayRate * Time.deltaTime);
        }

        private void AlignRootBodyToPelvis(Transform pelvis)
        {
            Vector3 pelvisPos = pelvis.position;
            float groundY = GetGroundY(pelvisPos);
            _rootBody.position = new Vector3(pelvisPos.x, groundY, pelvisPos.z);

            Vector3 hipsForward = pelvis.rotation * Vector3.forward;
            hipsForward.y = 0f;

            if (hipsForward.sqrMagnitude > MinDirectionSqrMagnitude)
                _rootBody.rotation = Quaternion.LookRotation(hipsForward);
        }

        private float GetGroundY(Vector3 origin)
        {
            Vector3 rayOrigin = origin + Vector3.up * RayOriginUpOffset;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _groundCheckDistance, _groundLayer))
                return hit.point.y;

            return origin.y;
        }

        private void SetActiveRagdollForceActive(bool active)
        {
            if (_activeRagdollForce != null)
                _activeRagdollForce.SetActive(active);
        }

        #endregion
    }
}
