using InGame.Player._02._Domain.Ragdoll;
using PlayerSystem.Domain;
using PlayerSystem.Ragdoll;
using UnityEngine;

namespace PlayerSystem.Test
{
    public class RagdollTestDriver : MonoBehaviour
    {
        [SerializeField] private float _testImpulseForce = 15f;
        [SerializeField] private float _testLightHitForce = 5f;

        private IRagdoll[] _ragdolls;

        private void Awake()
        {
            _ragdolls = GetComponentsInChildren<RagdollController>(true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
                ApplyImpulseToActive(_testLightHitForce);

            if (Input.GetKeyDown(KeyCode.R))
                ApplyImpulseToActive(_testImpulseForce);
        }

        private void ApplyImpulseToActive(float force)
        {
            foreach (var ragdoll in _ragdolls)
            {
                var mb = (MonoBehaviour)ragdoll;
                if (!mb.gameObject.activeInHierarchy) continue;

                Vector3 randomDir = Random.insideUnitSphere.normalized;
                randomDir.y = Mathf.Abs(randomDir.y);
                Vector3 impulse = randomDir * force;
                Vector3 hitPoint = mb.transform.position + Vector3.up * 0.5f;
                ragdoll.OnHitImpact(impulse, hitPoint);
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            GUI.color = Color.white;

            int y = 10;
            foreach (var ragdoll in _ragdolls)
            {
                var mb = (MonoBehaviour)ragdoll;
                if (!mb.gameObject.activeInHierarchy) continue;

                GUI.Label(new Rect(10, y, 500, 40),
                    $"{mb.gameObject.name}: {ragdoll.CurrentState}", style);
                y += 40;
            }

            GUI.Label(new Rect(10, y, 400, 30),
                "[T] Light Hit  [R] Ragdoll", GUI.skin.label);
        }
    }
}
