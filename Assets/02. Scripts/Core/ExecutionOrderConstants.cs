namespace Core
{
    public static class ExecutionOrderConstants
    {
        // ── 초기화 (-10) ──
        public const int BodyStateCoordinator = -10;

        // ── 카메라 (+5) ── PoseTransfer(0) 이후 visual pelvis 읽기 보장.
        public const int CinemachineCameraManager = 5;

        // ── 입력 (-5) ──
        public const int LocalPlayerInput = -5;

        // ── 물리 (0, 암묵) ──
        // InputRouter, PlayerMovement 등은 기본값(0) 사용.

        // ── 후처리 (10+) ──
        public const int BodySimulationToggle = 10;
        public const int BodyMovementSynchronizer = 100;
        public const int RagdollBoneSynchronizer = 101;
        public const int RagdollStateNetworkBridge = 102;
    }
}
