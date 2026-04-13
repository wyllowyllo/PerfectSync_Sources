using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 시 지정된 배경 프레임 이미지의 색상을 누르는 동안 변경합니다.
/// Button 컴포넌트가 있는 GameObject에 추가하세요.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonFrameHighlight : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Frame")]
    [SerializeField] private Image _frameImage;

    [Header("Highlight")]
    [SerializeField] private Color _pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private Color _originalColor;

    private void Awake()
    {
        if (_frameImage != null)
            _originalColor = _frameImage.color;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_frameImage != null)
            _frameImage.color = _pressedColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_frameImage != null)
            _frameImage.color = _originalColor;
    }

    private void OnDisable()
    {
        if (_frameImage != null)
            _frameImage.color = _originalColor;
    }
}
