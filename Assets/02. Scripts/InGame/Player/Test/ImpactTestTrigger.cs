using InGame.UserInput;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// 키 입력으로 래그돌 충격을 테스트하는 디버그 스크립트.
    /// TeamCharacter 루트에 부착하여 사용한다.
    /// </summary>
    public class ImpactTestTrigger : MonoBehaviour
    {
        public enum ImpactTarget
        {
            Merged,
            AvatarA,
            AvatarB
        }

        [Header("Target")]
        [SerializeField] private ImpactTarget _target = ImpactTarget.Merged;

        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;
        [SerializeField] private GameObject _avatarA;
        [SerializeField] private GameObject _avatarB;

        [Header("Impulse")]
        [SerializeField] private float _impulseMagnitude = 10f;

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
                UnityEngine.Debug.LogWarning($"[ImpactTestTrigger] Target body '{_target}' is null or inactive.");
                return;
            }

            var pv = body.GetComponentInChildren<PhotonView>();
            if (pv == null)
            {
                UnityEngine.Debug.LogWarning($"[ImpactTestTrigger] No PhotonView on '{body.name}'.");
                return;
            }

            Vector3 impulse = Random.onUnitSphere * _impulseMagnitude;
            Vector3 hitPoint = body.transform.position;
            int viewID = pv.ViewID;

            Vector3 torque = Random.insideUnitSphere * impulse.magnitude * 0.15f;
            _input.SendImpact(impulse, hitPoint, viewID, torque);
            UnityEngine.Debug.Log($"[ImpactTestTrigger] Sent impact to '{_target}' (ViewID={viewID}), impulse={impulse}");
        }

        private GameObject GetTargetBody()
        {
            return _target switch
            {
                ImpactTarget.Merged => _mergedBody,
                ImpactTarget.AvatarA => _avatarA,
                ImpactTarget.AvatarB => _avatarB,
                _ => null
            };
        }
    }
}
