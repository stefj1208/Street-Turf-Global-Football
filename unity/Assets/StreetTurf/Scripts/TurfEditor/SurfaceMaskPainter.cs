using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StreetTurf.TurfEditor
{
    /// <summary>
    /// Paints a binary removal mask by raycasting a screen pointer onto a UV-mapped mesh.
    /// Attach this component to a full-screen transparent UI Image.
    /// White pixels are editable by the inpainting server; black pixels are protected.
    /// </summary>
    public sealed class SurfaceMaskPainter : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [Header("Scene references")]
        [SerializeField] private Camera editorCamera;
        [SerializeField] private Renderer surfaceRenderer;
        [SerializeField] private MeshCollider surfaceCollider;
        [SerializeField] private LayerMask paintableLayers = ~0;

        [Header("Mask")]
        [SerializeField] private string baseTextureShaderProperty = "_BaseMap";
        [SerializeField] private Vector2Int fallbackResolution = new Vector2Int(1024, 1024);
        [SerializeField, Range(256, 4096)] private int maximumTextureSize = 2048;
        [SerializeField, Range(2, 256)] private int brushRadiusPixels = 24;
        [SerializeField] private string maskShaderProperty = "_MaskTex";

        private static readonly Color32 ProtectedPixel = new Color32(0, 0, 0, 255);
        private static readonly Color32 EditablePixel = new Color32(255, 255, 255, 255);

        private Texture2D maskTexture;
        private Color32[] maskPixels;
        private Material runtimeMaterial;
        private bool textureIsDirty;
        private bool hasPreviousUv;
        private Vector2 previousUv;
        private int activePointerId = int.MinValue;
        private int maskWidth;
        private int maskHeight;

        public Texture2D MaskTexture => maskTexture;

        private void Awake()
        {
            InitializeMask();
        }

        private void LateUpdate()
        {
            ApplyPendingPixels();
        }

        public void InitializeMask()
        {
            if (editorCamera == null)
            {
                editorCamera = Camera.main;
            }

            if (editorCamera == null || surfaceRenderer == null || surfaceCollider == null)
            {
                Debug.LogError(
                    "SurfaceMaskPainter requires a Camera, Renderer and MeshCollider.",
                    this
                );
                enabled = false;
                return;
            }

            if (maskTexture != null)
            {
                Destroy(maskTexture);
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            runtimeMaterial = surfaceRenderer.material;
            CalculateMaskResolution(runtimeMaterial, out maskWidth, out maskHeight);
            maskTexture = new Texture2D(
                maskWidth,
                maskHeight,
                TextureFormat.RGBA32,
                false,
                true
            )
            {
                name = "StreetTurf_RemovalMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            maskPixels = new Color32[maskWidth * maskHeight];
            FillMask(ProtectedPixel);

            if (!runtimeMaterial.HasProperty(maskShaderProperty))
            {
                Debug.LogError(
                    $"The surface material must use a shader containing {maskShaderProperty}.",
                    this
                );
                enabled = false;
                return;
            }

            runtimeMaterial.SetTexture(maskShaderProperty, maskTexture);
            ApplyPendingPixels();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!enabled || activePointerId != int.MinValue)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            hasPreviousUv = false;
            PaintFromScreenPoint(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!enabled || eventData.pointerId != activePointerId)
            {
                return;
            }

            PaintFromScreenPoint(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                EndStroke();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                EndStroke();
            }
        }

        public void SetBrushRadiusPixels(float radius)
        {
            brushRadiusPixels = Mathf.Clamp(Mathf.RoundToInt(radius), 2, 256);
        }

        public void ClearMask()
        {
            if (maskPixels == null)
            {
                return;
            }

            FillMask(ProtectedPixel);
            ApplyPendingPixels();
        }

        public byte[] ExportMaskPng()
        {
            if (maskTexture == null)
            {
                throw new InvalidOperationException("The mask has not been initialized.");
            }

            ApplyPendingPixels();
            return maskTexture.EncodeToPNG();
        }

        public string SaveMaskForDebugging()
        {
            string path = Path.Combine(
                Application.persistentDataPath,
                $"street-turf-mask-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png"
            );
            File.WriteAllBytes(path, ExportMaskPng());
            Debug.Log($"Mask saved to {path}", this);
            return path;
        }

        private void PaintFromScreenPoint(Vector2 screenPoint)
        {
            Ray ray = editorCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    Mathf.Infinity,
                    paintableLayers,
                    QueryTriggerInteraction.Ignore
                ))
            {
                hasPreviousUv = false;
                return;
            }

            if (hit.collider != surfaceCollider)
            {
                hasPreviousUv = false;
                return;
            }

            Vector2 uv = hit.textureCoord;
            if (hasPreviousUv && Vector2.Distance(previousUv, uv) < 0.25f)
            {
                PaintLine(previousUv, uv);
            }
            else
            {
                PaintCircle(uv);
            }

            previousUv = uv;
            hasPreviousUv = true;
        }

        private void PaintLine(Vector2 fromUv, Vector2 toUv)
        {
            Vector2 pixelDelta = new Vector2(
                (toUv.x - fromUv.x) * maskWidth,
                (toUv.y - fromUv.y) * maskHeight
            );
            float distanceInPixels = pixelDelta.magnitude;
            float stepLength = Mathf.Max(1f, brushRadiusPixels * 0.4f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distanceInPixels / stepLength));

            for (int index = 0; index <= steps; index++)
            {
                float t = index / (float)steps;
                PaintCircle(Vector2.Lerp(fromUv, toUv, t));
            }
        }

        private void PaintCircle(Vector2 uv)
        {
            int centerX = Mathf.RoundToInt(uv.x * (maskWidth - 1));
            int centerY = Mathf.RoundToInt(uv.y * (maskHeight - 1));
            int radiusSquared = brushRadiusPixels * brushRadiusPixels;

            int minX = Mathf.Max(0, centerX - brushRadiusPixels);
            int maxX = Mathf.Min(maskWidth - 1, centerX + brushRadiusPixels);
            int minY = Mathf.Max(0, centerY - brushRadiusPixels);
            int maxY = Mathf.Min(maskHeight - 1, centerY + brushRadiusPixels);

            for (int y = minY; y <= maxY; y++)
            {
                int deltaY = y - centerY;
                int rowStart = y * maskWidth;
                for (int x = minX; x <= maxX; x++)
                {
                    int deltaX = x - centerX;
                    if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                    {
                        maskPixels[rowStart + x] = EditablePixel;
                    }
                }
            }

            textureIsDirty = true;
        }

        private void CalculateMaskResolution(
            Material material,
            out int calculatedWidth,
            out int calculatedHeight
        )
        {
            int sourceWidth = Mathf.Max(64, fallbackResolution.x);
            int sourceHeight = Mathf.Max(64, fallbackResolution.y);

            if (material.HasProperty(baseTextureShaderProperty))
            {
                Texture baseTexture = material.GetTexture(baseTextureShaderProperty);
                if (baseTexture != null)
                {
                    sourceWidth = baseTexture.width;
                    sourceHeight = baseTexture.height;
                }
            }

            float scale = Mathf.Min(
                1f,
                maximumTextureSize / (float)Mathf.Max(sourceWidth, sourceHeight)
            );
            calculatedWidth = Mathf.Max(64, Mathf.RoundToInt(sourceWidth * scale));
            calculatedHeight = Mathf.Max(64, Mathf.RoundToInt(sourceHeight * scale));
        }

        private void FillMask(Color32 color)
        {
            for (int index = 0; index < maskPixels.Length; index++)
            {
                maskPixels[index] = color;
            }

            textureIsDirty = true;
        }

        private void ApplyPendingPixels()
        {
            if (!textureIsDirty || maskTexture == null || maskPixels == null)
            {
                return;
            }

            maskTexture.SetPixels32(maskPixels);
            maskTexture.Apply(false, false);
            textureIsDirty = false;
        }

        private void EndStroke()
        {
            activePointerId = int.MinValue;
            hasPreviousUv = false;
            ApplyPendingPixels();
        }

        private void OnDestroy()
        {
            if (maskTexture != null)
            {
                Destroy(maskTexture);
            }

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }
    }
}
