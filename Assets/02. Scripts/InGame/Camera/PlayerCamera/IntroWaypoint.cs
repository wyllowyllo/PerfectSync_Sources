using UnityEngine;

namespace InGame.Camera.PlayerCamera
{
    public class IntroWaypoint : MonoBehaviour
    {
        [HideInInspector] public int index;

        private void OnDrawGizmos()
        {
            // 카메라 위치 (하늘색 구체).
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Gizmos.DrawSphere(transform.position, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 1.2f);

            // LookAt 자식.
            var lookAt = transform.Find("LookAt");
            if (lookAt != null)
            {
                Vector3 lookPos = lookAt.position;

                // 시선 지점 (주황색 구체).
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
                Gizmos.DrawSphere(lookPos, 0.5f);

                // 시선 방향 선.
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.4f);
                Gizmos.DrawLine(transform.position, lookPos);
            }

            // 다음 웨이포인트와 연결선.
            if (transform.parent == null) return;

            int nextIndex = transform.GetSiblingIndex() + 1;
            if (nextIndex < transform.parent.childCount)
            {
                var next = transform.parent.GetChild(nextIndex);
                if (next.GetComponent<IntroWaypoint>() != null)
                {
                    Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
                    Gizmos.DrawLine(transform.position, next.position);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 선택 시 더 밝게.
            Gizmos.color = new Color(0.4f, 1f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, 1.5f);

            var lookAt = transform.Find("LookAt");
            if (lookAt != null)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.4f, 1f);
                Gizmos.DrawWireSphere(lookAt.position, 0.8f);
                Gizmos.DrawLine(transform.position, lookAt.position);
            }
        }
    }
}
