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
        [SerializeField] private WobbleEffect _wobbleEffect;
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
        [SerializeField] private float _stumbleWobbleMultiplier = 2f;

        [Header("Ragdoll")]
        [SerializeField] private float _minRagdollDuration = 0.3f;
        [SerializeField] private float _maxRagdollDuration = 3.0f;
        [SerializeField] private float _settleVelocity = 0.5f;
        [SerializeField] private float _impactRadius = 2.0f;

        [Header("Recovery Blend")]
        [SerializeField] private float _recoveryDuration = 0.25f;

        [Header("Re-Impact")]
        [SerializeField, Range(0f, 1f)] private float _reImpactMultiplier = 0.6f;

        [Header("Instability")]
        [SerializeField] private float _instabilityDecayRate = 4f;

        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactTransfer _impactTransfer;
        private float _instability;
        private float _stateTimer;
        private float _stumbleDuration;

        private const float RayOriginUpOffset = 0.5f;
        private const float MinDirectionSqrMagnitude = 0.001f;

        public ERagdollState CurrentState => _currentState;
        public bool IsRagdollActive => _currentState != ERagdollState.Animated;

        public bool IsPhysicsRagdoll => _currentState == ERagdollState.Ragdoll ||
                                        _currentState == ERagdollState.Recovery ||
                                        _currentState == ERagdollState.Dead;

        public event Action<Vector3, Quaternion, bool> OnRecoveryDataReady;

        private void Start()
        {
            _impactTransfer = new RagdollImpactTransfer(_ragdollRig.Rigidbodies, _impactRadius);
            SetWobbleActive(true);
            SetActiveRagdollForceActive(false);
        }

        private void Update()
        {
            switch (_currentState)
            {
                case ERagdollState.Animated:
                    UpdateAnimated();
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
                    _impactTransfer.ApplyAdditionalImpact(impact, torqueVector);
                    _stateTimer = 0f;
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
                    else if (_wobbleEffect != null && _currentState == ERagdollState.Animated)
                    {
                        _wobbleEffect.AddImpulse(impulse);
                    }
                    break;

                case ERagdollState.Dead:
                    break;
            }
        }

        public void EnterDead()
        {
            _currentState = ERagdollState.Dead;

            _poseTransfer.SetDirection(EPoseDirection.AnimToRagdoll);
            _poseTransfer.CopyPose();

            Vector3 inheritedVelocity = _rootBody.linearVelocity;
            _ragdollRig.Activate(inheritedVelocity);
            _impactTransfer.TransferImpact(
                new ImpactData(Vector3.zero, _rootBody.position),
                inheritedVelocity,
                Vector3.zero);

            _poseTransfer.SetDirection(EPoseDirection.RagdollToAnim);
            SetWobbleActive(false);
            SetActiveRagdollForceActive(false);
        }

        public void ForceRecover()
        {
            if (_currentState == ERagdollState.Dead) return;

            _currentState = ERagdollState.Animated;
            _instability = 0f;
            _stateTimer = 0f;

            _poseTransfer.Stop();
            _ragdollRig.Deactivate();
            _poseTransfer.RestoreVisualBones();

            _animation.ClearGetUpState();
            _animation.ClearStumbleState();
            SetWobbleActive(true);
            SetActiveRagdollForceActive(false);
        }

        public void ApplyRemoteRecovery(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            if (_currentState != ERagdollState.Ragdoll) return;

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

        #region Animated

        private void UpdateAnimated()
        {
            if (_instability > 0f)
                _instability = Mathf.Max(0f, _instability - _instabilityDecayRate * Time.deltaTime);
        }

        #endregion

        #region Stumble

        private void EnterStumble(ImpactData impact)
        {
            _currentState = ERagdollState.Stumble;
            _instability += impact.Magnitude;

            Vector3 pushDir = impact.Impulse.normalized;
            _rootBody.AddForce(pushDir * impact.Magnitude * _stumblePushMultiplier, ForceMode.Impulse);

            if (_wobbleEffect != null)
                _wobbleEffect.AddImpulse(impact.Impulse * _stumbleWobbleMultiplier);

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

            if (_instability > 0f)
                _instability = Mathf.Max(0f, _instability - _instabilityDecayRate * Time.deltaTime);

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

            Vector3 inheritedVelocity = _rootBody.linearVelocity;
            _ragdollRig.Activate(inheritedVelocity);
            _impactTransfer.TransferImpact(impact, inheritedVelocity, torqueVector);

            _poseTransfer.SetDirection(EPoseDirection.RagdollToAnim);

            SetWobbleActive(false);
            SetActiveRagdollForceActive(true);
        }

        private void UpdateRagdoll()
        {
            _stateTimer += Time.deltaTime;

            if (_stateTimer < _minRagdollDuration)
                return;

            if (_ragdollRig.IsSettled(_settleVelocity) || _stateTimer >= _maxRagdollDuration)
                BeginRecovery();
        }

        private void BeginRecovery()
        {
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
            SetWobbleActive(true);
        }

        #endregion

        #region Helpers

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

        private void SetWobbleActive(bool active)
        {
            if (_wobbleEffect != null)
                _wobbleEffect.SetActive(active);
        }

        private void SetActiveRagdollForceActive(bool active)
        {
            if (_activeRagdollForce != null)
                _activeRagdollForce.SetActive(active);
        }

        #endregion
    }
}
