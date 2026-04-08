using UnityEngine;

public abstract class LobbyPopupBase : MonoBehaviour
{
    public GameObject Root => gameObject;

    public bool IsShowing => gameObject.activeInHierarchy;

    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }
}
