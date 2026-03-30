using InGame.Player.Movement;
using UnityEngine;

/// <summary>
/// BodyStateCoordinator와 RubberBand 사이의 중재자입니다.
/// 분리 모드 진입/퇴장 시 고무줄의 생명주기와 타겟 바인딩을 관리합니다.
/// </summary>
public class RubberBandCoordinator : MonoBehaviour
{
    [SerializeField] private RubberBand _rubberBand;

    [Header("Anchor Points")]
    [Tooltip("AvatarA 쪽 고무줄 연결 위치 (로컬 오프셋 계산용)")]
    [SerializeField] private Transform _anchorA;

    [Tooltip("AvatarB 쪽 고무줄 연결 위치 (로컬 오프셋 계산용)")]
    [SerializeField] private Transform _anchorB;

    /// <summary>
    /// 분리 모드 진입 시 고무줄을 활성화하고 타겟을 바인딩합니다.
    /// 앵커의 로컬 오프셋을 물리 바디(RootBody) 기준으로 계산하여 전달합니다.
    /// </summary>
    public void Activate(GameObject avatarA, GameObject avatarB, bool isHost)
    {
        var bodyA = avatarA.GetComponentInChildren<IControllableBody>();
        var bodyB = avatarB.GetComponentInChildren<IControllableBody>();

        Transform physicsBodyA = bodyA.BodyTransform;
        Transform physicsBodyB = bodyB.BodyTransform;

        // 앵커의 월드 위치를 물리 바디 로컬 공간으로 변환하여 오프셋 계산
        Vector3 offsetA = physicsBodyA.InverseTransformPoint(_anchorA.position);
        Vector3 offsetB = physicsBodyB.InverseTransformPoint(_anchorB.position);

        Rigidbody rbA = isHost ? physicsBodyA.GetComponent<Rigidbody>() : null;
        Rigidbody rbB = isHost ? physicsBodyB.GetComponent<Rigidbody>() : null;

        _rubberBand.BindTargets(physicsBodyA, physicsBodyB, rbA, rbB, offsetA, offsetB);
        _rubberBand.SetAuthority(isHost);
        _rubberBand.ResetSimulator();
        _rubberBand.gameObject.SetActive(true);
    }

    /// <summary>
    /// 합체 모드 진입 시 고무줄을 비활성화합니다.
    /// </summary>
    public void Deactivate()
    {
        _rubberBand.gameObject.SetActive(false);
    }
}
