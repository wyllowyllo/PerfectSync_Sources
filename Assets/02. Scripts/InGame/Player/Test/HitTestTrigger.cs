using InGame.UserInput;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// 키 입력으로 래그돌 충격을 테스트하는 디버그 스크립트.
    /// TeamCharacter 루트에 부착하여 사용한다.
    /// </summary>
    public class HitTestTrigger : MonoBehaviour
    {
        [Header("Bodies")]
        [SerializeField] private GameObject _mergedBody;

        [Header("Knockback")]
        [SerializeField] private float _knockbackMagnitude = 10f;

        [SerializeField] private EHitResponse _testResponse = EHitResponse.Ragdoll;

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

            if (_mergedBody == null || !_mergedBody.activeInHierarchy)
            {
                UnityEngine.Debug.LogWarning("[HitTestTrigger] Merged body is null or inactive.");
                return;
            }

            var pv = _mergedBody.GetComponentInChildren<PhotonView>();
            if (pv == null)
            {
                UnityEngine.Debug.LogWarning($"[HitTestTrigger] No PhotonView on '{_mergedBody.name}'.");
                return;
            }

            Vector3 knockback = Random.onUnitSphere * _knockbackMagnitude;
            Vector3 hitPoint = _mergedBody.transform.position;
            int viewID = pv.ViewID;

            var hit = new HitData(knockback, hitPoint, HitData.ComputeRandomTorque(knockback.magnitude), _testResponse);
            _input.SendHit(hit, viewID);
            UnityEngine.Debug.Log($"[HitTestTrigger] Sent hit (ViewID={viewID}), knockback={knockback}");
        }
    }
}
