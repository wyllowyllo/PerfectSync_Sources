using Core;
using InGame.Player.Ragdoll;
using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    // 카메라와 래그돌 사이의 중재자.
    // 래그돌 상태에서 속도 적응형 지수 감쇠로 pelvis를 추적하여
    // 저속 떨림은 흡수하고 고속 넉백은 즉시 따라감.
    [DefaultExecutionOrder(ExecutionOrderConstants.CameraTargetProvider)]
    public class CameraTargetProvider : MonoBehaviour
    {
        [SerializeField] private RagdollStateMachine _ragdollStateMachine;
        [SerializeField] private RagdollRig _ragdollRig;
        [SerializeField] private Rigidbody _rootBody;

        [Header("Adaptive Smoothing")]
        [SerializeField] private float _looseSpeed = 12f;
        [SerializeField] private float _tightSpeed = 60f;
        [SerializeField] private float _lowSpeedThreshold = 2f;
        [SerializeField] private float _highSpeedThreshold = 15f;

        [Header("Handoff")]
        [SerializeField] private float _handoffSpeed = 25f;

        [Header("Safety")]
        [SerializeField] private float _maxDistance = 6f;

        private Vector3 _smoothedPosition;
        private Vector3 _lastPelvisPosition;

        public Vector3 SmoothedPosition => _smoothedPosition;

        private void Start()
        {
            _smoothedPosition = _rootBody.position;
            _lastPelvisPosition = _ragdollRig.PelvisTransform.position;
        }

        private void FixedUpdate()
        {
            if (!_ragdollStateMachine.IsPhysicsRagdoll)
            {
                float interpolateTime = 1f - Mathf.Exp(-_handoffSpeed * Time.fixedDeltaTime);
                _smoothedPosition = Vector3.Lerp(_smoothedPosition, _rootBody.position, interpolateTime);
                _lastPelvisPosition = _ragdollRig.PelvisTransform.position;
                return;
            }

            Vector3 pelvisPos = _ragdollRig.PelvisTransform.position;

            float estimatedSpeed = (pelvisPos - _lastPelvisPosition).magnitude / Time.fixedDeltaTime;
            _lastPelvisPosition = pelvisPos;

            float speedRatio = Mathf.InverseLerp(_lowSpeedThreshold, _highSpeedThreshold, estimatedSpeed);
            float adaptiveSpeed = Mathf.Lerp(_looseSpeed, _tightSpeed, speedRatio);

            float t = 1f - Mathf.Exp(-adaptiveSpeed * Time.fixedDeltaTime);
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, pelvisPos, t);

            Vector3 delta = _smoothedPosition - pelvisPos;
            if (delta.sqrMagnitude > _maxDistance * _maxDistance)
                _smoothedPosition = pelvisPos + delta.normalized * _maxDistance;
        }

        public void WarpTo(Vector3 position)
        {
            _smoothedPosition = position;
            _lastPelvisPosition = position;
        }
    }
}
