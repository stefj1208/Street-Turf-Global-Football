using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace StreetTurf.Gameplay
{
    public sealed class TouchActionButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private UnityEvent onPressed = new UnityEvent();
        [SerializeField] private UnityEvent onReleased = new UnityEvent();

        public void AddPressedListener(UnityAction listener)
        {
            onPressed.AddListener(listener);
        }

        public void AddReleasedListener(UnityAction listener)
        {
            onReleased.AddListener(listener);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            onPressed.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            onReleased.Invoke();
        }
    }
}
