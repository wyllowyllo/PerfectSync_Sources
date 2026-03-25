using UnityEngine;

namespace InGame.Player.Ragdoll
{
    // 래그돌 복귀 시 루트 바디를 목표 위치로 부드럽게 전환.
    // BlendToAnim 진입 시 시작되어 FixedUpdate에서 루트 바디 위치/회전을 보간.
    class RagdollRootTransition
    {
        private readonly float _duration;

        private bool _isActive;
        private Vector3 _startPos;
        private Quaternion _startRot;
        private Vector3 _targetPos;
        private Quaternion _targetRot;
        private float _timer;

        public bool IsActive => _isActive;

        public RagdollRootTransition(float duration)
        {
            _duration = duration;
        }

        public void Begin(Rigidbody rootBody, Vector3 targetPos, Quaternion targetRot)
        {
            _startPos = rootBody.position;
            _startRot = rootBody.rotation;
            _targetPos = targetPos;
            _targetRot = targetRot;
            _timer = 0f;
            _isActive = true;
        }

        public void Tick(Rigidbody rootBody)
        {
            if (!_isActive) return;

            _timer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(_timer / _duration);
            float smoothT = t * t * (3f - 2f * t);

            rootBody.MovePosition(Vector3.Lerp(_startPos, _targetPos, smoothT));
            rootBody.MoveRotation(Quaternion.Slerp(_startRot, _targetRot, smoothT));

            if (t >= 1f)
                _isActive = false;
        }

        public void Cancel()
        {
            _isActive = false;
        }
    }
}
