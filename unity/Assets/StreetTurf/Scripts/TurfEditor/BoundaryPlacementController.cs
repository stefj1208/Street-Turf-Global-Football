using System.Collections.Generic;
using StreetTurf.TurfData;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StreetTurf.TurfEditor
{
    /// <summary>
    /// Lets the player tap points around the playable area.
    /// HomeTurfLoader turns the resulting closed polygon into invisible walls.
    /// Attach this component to the full-screen UI Image used during boundary editing.
    /// </summary>
    public sealed class BoundaryPlacementController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Camera editorCamera;
        [SerializeField] private LayerMask playableGroundLayers;
        [SerializeField] private LineRenderer previewLine;
        [SerializeField, Range(4, 32)] private int maximumPoints = 16;
        [SerializeField, Min(0.1f)] private float minimumPointSpacing = 0.5f;
        [SerializeField, Min(0f)] private float previewHeight = 0.04f;

        private readonly List<Vector3> points = new List<Vector3>();
        private bool isClosed;

        public bool IsClosed => isClosed;
        public int PointCount => points.Count;

        private void Awake()
        {
            if (editorCamera == null)
            {
                editorCamera = Camera.main;
            }
            RefreshPreview();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isClosed || points.Count >= maximumPoints || editorCamera == null)
            {
                return;
            }

            Ray ray = editorCamera.ScreenPointToRay(eventData.position);
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

            Vector3 candidate = hit.point;
            if (points.Count > 0
                && Vector3.Distance(points[points.Count - 1], candidate) < minimumPointSpacing)
            {
                return;
            }

            points.Add(candidate);
            RefreshPreview();
        }

        public void CloseBoundary()
        {
            if (points.Count < 4)
            {
                Debug.LogWarning("Place at least four boundary points before closing.", this);
                return;
            }

            isClosed = true;
            RefreshPreview();
        }

        public void UndoLastPoint()
        {
            if (isClosed)
            {
                isClosed = false;
            }
            else if (points.Count > 0)
            {
                points.RemoveAt(points.Count - 1);
            }
            RefreshPreview();
        }

        public void ClearBoundary()
        {
            points.Clear();
            isClosed = false;
            RefreshPreview();
        }

        public Vector3Data[] BuildBoundaryData()
        {
            if (!isClosed || points.Count < 4)
            {
                return new Vector3Data[0];
            }

            Vector3Data[] result = new Vector3Data[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                result[index] = new Vector3Data(points[index]);
            }
            return result;
        }

        private void RefreshPreview()
        {
            if (previewLine == null)
            {
                return;
            }

            int extraClosingPoint = isClosed && points.Count > 0 ? 1 : 0;
            previewLine.positionCount = points.Count + extraClosingPoint;
            for (int index = 0; index < points.Count; index++)
            {
                previewLine.SetPosition(index, points[index] + Vector3.up * previewHeight);
            }

            if (extraClosingPoint == 1)
            {
                previewLine.SetPosition(
                    points.Count,
                    points[0] + Vector3.up * previewHeight
                );
            }
        }
    }
}

