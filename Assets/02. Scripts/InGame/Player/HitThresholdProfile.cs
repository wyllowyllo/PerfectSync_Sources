using UnityEngine;

namespace InGame.Player
{
    [CreateAssetMenu(fileName = "NewHitThresholdProfile", menuName = "InGame/Hit Threshold Profile")]
    public class HitThresholdProfile : ScriptableObject
    {
        [Tooltip("이 이상이면 비틀거림(stumble) 반응")]
        [SerializeField] private float _stumbleThreshold = 5f;

        [Tooltip("이 이상이면 래그돌(ragdoll) 반응")]
        [SerializeField] private float _ragdollThreshold = 7f;

        public float StumbleThreshold => _stumbleThreshold;
        public float RagdollThreshold => _ragdollThreshold;
    }
}
