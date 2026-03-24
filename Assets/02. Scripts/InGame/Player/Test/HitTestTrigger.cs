using InGame.UserInput;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;

namespace InGame.Player.Test
{
    /// <summary>
    /// 키 입력으로 래그돌 충격을 테스트하는 디버그 스크립트.
    /// TeamCharacter 루트에 부착하여 사용한다.
    /// </summary>
    public class HitTestTrigger : MonoBehaviour
    {
        public enum HitTarget
        {
            Merged,
            AvatarA,
            AvatarB
        }

        [Header("Target")]
        [SerializeField] private HitTarget _target = HitTarget.Merged;

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        [Header("Knockback")]
        [FormerlySerializedAs("_impulseMagnitude")]
        [SerializeField] private float _knockbackMagnitude = 10f;

        [Header("Key")]
        [SerializeField] private KeyCode _triggerKey = KeyCode.T;

        private LocalPlayerInput _input;

        private void Awake()
        {
            _input = GetComponent<LocalPlayerInput>();
        }

        private void Update()
        {
            if (_input == null ) return;
            if (!Input.GetKeyDown(_triggerKey)) return;

            GameObject body = GetTargetBody();
            if (body == null || !body.activeInHierarchy)
            {
                UnityEngine.Debug.LogWarning($"[HitTestTrigger] Target body '{_target}' is null or inactive.");
                return;
            }

            var pv = body.GetComponentInChildren<PhotonView>();
            if (pv == null)
            {
                UnityEngine.Debug.LogWarning($"[HitTestTrigger] No PhotonView on '{body.name}'.");
                return;
            }

            Vector3 knockback = Random.onUnitSphere * _knockbackMagnitude;
            Vector3 hitPoint = body.transform.position;
            int viewID = pv.ViewID;

            var hit = new HitData(knockback, hitPoint, HitData.ComputeRandomTorque(knockback.magnitude));
            _input.SendHit(hit, viewID);
            UnityEngine.Debug.Log($"[HitTestTrigger] Sent hit to '{_target}' (ViewID={viewID}), knockback={knockback}");
        }

        private GameObject GetTargetBody()
        {
            return _target switch
            {
                HitTarget.Merged => _mergedBody,
                HitTarget.AvatarA => _avatarA,
                HitTarget.AvatarB => _avatarB,
                _ => null
            };
        }
    }
}
