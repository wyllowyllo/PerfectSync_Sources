using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 시 지정된 배경 프레임 이미지의 색상을 누르는 동안 변경합니다.
/// 버튼이 비활성화 상태이면 프레임을 비활성화 색상으로 변경합니다.
/// Button 컴포넌트가 있는 GameObject에 추가하세요.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonFrameHighlight : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Frame")]
    [SerializeField] private Image _frameImage;

    [Header("Colors")]
    [SerializeField] private Color _pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color _disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private Button _button;
    private Color _originalColor;
    private bool _lastInteractable;

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (_frameImage != null)
            _originalColor = _frameImage.color;
    }

    private void OnEnable()
    {
        _lastInteractable = _button != null && _button.interactable;
        ApplyFrameColor();
    }

    private void Update()
    {
        if (_button == null || _frameImage == null) return;

        bool interactable = _button.interactable;
        if (_lastInteractable == interactable) return;

        _lastInteractable = interactable;
        ApplyFrameColor();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_frameImage == null) return;
        if (_button != null && !_button.interactable) return;

        _frameImage.color = _pressedColor;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_frameImage == null) return;

        ApplyFrameColor();
    }

    private void OnDisable()
    {
        if (_frameImage != null)
            _frameImage.color = _originalColor;
    }

    private void ApplyFrameColor()
    {
        if (_frameImage == null) return;

        bool interactable = _button != null && _button.interactable;
        _frameImage.color = interactable ? _originalColor : _disabledColor;
    }
}
