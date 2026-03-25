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
    [Tooltip("AvatarA 쪽 고무줄 연결 위치")]
    [SerializeField] private Transform _anchorA;

    [Tooltip("AvatarB 쪽 고무줄 연결 위치")]
    [SerializeField] private Transform _anchorB;

    /// <summary>
    /// 분리 모드 진입 시 고무줄을 활성화하고 타겟을 바인딩합니다.
    /// </summary>
    public void Activate(GameObject avatarA, GameObject avatarB, bool isHost)
    {
        var bodyA = avatarA.GetComponentInChildren<IControllableBody>();
        var bodyB = avatarB.GetComponentInChildren<IControllableBody>();

        Rigidbody rbA = isHost ? bodyA.BodyTransform.GetComponent<Rigidbody>() : null;
        Rigidbody rbB = isHost ? bodyB.BodyTransform.GetComponent<Rigidbody>() : null;

        _rubberBand.BindTargets(_anchorA, _anchorB, rbA, rbB);
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
