using InGame.Team;
using InGame.UserInput;
using UnityEngine;

namespace InGame.Player.Test
{
    /// <summary>
    /// 합동 모드 입력 시각화. Red(Host)/Blue(Guest) 화살표로 각 유저의 입력 방향을 표시.
    /// MergedBody 주위에 화살표가 나타나며, 입력이 있을 때 해당 방향을 가리킨다.
    /// 테스트용 - 이 파일 전체 삭제로 제거 가능.
    /// </summary>
    public class CoopInputVisualizer : MonoBehaviour
    {
        // ── 설정값 ──────────────────────────────────────────────
        private const float ArrowRadius = 1.5f;
        private const float ArrowYOffset = 1.2f;
        private const float IdleAlpha = 0.15f;
        private const float ActiveAlpha = 1.0f;
        private const float AlphaLerpSpeed = 10f;
        private const float JumpMarkerDuration = 0.4f;
        private const float OverlapAngleOffset = 15f;

        // ── 색상 ────────────────────────────────────────────────
        private static readonly Color HostColor = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color GuestColor = new Color(0.2f, 0.4f, 1f, 1f);

        // ── 참조 ────────────────────────────────────────────────
        private InputRouter _inputRouter;
        private PlayerFormController _playerFormController;
        private Transform _mergedBodyTransform;

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
        private Vector3 _lastHostDir = Vector3.forward;
        private Vector3 _lastGuestDir = Vector3.back;
        private bool _isActive;

        // ── Lifecycle ───────────────────────────────────────────

        private void Start()
        {
            _inputRouter = GetComponent<InputRouter>();
            _playerFormController = GetComponent<PlayerFormController>();

            if (_inputRouter == null || _playerFormController == null)
            {
                enabled = false;
                return;
            }

            // MergedBody Transform 캐싱
            GameObject mergedBody = _inputRouter.MergedBody;
            if (mergedBody != null)
                _mergedBodyTransform = mergedBody.transform;

            // 화살표 생성 (월드 공간에 독립 배치 — 부모 transform 영향 제거)
            _hostArrow = InputArrowBuilder.CreateArrow("HostArrow", HostColor, null);
            _guestArrow = InputArrowBuilder.CreateArrow("GuestArrow", GuestColor, null);
            _hostJumpMarker = InputArrowBuilder.CreateJumpIndicator("HostJump", HostColor, null);
            _guestJumpMarker = InputArrowBuilder.CreateJumpIndicator("GuestJump", GuestColor, null);

            _hostAlpha = IdleAlpha;
            _guestAlpha = IdleAlpha;

            _playerFormController.OnModeChanged += HandleModeChanged;

            _isActive = _inputRouter.CurrentMode == ETeamMode.Merged;
            SetVisualsActive(_isActive);
        }

        private void OnDestroy()
        {
            if (_playerFormController != null)
                _playerFormController.OnModeChanged -= HandleModeChanged;

            if (_hostArrow != null) Destroy(_hostArrow);
            if (_guestArrow != null) Destroy(_guestArrow);
            if (_hostJumpMarker != null) Destroy(_hostJumpMarker);
            if (_guestJumpMarker != null) Destroy(_guestJumpMarker);
        }

        // ── LateUpdate ─────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_isActive) return;

            // MergedBody root는 고정 — 실제 이동하는 RootBody(Rigidbody)를 추적
            if (_mergedBodyTransform == null)
            {
                GameObject mergedBody = _inputRouter.MergedBody;
                if (mergedBody == null) return;
                var rb = mergedBody.GetComponentInChildren<Rigidbody>();
                _mergedBodyTransform = rb != null ? rb.transform : mergedBody.transform;
            }
            if (!_mergedBodyTransform.gameObject.activeInHierarchy) return;

            // 1. InputRouter에서 라우팅된 입력 읽기 (A=Host, B=Guest)
            Vector3 hostInputDir = _inputRouter.RoutedDirA;
            Vector3 guestInputDir = _inputRouter.RoutedDirB;
            bool hostHasInput = hostInputDir.sqrMagnitude > 0.001f;
            bool guestHasInput = guestInputDir.sqrMagnitude > 0.001f;

            // 입력이 있으면 방향 업데이트, 없으면 마지막 방향 유지
            if (hostHasInput) _lastHostDir = hostInputDir.normalized;
            if (guestHasInput) _lastGuestDir = guestInputDir.normalized;

            // 점프 감지
            if (_inputRouter.RoutedJumpA) _hostJumpTimer = JumpMarkerDuration;
            if (_inputRouter.RoutedJumpB) _guestJumpTimer = JumpMarkerDuration;

            // 2. 겹침 방지
            Vector3 hostDir = _lastHostDir;
            Vector3 guestDir = _lastGuestDir;
            float angle = Vector3.Angle(hostDir, guestDir);
            if (angle < OverlapAngleOffset * 2f)
            {
                hostDir = Quaternion.Euler(0, -OverlapAngleOffset, 0) * hostDir;
                guestDir = Quaternion.Euler(0, OverlapAngleOffset, 0) * guestDir;
            }

            // 3. MergedBody 기준 위치
            Vector3 basePos = _mergedBodyTransform.position + Vector3.up * ArrowYOffset;

            UpdateArrow(_hostArrow, basePos, hostDir);
            UpdateArrow(_guestArrow, basePos, guestDir);

            // 4. 알파 (입력 시 밝게, 미입력 시 어둡게)
            float targetHostAlpha = hostHasInput ? ActiveAlpha : IdleAlpha;
            float targetGuestAlpha = guestHasInput ? ActiveAlpha : IdleAlpha;
            _hostAlpha = Mathf.Lerp(_hostAlpha, targetHostAlpha, Time.deltaTime * AlphaLerpSpeed);
            _guestAlpha = Mathf.Lerp(_guestAlpha, targetGuestAlpha, Time.deltaTime * AlphaLerpSpeed);

            ApplyAlpha(_hostArrow, HostColor, _hostAlpha);
            ApplyAlpha(_guestArrow, GuestColor, _guestAlpha);

            // 5. 점프 마커
            _hostJumpTimer -= Time.deltaTime;
            _guestJumpTimer -= Time.deltaTime;

            UpdateJumpMarker(_hostJumpMarker,
                _hostArrow.transform.position + Vector3.up * 0.4f, hostDir, _hostJumpTimer > 0);
            UpdateJumpMarker(_guestJumpMarker,
                _guestArrow.transform.position + Vector3.up * 0.4f, guestDir, _guestJumpTimer > 0);
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
            _mergedBodyTransform = null; // 모드 전환 시 다음 프레임에 재탐색
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
