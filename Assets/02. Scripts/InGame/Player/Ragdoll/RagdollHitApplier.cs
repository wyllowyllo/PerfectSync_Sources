using System.Collections.Generic;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    public class RagdollHitApplier
    {
        private readonly IReadOnlyList<Rigidbody> _ragdollRbs;
        private readonly float _hitRadius;
        private readonly float _forceScale;
        private readonly float _maxInheritedSpeed;
        private readonly float _maxBoneSpeed;
        private readonly float _maxBoneSpeedSqr;

        public RagdollHitApplier(
            IReadOnlyList<Rigidbody> ragdollRbs,
            float hitRadius,
            float forceScale,
            CharacterSpeedLimits speedLimits)
        {
            _ragdollRbs = ragdollRbs;
            _hitRadius = hitRadius;
            _forceScale = forceScale;

            if (speedLimits != null)
            {
                _maxInheritedSpeed = speedLimits.MaxInheritedSpeed;
                _maxBoneSpeed = speedLimits.MaxBoneSpeed;
                _maxBoneSpeedSqr = speedLimits.MaxBoneSpeedSqr;
            }
            else
            {
                _maxInheritedSpeed = 20f;
                _maxBoneSpeed = 30f;
                _maxBoneSpeedSqr = 900f;
            }
        }

        /// <summary>
        /// 래그돌 진입 시 호출. 캡슐 속도를 상속한 뒤 충격을 거리 기반으로 분산 적용.
        /// </summary>
        public void ApplyInitialHit(HitData hit, Vector3 inheritedVelocity)
        {
            if (inheritedVelocity.sqrMagnitude > _maxInheritedSpeed * _maxInheritedSpeed)
                inheritedVelocity = inheritedVelocity.normalized * _maxInheritedSpeed;

            for (int i = 0; i < _ragdollRbs.Count; i++)
                _ragdollRbs[i].linearVelocity = inheritedVelocity;

            ApplyDistributedForce(hit);
        }

        /// <summary>
        /// 래그돌 상태에서 추가 충격. 기존 속도는 유지하고 힘만 추가.
        /// </summary>
        public void ApplyAdditionalHit(HitData hit)
        {
            ApplyDistributedForce(hit);
        }

        private void ApplyDistributedForce(HitData hit)
        {
            bool anyHit = false;

            for (int i = 0; i < _ragdollRbs.Count; i++)
            {
                float dist = Vector3.Distance(_ragdollRbs[i].position, hit.HitPoint);
                float falloff = Mathf.Clamp01(1f - dist / _hitRadius);
                if (falloff <= 0f) continue;

                _ragdollRbs[i].AddForce(hit.Knockback * (falloff * _forceScale), ForceMode.Impulse);
                _ragdollRbs[i].AddTorque(hit.Torque * (falloff * _forceScale), ForceMode.Impulse);
                anyHit = true;
            }

            // 반경 내 뼈가 없으면 가장 가까운 뼈에 전량 적용
            if (!anyHit)
            {
                Rigidbody closest = GetClosestBoneRb(hit.HitPoint);
                closest.AddForce(hit.Knockback * _forceScale, ForceMode.Impulse);
                closest.AddTorque(hit.Torque * _forceScale, ForceMode.Impulse);
            }

            // 임펄스 직후 속도 클램프: 다음 FixedUpdate까지 1프레임 과속 방지.
            ClampBoneVelocities();
        }

        private void ClampBoneVelocities()
        {
            for (int i = 0; i < _ragdollRbs.Count; i++)
            {
                Vector3 v = _ragdollRbs[i].linearVelocity;
                if (v.sqrMagnitude > _maxBoneSpeedSqr)
                    _ragdollRbs[i].linearVelocity = v.normalized * _maxBoneSpeed;
            }
        }

        private Rigidbody GetClosestBoneRb(Vector3 point)
        {
            Rigidbody closest = _ragdollRbs[0];
            float closestSqr = (closest.position - point).sqrMagnitude;

            for (int i = 1; i < _ragdollRbs.Count; i++)
            {
                float sqr = (_ragdollRbs[i].position - point).sqrMagnitude;
                if (sqr < closestSqr)
                {
                    closest = _ragdollRbs[i];
                    closestSqr = sqr;
                }
            }

            return closest;
        }
    }
}
