using PlayerSystem.Domain;
using System.Collections.Generic;
using InGame.Player._02._Domain.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PlayerSystem.Ragdoll
{
    public class RagdollImpactTransfer
    {
        private const float TorqueScaleFactor = 0.15f;

        private readonly IReadOnlyList<Rigidbody> _ragdollRbs;

        public RagdollImpactTransfer(IReadOnlyList<Rigidbody> ragdollRbs)
        {
            _ragdollRbs = ragdollRbs;
        }

        public void TransferImpact(ImpactData impact, Vector3 inheritedVelocity)
        {
            for (int i = 0; i < _ragdollRbs.Count; i++)
                _ragdollRbs[i].linearVelocity = inheritedVelocity;

            Rigidbody closestRb = GetClosestBoneRb(impact.HitPoint);
            closestRb.AddForce(impact.Impulse, ForceMode.Impulse);
            closestRb.AddTorque(Random.insideUnitSphere * impact.Magnitude * TorqueScaleFactor, ForceMode.Impulse);
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
