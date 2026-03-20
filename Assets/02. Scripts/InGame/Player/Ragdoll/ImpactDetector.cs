using InGame.Player.Network;
using InGame.UserInput;
using Photon.Pun;
using UnityEngine;

namespace InGame.Player.Ragdoll
{
    /// <summary>
    /// 바디 루트 캡슐의 충돌을 감지하여 LocalPlayerInput.SendImpact()를 호출한다.
    /// MergedPlayer, DividedPlayer_A, DividedPlayer_B 각각에 부착.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class ImpactDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask _hazardLayers;
        [SerializeField] private float _minImpulse = 5f;

        private PhotonView _photonView;
        private LocalPlayerInput _localPlayerInput;
        private BodySimulationToggle _bodyToggle;

        private void Awake()
        {
            _photonView = GetComponent<PhotonView>();
            _bodyToggle = GetComponent<BodySimulationToggle>();
            _localPlayerInput = GetComponentInParent<LocalPlayerInput>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Remote(네트워크 동기화) 바디는 충돌 감지 안 함
            if (_bodyToggle != null && _bodyToggle.IsRemote) return;

            // 지정된 레이어만 처리
            if ((_hazardLayers & (1 << collision.gameObject.layer)) == 0) return;

            Vector3 impulse = collision.relativeVelocity;
            if (impulse.sqrMagnitude < _minImpulse * _minImpulse) return;

            Vector3 hitPoint = collision.GetContact(0).point;
            _localPlayerInput.SendImpact(impulse, hitPoint, _photonView.ViewID);
        }
    }
}
