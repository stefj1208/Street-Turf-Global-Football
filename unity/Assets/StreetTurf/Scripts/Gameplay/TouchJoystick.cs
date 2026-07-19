using UnityEngine;
using UnityEngine.EventSystems;

namespace StreetTurf.Gameplay
{
    public sealed class TouchJoystick : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField, Range(0f, 0.9f)] private float deadZone = 0.1f;
        [SerializeField, Range(0.1f, 1f)] private float handleRange = 0.75f;

        public Vector2 Value { get; private set; }

        public void Configure(RectTransform newBackground, RectTransform newHandle)
        {
            background = newBackground;
            handle = newHandle;
            Value = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateValue(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateValue(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Value = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }

        private void UpdateValue(PointerEventData eventData)
        {
            if (background == null || handle == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint
                ))
            {
                return;
            }

            Vector2 halfSize = background.rect.size * 0.5f;
            Vector2 normalized = new Vector2(
                halfSize.x > 0f ? localPoint.x / halfSize.x : 0f,
                halfSize.y > 0f ? localPoint.y / halfSize.y : 0f
            );
            normalized = Vector2.ClampMagnitude(normalized, 1f);

            if (normalized.magnitude < deadZone)
            {
                normalized = Vector2.zero;
            }

            Value = normalized;
            handle.anchoredPosition = new Vector2(
                normalized.x * halfSize.x * handleRange,
                normalized.y * halfSize.y * handleRange
            );
        }
    }
}
