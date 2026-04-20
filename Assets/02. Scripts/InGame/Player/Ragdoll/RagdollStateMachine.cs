using System;
using Core;
using InGame.Player.Animation;
using UnityEngine;
namespace InGame.Player.Ragdoll
{
    public enum ERagdollState { Animated, Ragdolled, BlendToAnim, Dead }

    // 단일 계층 래그돌 상태머신.
    // Animator가 제어하는 본 = Rigidbody가 달린 본. PoseTransfer 불필요.
    // 래그돌 진입 시 스켈레톤을 rootBody 자식에서 분리하여 물리 독립 보장.
    [DefaultExecutionOrder(ExecutionOrderConstants.RagdollStateMachine)]
    [RequireComponent(typeof(PlayerAnimation))]
    public class RagdollStateMachine : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody _rootBody;
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private Transform _skeletonRoot;

        private PlayerAnimation _animation;

        [Header("Thresholds")]
        [SerializeField] private HitThresholdProfile _thresholdProfile;

        [Header("Ragdoll")]
        [SerializeField] private float _minRagdollDuration = 0.2f;
        [SerializeField] private float _maxRagdollDuration = 2.0f;
        [SerializeField] private float _settleVelocity = 1.5f;
        [SerializeField] private float _hitRadius = 2.0f;
        [SerializeField] private float _hitForceScale = 0.3f;

        [Header("Blend")]
        [SerializeField] private float _ragdollToAnimBlendTime = 0.25f;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Re-Impact")]
        [SerializeField, Range(0f, 1f)] private float _reImpactMultiplier = 0.6f;

        [Header("Instability")]
        [SerializeField] private float _instabilityDecayRate = 4f;

        [Header("Root Body Tracking")]
        [SerializeField] private float _rootBodyTrackingSpeed = 25f;

        [Header("Root Transition")]
        [SerializeField] private float _rootTransitionDuration = 0.2f;

        // State.
        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollHitApplier _hitApplier;
        private RagdollBlender _blender;
        private float _instability;
        private float _stateTimer;
        private bool _isAuthority = true;

        // Skeleton detach.
        private Transform _skeletonOriginalParent;
        private bool _isDetached;

        // Root control.
        private bool _shouldTrackPelvis;
        private RagdollRootTransition _rootTransition;


        // 프로퍼티
        public ERagdollState CurrentState => _currentState;
        public bool IsRagdollActive => _currentState != ERagdollState.Animated;
        public bool IsPhysicsRagdoll => _currentState == ERagdollState.Ragdolled || _currentState == ERagdollState.Dead;

        // 래그돌 시스템이 rootBody 위치를 관리 중인지 여부.
        // Ragdolled/Dead: pelvis 추적, BlendToAnim: root transition.
        // 이 동안 외부 위치 보정(BodyMovementSynchronizer, PhotonTransformView)을 억제해야 함.
        public bool IsRootManagedByRagdoll => _currentState != ERagdollState.Animated;

        // 이벤트
        public event Action<ERagdollState> OnStateChanged;
        public event Action OnStumblePlayed;

        // 상수
        private const float RayOriginUpOffset = 0.5f;

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;
        }

        private void Start()
        {
            _animation = GetComponent<PlayerAnimation>();
            _skeletonOriginalParent = _skeletonRoot.parent;
            _hitApplier = new RagdollHitApplier(_ragdollRig.Rigidbodies, _hitRadius, _hitForceScale, _ragdollRig.SpeedLimits);
            _blender = new RagdollBlender(_ragdollRig, _animator, _ragdollToAnimBlendTime, _groundCheckDistance, _groundLayer);
            _rootTransition = new RagdollRootTransition(_rootTransitionDuration);
        }

        private void Update()
        {
            if (!_isAuthority) return;

            DecayInstability();

            switch (_currentState)
            {
                case ERagdollState.Ragdolled:
                    UpdateRagdoll();
                    break;
                case ERagdollState.BlendToAnim:
                    UpdateBlendTimer();
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (_shouldTrackPelvis)
                TrackPelvis();

            _rootTransition.Tick(_rootBody);
        }

        private void LateUpdate()
        {
            // GetUp 애니메이션 반복 방지.
            _animation.ClearGetUpState();

            // BlendToAnim 블렌드는 Authority와 Remote 모두 적용.
            // Remote에서도 래그돌 포즈→애니메이션 포즈 블렌딩이 필요함.
            if (_currentState == ERagdollState.BlendToAnim)
                _blender.BlendToAnimation(_isAuthority, _rootBody);
        }

        #region Skeleton Detach / Reattach

        private void DetachSkeleton()
        {
            if (_isDetached) return;

            // 스켈레톤을 rootBody 자식에서 분리하여 rootBody 이동의 영향을 받지 않도록 함.
            _skeletonRoot.SetParent(transform.root, true);
            _isDetached = true;
        }

        private void ReattachSkeleton()
        {
            if (!_isDetached) return;

            _skeletonRoot.SetParent(_skeletonOriginalParent, true);
            _isDetached = false;
        }

        #endregion

        #region Root Control

        private void TrackPelvis()
        {
            Vector3 pelvisPos = _ragdollRig.PelvisTransform.position;
            float t = 1f - Mathf.Exp(-_rootBodyTrackingSpeed * Time.fixedDeltaTime);
            _rootBody.MovePosition(Vector3.Lerp(_rootBody.position, pelvisPos, t));
        }

        private void ResetRootControl(bool trackPelvis = false)
        {
            _shouldTrackPelvis = trackPelvis;
            _rootTransition.Cancel();
        }

        #endregion

        #region Public API (Authority)

        public void ApplyHit(HitData hit)
        {
            if (!_isAuthority) return;

            if (hit.Response != EHitResponse.Default)
            {
                ApplyForcedHit(hit);
                return;
            }

            ApplyThresholdBasedHit(hit);
        }

        private void ApplyThresholdBasedHit(HitData hit)
        {
            float effectiveMagnitude = hit.Magnitude + _instability;

            switch (_currentState)
            {
                case ERagdollState.BlendToAnim:
                    if (effectiveMagnitude >= _thresholdProfile.RagdollThreshold * _reImpactMultiplier)
                        EnterRagdolled(hit);
                    break;

                case ERagdollState.Ragdolled:
                    _hitApplier.ApplyAdditionalHit(hit);
                    _stateTimer = 0f;
                    break;

                case ERagdollState.Animated:
                    if (effectiveMagnitude >= _thresholdProfile.RagdollThreshold)
                        EnterRagdolled(hit);
                    else if (hit.Magnitude >= _thresholdProfile.StumbleThreshold)
                        EnterStumble(hit);
                    break;
            }
        }

        private void ApplyForcedHit(HitData hit)
        {
            if (_currentState == ERagdollState.Dead) return;

            if (_currentState == ERagdollState.Ragdolled)
            {
                _hitApplier.ApplyAdditionalHit(hit);
                _stateTimer = 0f;
                return;
            }

            switch (hit.Response)
            {
                case EHitResponse.Ragdoll:
                    EnterRagdolled(hit);
                    break;

                case EHitResponse.Stumble:
                    if (_currentState == ERagdollState.BlendToAnim)
                        EnterRagdolled(hit);
                    else
                        EnterStumble(hit);
                    break;

                case EHitResponse.Push:
                    if (_currentState == ERagdollState.Animated)
                    {
                        _rootBody.AddForce(hit.Knockback, ForceMode.Impulse);
                        _instability += hit.Magnitude;
                    }
                    break;
            }
        }

        public void EnterDead()
        {
            if (_currentState == ERagdollState.Dead) return;

            DetachSkeleton();

            Vector3 inheritedVelocity = _rootBody.linearVelocity;
            _ragdollRig.ActivatePhysics(inheritedVelocity);
            _hitApplier.ApplyInitialHit(
                new HitData(Vector3.zero, _rootBody.position, Vector3.zero),
                inheritedVelocity);

            _animator.enabled = false;
            _rootBody.isKinematic = true;
            ResetRootControl();

            _currentState = ERagdollState.Dead;
            OnStateChanged?.Invoke(ERagdollState.Dead);
        }

        public void Respawn()
        {
            if (_currentState != ERagdollState.Dead) return;

            _ragdollRig.DeactivateRagdoll();
            ReattachSkeleton();

            _animator.enabled = true;
            _rootBody.isKinematic = false;
            _rootBody.linearVelocity = Vector3.zero;
            _rootBody.angularVelocity = Vector3.zero;

            _instability = 0f;
            _stateTimer = 0f;

            _animation.ClearGetUpState();
            _animation.ClearStumbleState();

            _currentState = ERagdollState.Animated;
            OnStateChanged?.Invoke(ERagdollState.Animated);
        }

        public void ForceRecover()
        {
            if (_currentState == ERagdollState.Dead) return;

            ResetRootControl();
            _instability = 0f;
            _stateTimer = 0f;

            if (_currentState == ERagdollState.Ragdolled || _currentState == ERagdollState.BlendToAnim)
                _ragdollRig.DeactivateRagdoll();

            ReattachSkeleton();

            _animator.enabled = true;
            _rootBody.isKinematic = false;
            _rootBody.linearVelocity = Vector3.zero;
            _rootBody.angularVelocity = Vector3.zero;

            _animation.ClearGetUpState();
            _animation.ClearStumbleState();

            _currentState = ERagdollState.Animated;
            OnStateChanged?.Invoke(ERagdollState.Animated);
        }

        // 현재 상태에 맞는 복구 경로로 분기.
        // Dead → Respawn (스켈레톤 재연결), 그 외 → ForceRecover (래그돌 해제 + 초기화).
        public void TriggerRecovery()
        {
            if (_currentState == ERagdollState.Dead)
                Respawn();
            else
                ForceRecover();
        }

        // Animator의 GetUp 애니메이션이 끝나면 호출.
        public void OnGetUpComplete()
        {
            if (_currentState != ERagdollState.BlendToAnim) return;
            TransitionToAnimated();
        }

        #endregion

        #region Remote Entry (RagdollStateNetworkBridge가 호출)

        public void EnterRagdolledRemote()
        {
            _currentState = ERagdollState.Ragdolled;
            _stateTimer = 0f;
            ResetRootControl();

            DetachSkeleton();
            _ragdollRig.ActivateKinematic();
            _animator.enabled = false;
            _rootBody.isKinematic = true;

            OnStateChanged?.Invoke(_currentState);
        }

        public void EnterBlendToAnimRemote(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            ResetRootControl();

            // BoneReceiver가 위치시킨 래그돌 포즈를 블렌드용으로 캡처.
            // Animator 활성화 전에 캡처해야 현재 래그돌 포즈를 유지할 수 있음.
            _blender.SnapshotRagdollPoses();

            _ragdollRig.DeactivateRagdoll();
            ReattachSkeleton();

            _rootTransition.Begin(_rootBody, rootPos, rootRot);

            _animator.enabled = true;
            _animation.GetUp(isFaceUp);

            _blender.StartBlend();
            _currentState = ERagdollState.BlendToAnim;
            _stateTimer = 0f;

            OnStateChanged?.Invoke(_currentState);
        }

        public void EnterAnimatedRemote()
        {
            _currentState = ERagdollState.Animated;
            ResetRootControl();
            ReattachSkeleton();
            _animator.enabled = true;
            _animation.ClearGetUpState();

            // 래그돌 진입 시 kinematic으로 전환했으므로 복원.
            // 합체 모드에서는 BodySimulationToggle이 다시 kinematic으로 되돌림.
            _rootBody.isKinematic = false;
            _rootBody.linearVelocity = Vector3.zero;
            _rootBody.angularVelocity = Vector3.zero;

            OnStateChanged?.Invoke(_currentState);
        }

        public void PlayStumbleAnimation()
        {
            _animation.Stumble();
        }

        public void EnterDeadRemote()
        {
            _currentState = ERagdollState.Dead;
            ResetRootControl();

            DetachSkeleton();
            _ragdollRig.ActivateKinematic();
            _rootBody.isKinematic = true;
            _animator.enabled = false;

            OnStateChanged?.Invoke(_currentState);
        }

        public void RespawnRemote()
        {
            _currentState = ERagdollState.Animated;
            ResetRootControl();
            ReattachSkeleton();
            _ragdollRig.DeactivateRagdoll();
            _animator.enabled = true;
            _rootBody.isKinematic = false;
            _rootBody.linearVelocity = Vector3.zero;
            _rootBody.angularVelocity = Vector3.zero;

            OnStateChanged?.Invoke(_currentState);
        }

        #endregion

        #region Stumble (Authority)

        private void EnterStumble(HitData hit)
        {
            _instability += hit.Magnitude;
            _rootBody.AddForce(hit.Knockback, ForceMode.Impulse);
            _animation.Stumble();
            OnStumblePlayed?.Invoke();
        }

        #endregion

        #region Ragdolled (Authority) — 물리 래그돌

        private void EnterRagdolled(HitData hit)
        {
            _currentState = ERagdollState.Ragdolled;
            _instability = 0f;
            _stateTimer = 0f;

            // 스켈레톤 분리 → 물리 활성화.
            DetachSkeleton();

            Vector3 inheritedVelocity = _rootBody.linearVelocity;
            _ragdollRig.ActivatePhysics(inheritedVelocity);
            _hitApplier.ApplyInitialHit(hit, inheritedVelocity);

            _animator.enabled = false;
            _rootBody.isKinematic = true;
            ResetRootControl(trackPelvis: true);

            OnStateChanged?.Invoke(ERagdollState.Ragdolled);
        }

        private void UpdateRagdoll()
        {
            _stateTimer += Time.deltaTime;

            if (_stateTimer < _minRagdollDuration)
                return;

            bool grounded = IsGrounded();
            bool settled = _ragdollRig.IsSettled(_settleVelocity);
            Debug.Log($"[Ragdoll] timer={_stateTimer:F2} grounded={grounded} settled={settled} pelvisY={_ragdollRig.PelvisTransform.position.y:F2}");

            if (!grounded)
                return;

            if (settled || _stateTimer >= _maxRagdollDuration)
                BeginBlendToAnim();
        }

        #endregion

        #region BlendToAnim (Authority)

        private void BeginBlendToAnim()
        {
            _blender.SnapshotRagdollPoses();
            _blender.SnapshotRootAlignment();

            bool isFaceUp = IsFaceUp();

            // 래그돌 비활성화 + 스켈레톤 재결합.
            _ragdollRig.DeactivateRagdoll();
            ReattachSkeleton();

            // 루트 바디를 래그돌 최종 위치에 맞춤.
            Transform hipBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _blender.AlignRootBodyToPelvis(_rootBody, hipBone);
            _shouldTrackPelvis = false;

            // 애니메이터 재활성화 + 기립 애니메이션.
            _animator.enabled = true;
            _animation.GetUp(isFaceUp);

            _blender.StartBlend();
            _currentState = ERagdollState.BlendToAnim;
            _stateTimer = 0f;

            OnStateChanged?.Invoke(ERagdollState.BlendToAnim);
        }

        private void UpdateBlendTimer()
        {
            _stateTimer += Time.deltaTime;

            if (_blender.IsBlendComplete())
                TransitionToAnimated();
        }

        #endregion

        #region Helpers

        private void TransitionToAnimated()
        {
            _rootBody.isKinematic = false;
            _rootBody.linearVelocity = Vector3.zero;
            _rootBody.angularVelocity = Vector3.zero;

            _currentState = ERagdollState.Animated;
            _animation.ClearGetUpState();
            OnStateChanged?.Invoke(ERagdollState.Animated);
        }

        public Vector3 RecoveryPosition => _rootBody.position;
        public Quaternion RecoveryRotation => _rootBody.rotation;

        public bool IsFaceUp()
        {
            Transform pelvis = _ragdollRig.PelvisTransform;
            return (pelvis.rotation * Vector3.forward).y > 0f;
        }

        private void DecayInstability()
        {
            if (_instability > 0f)
                _instability = Mathf.Max(0f, _instability - _instabilityDecayRate * Time.deltaTime);
        }

        private bool IsGrounded()
        {
            Vector3 pelvisPos = _ragdollRig.PelvisTransform.position;
            Vector3 rayOrigin = pelvisPos + Vector3.up * RayOriginUpOffset;
            bool hit = Physics.Raycast(rayOrigin, Vector3.down, _groundCheckDistance, _groundLayer);
            Debug.DrawRay(rayOrigin, Vector3.down * _groundCheckDistance, hit ? Color.green : Color.red);
            return hit;
        }

        #endregion
    }
}
