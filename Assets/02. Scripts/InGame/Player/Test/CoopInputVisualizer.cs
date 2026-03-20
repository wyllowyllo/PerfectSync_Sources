using InGame.Team._02._Domain;
using InGame.UserInput;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// [InputViz] 합동 모드 입력 시각화. Red(Host)/Blue(Guest) 화살표로 카메라 방향과 입력 상태 표시.
    /// 테스트용 - 이 파일 전체 삭제로 제거 가능.
    /// </summary>
    public class CoopInputVisualizer : MonoBehaviour
    {
        // ── 설정값 ──────────────────────────────────────────────
        private const float ArrowRadius = 1.5f;
        private const float ArrowYOffset = 1.2f;
        private const float IdleAlpha = 0.25f;
        private const float ActiveAlpha = 1.0f;
        private const float AlphaLerpSpeed = 8f;
        private const float JumpMarkerDuration = 0.4f;
        private const float OverlapAngleOffset = 10f;

        // ── 색상 ────────────────────────────────────────────────
        private static readonly Color HostColor = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color GuestColor = new Color(0.2f, 0.4f, 1f, 1f);

        // ── 참조 ────────────────────────────────────────────────
        private InputRouter _inputRouter;
        private RemotePlayerInput _remotePlayerInput;
        private PlayerFormController _playerFormController;

        // ── 화살표 오브젝트 ─────────────────────────────────────
        private GameObject _hostArrow;
        private GameObject _guestArrow;
        private GameObject _hostJumpMarker;
        private GameObject _guestJumpMarker;

        // ── 상태 ────────────────────────────────────────────────
        private float _hostAlpha;
        private float _guestAlpha;
        private float _hostJumpTimer;
        private float _guestJumpTimer;
        private bool _hostHasInput;
        private bool _guestHasInput;
        private bool _isActive;

        // ── Lifecycle ───────────────────────────────────────────

        private void Start()
        {
            _inputRouter = GetComponent<InputRouter>();
            _remotePlayerInput = GetComponent<RemotePlayerInput>();
            _playerFormController = GetComponent<PlayerFormController>();

            if (_inputRouter == null || _remotePlayerInput == null || _playerFormController == null)
            {
                enabled = false;
                return;
            }

            // 화살표 생성
            _hostArrow = InputArrowBuilder.CreateArrow("HostArrow", HostColor, transform);
            _guestArrow = InputArrowBuilder.CreateArrow("GuestArrow", GuestColor, transform);
            _hostJumpMarker = InputArrowBuilder.CreateJumpIndicator("HostJump", HostColor, transform);
            _guestJumpMarker = InputArrowBuilder.CreateJumpIndicator("GuestJump", GuestColor, transform);

            // 초기 알파
            _hostAlpha = IdleAlpha;
            _guestAlpha = IdleAlpha;

            // 모드 변경 구독
            _playerFormController.OnModeChanged += HandleModeChanged;

            // 초기 상태: Merged면 활성
            _isActive = _inputRouter.CurrentMode == ETeamMode.Merged;
            SetVisualsActive(_isActive);
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;
        }

        // ── Public API (InputRouter에서 호출) ───────────────────

        public void UpdateInputState(Vector3 dirA, Vector3 dirB, bool jumpA, bool jumpB)
        {
            if (!_isActive) return;

            // A = Host 입력, B = Guest 입력
            _hostHasInput = dirA.sqrMagnitude > 0.001f;
            _guestHasInput = dirB.sqrMagnitude > 0.001f;

            if (jumpA) _hostJumpTimer = JumpMarkerDuration;
            if (jumpB) _guestJumpTimer = JumpMarkerDuration;
        }

        // ── LateUpdate ─────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_isActive) return;

            GameObject mergedBody = _inputRouter.MergedBody;
            if (mergedBody == null || !mergedBody.activeInHierarchy) return;

            // 1. 카메라 forward 방향 획득
            Vector3 localCamFwd = _inputRouter.LocalCameraForwardXZ;
            Vector3 remoteCamFwd = _remotePlayerInput.CameraForwardXZ;

            // forward가 0이면 기본값
            if (localCamFwd.sqrMagnitude < 0.001f) localCamFwd = Vector3.forward;
            if (remoteCamFwd.sqrMagnitude < 0.001f) remoteCamFwd = Vector3.forward;

            // 2. Host/Guest 배정
            Vector3 hostFwd, guestFwd;
            if (_inputRouter.IsHost)
            {
                hostFwd = localCamFwd;
                guestFwd = remoteCamFwd;
            }
            else
            {
                hostFwd = remoteCamFwd;
                guestFwd = localCamFwd;
            }

            // 3. 겹침 방지: 두 방향이 거의 같으면 ±오프셋
            float angle = Vector3.Angle(hostFwd, guestFwd);
            if (angle < OverlapAngleOffset * 2f)
            {
                hostFwd = Quaternion.Euler(0, -OverlapAngleOffset, 0) * hostFwd;
                guestFwd = Quaternion.Euler(0, OverlapAngleOffset, 0) * guestFwd;
            }

            // 4. 화살표 위치/회전
            Vector3 basePos = mergedBody.transform.position + Vector3.up * ArrowYOffset;

            UpdateArrow(_hostArrow, basePos, hostFwd);
            UpdateArrow(_guestArrow, basePos, guestFwd);

            // 5. 알파 lerp
            float targetHostAlpha = _hostHasInput ? ActiveAlpha : IdleAlpha;
            float targetGuestAlpha = _guestHasInput ? ActiveAlpha : IdleAlpha;
            _hostAlpha = Mathf.Lerp(_hostAlpha, targetHostAlpha, Time.deltaTime * AlphaLerpSpeed);
            _guestAlpha = Mathf.Lerp(_guestAlpha, targetGuestAlpha, Time.deltaTime * AlphaLerpSpeed);

            ApplyAlpha(_hostArrow, HostColor, _hostAlpha);
            ApplyAlpha(_guestArrow, GuestColor, _guestAlpha);

            // 6. 점프 마커
            _hostJumpTimer -= Time.deltaTime;
            _guestJumpTimer -= Time.deltaTime;

            UpdateJumpMarker(_hostJumpMarker, _hostArrow.transform.position + Vector3.up * 0.4f,
                hostFwd, _hostJumpTimer > 0);
            UpdateJumpMarker(_guestJumpMarker, _guestArrow.transform.position + Vector3.up * 0.4f,
                guestFwd, _guestJumpTimer > 0);
        }

        // ── Helper ──────────────────────────────────────────────

        private void UpdateArrow(GameObject arrow, Vector3 basePos, Vector3 forward)
        {
            arrow.transform.position = basePos + forward * ArrowRadius;
            arrow.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private void ApplyAlpha(GameObject arrow, Color baseColor, float alpha)
        {
            var lr = arrow.GetComponent<LineRenderer>();
            if (lr == null) return;
            Color c = baseColor;
            c.a = alpha;
            lr.startColor = c;
            lr.endColor = c;
        }

        private void UpdateJumpMarker(GameObject marker, Vector3 position, Vector3 forward, bool show)
        {
            marker.SetActive(show);
            if (show)
            {
                marker.transform.position = position;
                marker.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        private void HandleModeChanged(ETeamMode newMode)
        {
            _isActive = newMode == ETeamMode.Merged;
            SetVisualsActive(_isActive);
        }

        private void SetVisualsActive(bool active)
        {
            if (_hostArrow != null) _hostArrow.SetActive(active);
            if (_guestArrow != null) _guestArrow.SetActive(active);
            if (_hostJumpMarker != null) _hostJumpMarker.SetActive(false);
            if (_guestJumpMarker != null) _guestJumpMarker.SetActive(false);
        }
    }
}
