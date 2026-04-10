using Photon.Pun;
using UnityEngine;

namespace InGame.Platform
{
    public class PlatformRotation : MonoBehaviour, IPunObservable
    {
        public enum RotationAxis
        {
            X,
            Y,
            Z
        }

        public RotationAxis rotationAxis = RotationAxis.Y;
        public float rotationSpeed = 50.0f;

        private const float NetworkCorrectionFactor = 0.2f;

        private Rigidbody _rb;
        private PhotonView _view;
        private Quaternion _networkRotation;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
            }
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _view = GetComponent<PhotonView>();
            _networkRotation = _rb.rotation;
        }

        private void FixedUpdate()
        {
            float rotationValue = rotationSpeed * Time.fixedDeltaTime;

            Vector3 axis = rotationAxis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                _ => Vector3.up
            };

            Quaternion deltaRotation = Quaternion.AngleAxis(rotationValue, axis);

            if (_view == null || _view.IsMine)
            {
                _rb.MoveRotation(_rb.rotation * deltaRotation);
            }
            else
            {
                Quaternion predicted = _rb.rotation * deltaRotation;
                _rb.MoveRotation(Quaternion.Slerp(predicted, _networkRotation, NetworkCorrectionFactor));
            }
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(_rb.rotation);
            }
            else
            {
                _networkRotation = (Quaternion)stream.ReceiveNext();
            }
        }
    }
}
