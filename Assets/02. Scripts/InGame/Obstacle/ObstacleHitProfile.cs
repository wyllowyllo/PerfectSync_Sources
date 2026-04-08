using InGame.Player;
using UnityEngine;

namespace InGame.Obstacle
{
    public enum EHitDirection
    {
        RelativeVelocity,
        ObstacleForward,
        AwayFromCenter,
        Custom
    }

    [CreateAssetMenu(fileName = "NewObstacleHitProfile", menuName = "InGame/Obstacle Hit Profile")]
    public class ObstacleHitProfile : ScriptableObject
    {
        [Header("Response")]
        [Tooltip("Default: 상대속도 기반 threshold 판정 / 나머지: 강제 반응")]
        [SerializeField] private EHitResponse _response = EHitResponse.Default;

        [Header("Extra Knockback")]
        [Tooltip("장애물 고유 추가 넉백 크기")]
        [SerializeField] private float _extraKnockback = 8f;

        [Tooltip("x = 정규화된 상대속도(0~1), y = 넉백 배율. 비선형 과장/감쇠 조절용.")]
        [SerializeField] private AnimationCurve _speedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("커브 x=1에 해당하는 상대속도")]
        [SerializeField] private float _maxSpeed = 15f;

        [Header("Direction")]
        [SerializeField] private EHitDirection _direction = EHitDirection.RelativeVelocity;

        [Tooltip("EHitDirection.Custom일 때 사용할 월드 방향")]
        [SerializeField] private Vector3 _customDirection = Vector3.forward;

        [Tooltip("넉백 방향에 상향 성분을 섞는 비율 (0 = 없음, 1 = 완전 위로)")]
        [SerializeField, Range(0f, 1f)] private float _upwardBias;

        [Header("Torque")]
        [SerializeField] private float _torqueScale = 0.15f;

        [Header("Cooldown")]
        [Tooltip("같은 대상에 대한 재히트 방지 시간 (초)")]
        [SerializeField] private float _cooldown = 0.5f;

        public float Cooldown => _cooldown;
        public EHitResponse Response => _response;

        public Vector3 ComputeKnockback(Collision collision, Transform obstacleTransform)
        {
            Vector3 contactPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : obstacleTransform.position;
            return ComputeKnockback(contactPoint, collision.relativeVelocity, obstacleTransform);
        }

        public Vector3 ComputeKnockback(Vector3 contactPoint, Vector3 relativeVelocity, Transform obstacleTransform)
        {
            float relativeSpeed = relativeVelocity.magnitude;

            // Direction.
            Vector3 direction = _direction switch
            {
                EHitDirection.ObstacleForward => obstacleTransform.forward,
                EHitDirection.AwayFromCenter =>
                    (contactPoint - obstacleTransform.position).normalized,
                EHitDirection.Custom => _customDirection.normalized,
                _ => relativeSpeed > 0.001f
                    ? relativeVelocity.normalized
                    : obstacleTransform.forward
            };

            if (_upwardBias > 0f)
                direction = Vector3.Lerp(direction, Vector3.up, _upwardBias).normalized;

            // Magnitude: 속도 커브 기반.
            float normalizedSpeed = _maxSpeed > 0f ? Mathf.Clamp01(relativeSpeed / _maxSpeed) : 1f;
            float speedFactor = _speedCurve.Evaluate(normalizedSpeed);
            float magnitude = _extraKnockback * speedFactor;

            return direction * magnitude;
        }

        public Vector3 ComputeTorque(float knockbackMagnitude)
        {
            return HitData.ComputeRandomTorque(knockbackMagnitude, _torqueScale);
        }
    }
}
