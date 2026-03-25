using InGame.Player;
using UnityEngine;

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

    public enum EImpactLevel
    {
        Default,
        Stumble,
        Ragdoll,
        PushOnly
    }

    [CreateAssetMenu(fileName = "NewHitProfile", menuName = "InGame/Obstacle Hit Profile")]
    public class ObstacleHitProfile : ScriptableObject
    {
        [Header("Knockback")]
        [SerializeField] private EKnockbackMode _knockbackMode = EKnockbackMode.VelocityScaled;

        [Tooltip("VelocityScaled: 상대속도에 곱해지는 배율 / Fixed: 고정 넉백 크기")]
        [SerializeField] private float _knockbackStrength = 1f;

        [Header("Direction")]
        [SerializeField] private EKnockbackDirection _knockbackDirection = EKnockbackDirection.FromCollision;

        [Tooltip("KnockbackDirection이 Custom일 때 사용할 월드 방향")]
        [SerializeField] private Vector3 _customDirection = Vector3.forward;

        [Tooltip("밀림 방향에 상향 성분을 섞는 비율 (0 = 없음, 1 = 완전 위로)")]
        [SerializeField, Range(0f, 1f)] private float _upwardBias;

        [Header("Impact")]
        [SerializeField] private HitThresholdProfile _thresholdProfile;

        [Tooltip("Default: 속도 기반 자연스러운 결과 / Stumble·Ragdoll·PushOnly: _impactForce 직접 적용")]
        [SerializeField] private EImpactLevel _impactLevel = EImpactLevel.Default;

        [Tooltip("ImpactLevel이 Default가 아닐 때 적용할 넉백 크기")]
        [SerializeField] private float _impactForce;

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

            float magnitude;
            if (_impactLevel != EImpactLevel.Default)
            {
                magnitude = _impactForce;
            }
            else
            {
                magnitude = _knockbackMode == EKnockbackMode.Fixed
                    ? _knockbackStrength
                    : collision.relativeVelocity.magnitude * _knockbackStrength;
            }

            return direction * magnitude;
        }

        public Vector3 ComputeTorque(float knockbackMagnitude)
        {
            return HitData.ComputeRandomTorque(knockbackMagnitude, _torqueScale);
        }

        private void OnValidate()
        {
            if (_thresholdProfile == null || _impactLevel == EImpactLevel.Default) return;

            float stumble = _thresholdProfile.StumbleThreshold;
            float ragdoll = _thresholdProfile.RagdollThreshold;

            _impactForce = _impactLevel switch
            {
                EImpactLevel.PushOnly => Mathf.Clamp(_impactForce, 0f, stumble - 0.01f),
                EImpactLevel.Stumble => Mathf.Clamp(_impactForce, stumble, ragdoll - 0.01f),
                EImpactLevel.Ragdoll => Mathf.Max(_impactForce, ragdoll),
                _ => _impactForce
            };
        }
    }
}
