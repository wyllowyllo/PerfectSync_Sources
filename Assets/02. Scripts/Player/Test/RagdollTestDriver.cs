using Player.Ragdoll;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Test
{
    public class RagdollTestDriver : MonoBehaviour
    {
        [FormerlySerializedAs("ragdoll")]
        [FormerlySerializedAs("ragdollController")]
        [SerializeField] private RagdollController _ragdollController;
        [SerializeField] private float testImpulseForce = 15f;
        [FormerlySerializedAs("testStumbleForce")] [SerializeField] private float testLightHitForce = 5f;

        private IRagdollInput _ragdollInput;

        private void Awake()
        {
            _ragdollInput = _ragdollController;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                Vector3 randomDir = Random.insideUnitSphere.normalized;
                randomDir.y = Mathf.Abs(randomDir.y);
                Vector3 impulse = randomDir * testLightHitForce;
                Vector3 hitPoint = _ragdollController.transform.position + Vector3.up * 0.5f;
                _ragdollInput.OnHitImpact(impulse, hitPoint);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Vector3 randomDir = Random.insideUnitSphere.normalized;
                randomDir.y = Mathf.Abs(randomDir.y);
                Vector3 impulse = randomDir * testImpulseForce;
                Vector3 hitPoint = _ragdollController.transform.position + Vector3.up * 0.5f;
                _ragdollInput.OnHitImpact(impulse, hitPoint);
            }

        }

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 400, 40),
                $"State: {_ragdollInput.CurrentState}", style);
            GUI.Label(new Rect(10, 50, 400, 30),
                "[T] Light Hit  [R] Ragdoll", GUI.skin.label);
        }
    }
}
