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

        // ── 카메라 (+5) ──
        public const int CinemachineCameraManager = 5;

        // ── 후처리 (10+) ──
        public const int BodySimulationToggle = 10;
        public const int BodyMovementSynchronizer = 100;
        public const int RagdollBoneSynchronizer = 101;
        public const int RagdollStateNetworkBridge = 102;
    }
}
