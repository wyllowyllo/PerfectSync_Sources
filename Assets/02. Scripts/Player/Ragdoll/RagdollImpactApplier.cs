using UnityEngine;
using Random = UnityEngine.Random;

namespace Player.Ragdoll
{
    public class RagdollImpactApplier
    {
        private readonly Rigidbody[] _ragdollRbs;

        public RagdollImpactApplier(Rigidbody[] ragdollRbs)
        {
            _ragdollRbs = ragdollRbs;
        }

        public void Apply(ImpactData impact, Vector3 inheritedVelocity)
        {
            foreach (var rb in _ragdollRbs)
                rb.linearVelocity = inheritedVelocity;

            Rigidbody closestRb = GetClosestBoneRb(impact.HitPoint);
            closestRb.AddForce(impact.Impulse, ForceMode.Impulse);
            closestRb.AddTorque(
                Random.insideUnitSphere * impact.Magnitude * 0.15f,
                ForceMode.Impulse);
        }

        private Rigidbody GetClosestBoneRb(Vector3 point)
        {
            Rigidbody closest = _ragdollRbs[0];
            float closestSqr = (closest.position - point).sqrMagnitude;

            for (int i = 1; i < _ragdollRbs.Length; i++)
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
