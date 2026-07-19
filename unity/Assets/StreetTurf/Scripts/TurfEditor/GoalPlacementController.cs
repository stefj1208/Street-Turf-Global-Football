using StreetTurf.TurfData;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StreetTurf.TurfEditor
{
    /// <summary>
    /// Moves one of two goal prefabs over a collider-backed playable ground.
    /// Attach to the full-screen UI Image used during the goal-placement step.
    /// </summary>
    public sealed class GoalPlacementController : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        [SerializeField] private Camera editorCamera;
        [SerializeField] private LayerMask playableGroundLayers;
        [SerializeField] private Transform goalA;
        [SerializeField] private Transform goalB;
        [SerializeField, Min(0f)] private float groundOffset = 0f;

        private Transform selectedGoal;
        private int activePointerId = int.MinValue;

        private void Awake()
        {
            if (editorCamera == null)
            {
                editorCamera = Camera.main;
            }
        }

        public void SelectGoalA()
        {
            selectedGoal = goalA;
        }

        public void SelectGoalB()
        {
            selectedGoal = goalB;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectedGoal == null || activePointerId != int.MinValue)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            MoveSelectedGoal(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                MoveSelectedGoal(eventData.position);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                activePointerId = int.MinValue;
                FaceGoalsTowardEachOther();
            }
        }

        public GoalTransformData[] BuildGoalData()
        {
            if (goalA == null || goalB == null)
            {
                return new GoalTransformData[0];
            }

            return new[]
            {
                new GoalTransformData(goalA),
                new GoalTransformData(goalB)
            };
        }

        private void MoveSelectedGoal(Vector2 screenPosition)
        {
            if (editorCamera == null || selectedGoal == null)
            {
                return;
            }

            Ray ray = editorCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    Mathf.Infinity,
                    playableGroundLayers,
                    QueryTriggerInteraction.Ignore
                ))
            {
                return;
            }

            selectedGoal.position = hit.point + hit.normal * groundOffset;
            FaceGoalsTowardEachOther();
        }

        private void FaceGoalsTowardEachOther()
        {
            if (goalA == null || goalB == null)
            {
                return;
            }

            Vector3 fromAToB = Vector3.ProjectOnPlane(goalB.position - goalA.position, Vector3.up);
            if (fromAToB.sqrMagnitude < 0.01f)
            {
                return;
            }

            goalA.rotation = Quaternion.LookRotation(fromAToB.normalized, Vector3.up);
            goalB.rotation = Quaternion.LookRotation(-fromAToB.normalized, Vector3.up);
        }
    }
}

