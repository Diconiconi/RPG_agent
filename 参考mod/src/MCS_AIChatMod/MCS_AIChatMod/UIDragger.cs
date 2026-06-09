using UnityEngine;
using UnityEngine.EventSystems;

namespace MCS_AIChatMod;

public class UIDragger : MonoBehaviour, IDragHandler, IEventSystemHandler, IBeginDragHandler
{
	public RectTransform target;

	private Vector2 _offset;

	public void OnBeginDrag(PointerEventData eventData)
	{
		RectTransformUtility.ScreenPointToLocalPointInRectangle(target.parent as RectTransform, eventData.position, eventData.pressEventCamera, out _offset);
		_offset = target.anchoredPosition - _offset;
	}

	public void OnDrag(PointerEventData eventData)
	{
		RectTransformUtility.ScreenPointToLocalPointInRectangle(target.parent as RectTransform, eventData.position, eventData.pressEventCamera, out var localPoint);
		target.anchoredPosition = localPoint + _offset;
	}
}
