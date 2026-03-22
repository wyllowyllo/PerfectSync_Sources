using System;
using InGame.Player.Animation;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    // 단일 계층 래그돌 상태머신.
    // Animator가 제어하는 본 = Rigidbody가 달린 본. PoseTransfer 불필요.
    // 래그돌 진입 시 스켈레톤을 rootBody 자식에서 분리하여 물리 독립 보장.
    public class RagdollStateMachine : MonoBehaviour, IRagdoll
    {
        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody _rootBody;
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private PlayerAnimation _animation;
        [SerializeField] private Transform _skeletonRoot;

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

        [Header("Blend")]
        [SerializeField] private float _ragdollToAnimBlendTime = 0.5f;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckDistance = 10f;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Re-Impact")]
        [SerializeField, Range(0f, 1f)] private float _reImpactMultiplier = 0.6f;

        [Header("Instability")]
        [SerializeField] private float _instabilityDecayRate = 4f;

        [Header("Root Body Tracking")]
        [SerializeField] private float _rootBodyTrackingSpeed = 5f;

        [Header("Remote Root Lerp")]
        [SerializeField] private float _rootLerpDuration = 0.4f;

        // State.
        private ERagdollState _currentState = ERagdollState.Animated;
        private RagdollImpactTransfer _impactTransfer;
        private float _instability;
        private float _stateTimer;
        private float _stumbleDuration;
        private bool _shouldTrackPelvis;
        private bool _isAuthority = true;

        // Skeleton detach.
        private Transform _skeletonOriginalParent;
        private bool _isDetached;

        // Blend data (RagdollHelper 방식).
        private struct BonePose
        {
            public Transform Transform;
            public Vector3 StoredPosition;
            public Quaternion StoredRotation;
        }

        private BonePose[] _blendBones;
        private int _hipBoneIndex = -1;
        private Vector3 _ragdolledHipPosition;
        private Vector3 _ragdolledHeadPosition;
        private Vector3 _ragdolledFeetPosition;
        private float _blendStartTime;

        // Remote root lerp.
        private bool _isLerpingRoot;
        private Vector3 _lerpStartPos;
        private Quaternion _lerpStartRot;
        private Vector3 _lerpTargetPos;
        private Quaternion _lerpTargetRot;
        private float _lerpTimer;

        private const float MecanimTransitionTime = 0.05f;
        private const float RayOriginUpOffset = 0.5f;
        private const float MinDirectionSqrMagnitude = 0.001f;

        public ERagdollState CurrentState => _currentState;
        public bool IsRagdollActive => _currentState != ERagdollState.Animated;

        public bool IsPhysicsRagdoll => _currentState == ERagdollState.Ragdolled
                                        || _currentState == ERagdollState.Dead;

        public event Action<ERagdollState> OnStateChanged;

        public void SetAuthority(bool isAuthority)
        {
            _isAuthority = isAuthority;
        }

        private void Start()
        {
            _skeletonOriginalParent = _skeletonRoot.parent;
            _impactTransfer = new RagdollImpactTransfer(
                _ragdollRig.Rigidbodies, _impactRadius, _impactForceScale);
            InitializeBlendBones();
        }

        private void InitializeBlendBones()
        {
            var transforms = _skeletonRoot.GetComponentsInChildren<Transform>(true);
            _blendBones = new BonePose[transforms.Length];

            Transform hipTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);

            for (int i = 0; i < transforms.Length; i++)
            {
                _blendBones[i].Transform = transforms[i];
                if (transforms[i] == hipTransform)
                    _hipBoneIndex = i;
            }
        }

        private void Update()
        {
            if (!_isAuthority) return;

            DecayInstability();

            switch (_currentState)
            {
                case ERagdollState.Stumble:
                    UpdateStumble();
                    break;
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
            {
                Vector3 pelvisPos = _ragdollRig.PelvisTransform.position;
                float t = 1f - Mathf.Exp(-_rootBodyTrackingSpeed * Time.fixedDeltaTime);
                _rootBody.MovePosition(Vector3.Lerp(_rootBody.position, pelvisPos, t));
            }

            if (_isLerpingRoot)
            {
                _lerpTimer += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(_lerpTimer / _rootLerpDuration);
                float smoothT = t * t * (3f - 2f * t);

                _rootBody.MovePosition(Vector3.Lerp(_lerpStartPos, _lerpTargetPos, smoothT));
                _rootBody.MoveRotation(Quaternion.Slerp(_lerpStartRot, _lerpTargetRot, smoothT));

                if (t >= 1f)
                    _isLerpingRoot = false;
            }
        }

        private void LateUpdate()
        {
            // GetUp 애니메이션 반복 방지 (RagdollHelper 방식).
            _animation.ClearGetUpState();

            if (!_isAuthority) return;

            // 단일 계층: Ragdolled/Dead 중에는 물리가 본을 직접 구동하므로 복사 불필요.
            if (_currentState == ERagdollState.BlendToAnim)
                ApplyBlend();
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

        #region Public API (Authority)

        public void OnHitImpact(Vector3 impulse, Vector3 hitPoint, Vector3 torqueVector)
        {
            if (!_isAuthority) return;

            var impact = new ImpactData(impulse, hitPoint);
            float effectiveMagnitude = impact.Magnitude + _instability;

            switch (_currentState)
            {
                case ERagdollState.BlendToAnim:
                    if (effectiveMagnitude >= _ragdollThreshold * _reImpactMultiplier)
                        EnterRagdolled(impact, torqueVector);
                    break;

                case ERagdollState.Ragdolled:
                    _impactTransfer.ApplyAdditionalImpact(impact, torqueVector);
                    _stateTimer = 0f;
                    break;

                case ERagdollState.Animated:
                case ERagdollState.Stumble:
                    if (effectiveMagnitude >= _ragdollThreshold)
                        EnterRagdolled(impact, torqueVector);
                    else if (impact.Magnitude >= _stumbleThreshold)
                        EnterStumble(impact);
                    break;
            }
        }

        public void EnterDead()
        {
            if (_currentState == ERagdollState.Dead) return;

            DetachSkeleton();

            Vector3 inheritedVelocity = _rootBody.linearVelocity;
            _ragdollRig.Activate(inheritedVelocity);
            _impactTransfer.TransferImpact(
                new ImpactData(Vector3.zero, _rootBody.position),
                inheritedVelocity,
                Vector3.zero);

            _animator.enabled = false;
            _rootBody.isKinematic = true;
            _shouldTrackPelvis = false;
            _isLerpingRoot = false;

            _currentState = ERagdollState.Dead;
            OnStateChanged?.Invoke(ERagdollState.Dead);
        }

        public void ForceRecover()
        {
            if (_currentState == ERagdollState.Dead) return;

            _shouldTrackPelvis = false;
            _isLerpingRoot = false;
            _instability = 0f;
            _stateTimer = 0f;

            if (_currentState == ERagdollState.Ragdolled || _currentState == ERagdollState.BlendToAnim)
                _ragdollRig.Deactivate();

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
            _shouldTrackPelvis = false;
            _isLerpingRoot = false;

            DetachSkeleton();
            _ragdollRig.ActivateKinematic();
            _animator.enabled = false;
        }

        public void EnterBlendToAnimRemote(Vector3 rootPos, Quaternion rootRot, bool isFaceUp)
        {
            _shouldTrackPelvis = false;
            _ragdollRig.Deactivate();
            ReattachSkeleton();

            _lerpStartPos = _rootBody.position;
            _lerpStartRot = _rootBody.rotation;
            _lerpTargetPos = rootPos;
            _lerpTargetRot = rootRot;
            _lerpTimer = 0f;
            _isLerpingRoot = true;

            _animator.enabled = true;
            _animation.GetUp(isFaceUp);

            _currentState = ERagdollState.BlendToAnim;
            _stateTimer = 0f;
        }

        public void EnterAnimatedRemote()
        {
            _currentState = ERagdollState.Animated;
            _isLerpingRoot = false;
            ReattachSkeleton();
            _animator.enabled = true;
            _animation.ClearGetUpState();
        }

        public void EnterStumbleRemote()
        {
            _currentState = ERagdollState.Stumble;
            _animation.Stumble();
            _stumbleDuration = _maxStumbleDuration;
            _stateTimer = 0f;
        }

        public void EnterDeadRemote()
        {
            _currentState = ERagdollState.Dead;
            _shouldTrackPelvis = false;
            _isLerpingRoot = false;

            DetachSkeleton();
            _ragdollRig.ActivateKinematic();
            _animator.enabled = false;
        }

        #endregion

        #region Stumble (Authority)

        private void EnterStumble(ImpactData impact)
        {
            _currentState = ERagdollState.Stumble;
            _instability += impact.Magnitude;

            Vector3 pushDir = impact.Impulse.normalized;
            _rootBody.AddForce(
                pushDir * impact.Magnitude * _stumblePushMultiplier,
                ForceMode.Impulse);

            _animation.Stumble();

            _stumbleDuration = Mathf.Clamp(
                impact.Magnitude * _stumbleDurationPerImpulse,
                _minStumbleDuration,
                _maxStumbleDuration);
            _stateTimer = 0f;

            OnStateChanged?.Invoke(ERagdollState.Stumble);
        }

        private void UpdateStumble()
        {
            _stateTimer += Time.deltaTime;

            if (_stateTimer >= _stumbleDuration)
            {
                _currentState = ERagdollState.Animated;
                _animation.ClearStumbleState();
                OnStateChanged?.Invoke(ERagdollState.Animated);
            }
        }

        #endregion

        #region Ragdolled (Authority) — 물리 래그돌

        private void EnterRagdolled(ImpactData impact, Vector3 torqueVector)
        {
            _currentState = ERagdollState.Ragdolled;
            _instability = 0f;
            _stateTimer = 0f;

            // 스켈레톤 분리 → 물리 활성화.
            DetachSkeleton();

            Vector3 inheritedVelocity = _rootBody.linearVelocity;
            _ragdollRig.Activate(inheritedVelocity);
            _impactTransfer.TransferImpact(impact, inheritedVelocity, torqueVector);

            _animator.enabled = false;
            _rootBody.isKinematic = true;
            _shouldTrackPelvis = true;
            _isLerpingRoot = false;

            OnStateChanged?.Invoke(ERagdollState.Ragdolled);
        }

        private void UpdateRagdoll()
        {
            _stateTimer += Time.deltaTime;

            if (_stateTimer < _minRagdollDuration)
                return;

            if (IsReadyToRecover() || _stateTimer >= _maxRagdollDuration)
                BeginBlendToAnim();
        }

        #endregion

        #region BlendToAnim (Authority) — RagdollHelper 방식 블렌드

        private void BeginBlendToAnim()
        {
            // 블렌드용 본 포즈 캡처 (물리가 구동한 현재 포즈).
            CaptureBlendPoses();

            // 루트 매칭용 위치 저장.
            Transform hipBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);

            _ragdolledHipPosition = hipBone.position;
            _ragdolledHeadPosition = headBone.position;
            _ragdolledFeetPosition = 0.5f * (leftFoot.position + rightFoot.position);

            bool isFaceUp = (hipBone.rotation * Vector3.forward).y > 0f;

            // 래그돌 비활성화 + 스켈레톤 재결합.
            _ragdollRig.Deactivate();
            ReattachSkeleton();

            // 루트 바디를 래그돌 최종 위치에 맞춤.
            AlignRootBodyToPelvis(hipBone);
            _shouldTrackPelvis = false;

            // 애니메이터 재활성화 + 기립 애니메이션.
            _animator.enabled = true;
            _animation.GetUp(isFaceUp);

            _blendStartTime = Time.time;
            _currentState = ERagdollState.BlendToAnim;
            _stateTimer = 0f;

            OnStateChanged?.Invoke(ERagdollState.BlendToAnim);
        }

        private void UpdateBlendTimer()
        {
            _stateTimer += Time.deltaTime;

            float elapsed = Time.time - _blendStartTime - MecanimTransitionTime;
            float ragdollBlend = 1.0f - elapsed / _ragdollToAnimBlendTime;

            if (ragdollBlend <= 0f)
                TransitionToAnimated();
        }

        private void ApplyBlend()
        {
            float elapsed = Time.time - _blendStartTime;

            // 메카님 전환 대기: 루트를 래그돌 위치에 맞추고 본을 래그돌 포즈로 유지.
            if (elapsed <= MecanimTransitionTime)
            {
                MatchRootToRagdolledPose();

                for (int i = 0; i < _blendBones.Length; i++)
                {
                    if (_blendBones[i].Transform == _skeletonRoot) continue;

                    _blendBones[i].Transform.rotation = _blendBones[i].StoredRotation;

                    if (i == _hipBoneIndex)
                        _blendBones[i].Transform.position = _blendBones[i].StoredPosition;
                }

                return;
            }

            // 블렌드 계수: 1.0(래그돌) → 0.0(애니메이션).
            float ragdollBlend = 1.0f
                - (elapsed - MecanimTransitionTime)
                / _ragdollToAnimBlendTime;
            ragdollBlend = Mathf.Clamp01(ragdollBlend);

            // Animator가 이미 이번 프레임 애니메이션 포즈를 적용한 상태.
            // 저장된 래그돌 포즈와 현재 애니메이션 포즈를 보간.
            for (int i = 0; i < _blendBones.Length; i++)
            {
                if (_blendBones[i].Transform == _skeletonRoot) continue;

                if (i == _hipBoneIndex)
                {
                    _blendBones[i].Transform.position = Vector3.Lerp(
                        _blendBones[i].Transform.position,
                        _blendBones[i].StoredPosition,
                        ragdollBlend);
                }

                _blendBones[i].Transform.rotation = Quaternion.Slerp(
                    _blendBones[i].Transform.rotation,
                    _blendBones[i].StoredRotation,
                    ragdollBlend);
            }
        }

        private void MatchRootToRagdolledPose()
        {
            Transform hipBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 offset = _ragdolledHipPosition - hipBone.position;
            Vector3 newRootPos = _rootBody.position + offset;

            newRootPos.y = GetGroundY(newRootPos);
            _rootBody.position = newRootPos;

            Vector3 ragdolledDir = _ragdolledHeadPosition - _ragdolledFeetPosition;
            ragdolledDir.y = 0f;

            Transform leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 animFeetPos = 0.5f * (leftFoot.position + rightFoot.position);
            Vector3 animDir = _animator.GetBoneTransform(HumanBodyBones.Head).position - animFeetPos;
            animDir.y = 0f;

            if (ragdolledDir.sqrMagnitude > MinDirectionSqrMagnitude
                && animDir.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                _rootBody.rotation *= Quaternion.FromToRotation(
                    animDir.normalized, ragdolledDir.normalized);
            }
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

        private void CaptureBlendPoses()
        {
            for (int i = 0; i < _blendBones.Length; i++)
            {
                _blendBones[i].StoredPosition = _blendBones[i].Transform.position;
                _blendBones[i].StoredRotation = _blendBones[i].Transform.rotation;
            }
        }

        public Vector3 GetRecoveryPosition() => _rootBody.position;
        public Quaternion GetRecoveryRotation() => _rootBody.rotation;

        public bool GetIsFaceUp()
        {
            Transform pelvis = _ragdollRig.PelvisTransform;
            return (pelvis.rotation * Vector3.forward).y > 0f;
        }

        private void DecayInstability()
        {
            if (_instability > 0f)
                _instability = Mathf.Max(
                    0f, _instability - _instabilityDecayRate * Time.deltaTime);
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

        private bool IsReadyToRecover()
        {
            if (!_ragdollRig.IsSettled(_settleVelocity))
                return false;

            Vector3 pelvisPos = _ragdollRig.PelvisTransform.position;
            Vector3 rayOrigin = pelvisPos + Vector3.up * RayOriginUpOffset;
            return Physics.Raycast(
                rayOrigin, Vector3.down, _groundCheckDistance, _groundLayer);
        }

        private float GetGroundY(Vector3 origin)
        {
            Vector3 rayOrigin = origin + Vector3.up * RayOriginUpOffset;

            if (Physics.Raycast(
                    rayOrigin, Vector3.down, out RaycastHit hit,
                    _groundCheckDistance, _groundLayer))
                return hit.point.y;

            return origin.y;
        }

        #endregion
    }
}
