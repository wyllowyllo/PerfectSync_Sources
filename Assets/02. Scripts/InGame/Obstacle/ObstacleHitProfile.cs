using InGame.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Obstacle
{
    public enum EKnockbackMode
    {
        VelocityScaled,
        Fixed
    }

    public enum EKnockbackDirection
    {
        FromCollision,
        ContactNormal,
        ObstacleForward,
        Custom
    }

    [CreateAssetMenu(fileName = "NewHitProfile", menuName = "InGame/Obstacle Hit Profile")]
    public class ObstacleHitProfile : ScriptableObject
    {
        [Header("Knockback")]
        [FormerlySerializedAs("_forceMode")]
        [SerializeField] private EKnockbackMode _knockbackMode = EKnockbackMode.VelocityScaled;

        [Tooltip("VelocityScaled: 상대속도에 곱해지는 배율 / Fixed: 고정 넉백 크기")]
        [FormerlySerializedAs("_forceValue")]
        [SerializeField] private float _knockbackStrength = 1f;

        [Header("Direction")]
        [FormerlySerializedAs("_directionMode")]
        [SerializeField] private EKnockbackDirection _knockbackDirection = EKnockbackDirection.FromCollision;

        [Tooltip("KnockbackDirection이 Custom일 때 사용할 월드 방향")]
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

        public Vector3 ComputeKnockback(Collision collision, Transform obstacleTransform)
        {
            Vector3 direction = _knockbackDirection switch
            {
                EKnockbackDirection.ContactNormal => collision.GetContact(0).normal,
                EKnockbackDirection.ObstacleForward => obstacleTransform.forward,
                EKnockbackDirection.Custom => _customDirection.normalized,
                _ => collision.relativeVelocity.normalized
            };

            if (_upwardBias > 0f)
                direction = Vector3.Lerp(direction, Vector3.up, _upwardBias).normalized;

            float magnitude = _knockbackMode == EKnockbackMode.Fixed
                ? _knockbackStrength
                : collision.relativeVelocity.magnitude * _knockbackStrength;

            if (_alwaysRagdoll)
                magnitude = Mathf.Max(magnitude, _minRagdollMagnitude);

            return direction * magnitude;
        }

        public Vector3 ComputeTorque(float knockbackMagnitude)
        {
            return HitData.ComputeRandomTorque(knockbackMagnitude, _torqueScale);
        }
    }
}
