using DG.Tweening;
using UnityEngine;

public static class UIScalePopUtility
{
    public static Tween PlayUniform(
        Transform target,
        float duration,
        float targetUniformScale,
        float overshoot = 1.12f)
    {
        if (target == null)
            return null;

        Vector3 end = Vector3.one * targetUniformScale;
        target.localScale = Vector3.zero;

        return target
            .DOScale(end, duration)
            .SetEase(Ease.OutBack, overshoot)
            .SetUpdate(true)
            .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
    }
}
