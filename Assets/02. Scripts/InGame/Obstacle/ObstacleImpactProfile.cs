using UnityEngine;

namespace InGame.Obstacle
{
    public enum EForceMode
    {
        VelocityScaled,
        Fixed
    }

    public enum EDirectionMode
    {
        FromCollision,
        ContactNormal,
        ObstacleForward,
        Custom
    }

    [CreateAssetMenu(fileName = "NewImpactProfile", menuName = "InGame/Obstacle Impact Profile")]
    public class ObstacleImpactProfile : ScriptableObject
    {
        [Header("Force")]
        [SerializeField] private EForceMode _forceMode = EForceMode.VelocityScaled;

        [Tooltip("VelocityScaled: 상대속도에 곱해지는 배율 / Fixed: 고정 힘 크기")]
        [SerializeField] private float _forceValue = 1f;

        [Header("Direction")]
        [SerializeField] private EDirectionMode _directionMode = EDirectionMode.FromCollision;

        [Tooltip("DirectionMode가 Custom일 때 사용할 월드 방향")]
        [SerializeField] private Vector3 _customDirection = Vector3.forward;

        [Tooltip("밀림 방향에 상향 성분을 섞는 비율 (0 = 없음, 1 = 완전 위로)")]
        [SerializeField, Range(0f, 1f)] private float _upwardBias;

        [Header("Ragdoll")]
        [Tooltip("true면 항상 래그돌 임계치 이상의 magnitude를 보장")]
        [SerializeField] private bool _alwaysRagdoll;

        [Tooltip("alwaysRagdoll 시 보장할 최소 magnitude (래그돌 임계치보다 높게 설정)")]
        [SerializeField] private float _minRagdollMagnitude = 8f;

        [Header("Torque")]
        [SerializeField] private float _torqueScale = 0.15f;

        [Header("Cooldown")]
        [Tooltip("같은 대상에 대한 재히트 방지 시간 (초)")]
        [SerializeField] private float _cooldown;

        public float Cooldown => _cooldown;

        public Vector3 ComputeImpulse(Collision collision, Transform obstacleTransform)
        {
            Vector3 direction = _directionMode switch
            {
                EDirectionMode.ContactNormal => collision.GetContact(0).normal,
                EDirectionMode.ObstacleForward => obstacleTransform.forward,
                EDirectionMode.Custom => _customDirection.normalized,
                _ => collision.relativeVelocity.normalized
            };

            if (_upwardBias > 0f)
                direction = Vector3.Lerp(direction, Vector3.up, _upwardBias).normalized;

            float magnitude = _forceMode == EForceMode.Fixed
                ? _forceValue
                : collision.relativeVelocity.magnitude * _forceValue;

            if (_alwaysRagdoll)
                magnitude = Mathf.Max(magnitude, _minRagdollMagnitude);

            return direction * magnitude;
        }

        public Vector3 ComputeTorque(float impulseMagnitude)
        {
            return Random.insideUnitSphere * impulseMagnitude * _torqueScale;
        }
    }
}
