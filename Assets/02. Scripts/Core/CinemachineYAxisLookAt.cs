using UnityEngine;
using Unity.Cinemachine;

// NOTE : 시네머신 특정 대상을 Y축 기준으로만 바라보게 하는 익스텐션
[AddComponentMenu("Cinemachine/Extension/Y Axis Look At")]
[ExecuteInEditMode]
public class CinemachineYAxisLookAt : CinemachineExtension
{
    [SerializeField] private Transform _target;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;
        if (_target == null) return;

        Vector3 direction = _target.position - state.GetFinalPosition();
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            state.RawOrientation = Quaternion.LookRotation(direction);
        }
    }
}
