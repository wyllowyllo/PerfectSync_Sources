namespace Core
{
    public static class ExecutionOrderConstants
    {
        // ── 초기화 (-10) ──
        public const int BodyStateCoordinator = -10;

        // ── 입력 (-5) ──
        public const int LocalPlayerInput = -5;

        // ── 래그돌 코어 (0~2) ── 상태 전이 → 본 적용 → 이동 순서 보장.
        public const int RagdollStateMachine = 0;
        public const int RagdollBoneReceiver = 1;
        public const int PlayerMovement = 2;
        public const int LaunchController = 3;

        // ── 플랫폼 동기화 → 운반 (3~4) ── PlatformCarrier 이전에 위치 갱신.
        public const int MovingPlatformSync = 3;
        public const int PlatformCarrier = 4;

        // ── 카메라 타겟 ──
        public const int CameraTargetProvider = 5;

        // ── 카메라 ──
        public const int CinemachineCameraManager = 6;

        // ── 후처리 (10+) ──
        public const int BodySimulationToggle = 10;
        public const int BodyMovementSynchronizer = 100;
        public const int RagdollBoneSynchronizer = 101;
        public const int RagdollStateNetworkBridge = 102;

        // ── 시각 효과 (110+) ── 네트워크 동기화 이후 실행.
        public const int SyncInputDisplay = 110;
    }
}
