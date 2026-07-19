using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace StreetTurf.TurfData
{
    /// <summary>
    /// Downloads the immutable Home Turf selected by the match server.
    /// Both Home and Away must load the same turfId before gameplay starts.
    /// </summary>
    public sealed class HomeTurfLoader : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://127.0.0.1:8000";
        [SerializeField] private Renderer environmentRenderer;
        [SerializeField] private string environmentTextureProperty = "_BaseMap";
        [SerializeField] private Transform goalA;
        [SerializeField] private Transform goalB;
        [SerializeField] private Transform boundaryRoot;
        [SerializeField, Min(0.05f)] private float boundaryThickness = 0.25f;
        [SerializeField, Min(0.5f)] private float boundaryHeight = 2.5f;
        [SerializeField, Min(5)] private int timeoutSeconds = 60;

        private Material runtimeEnvironmentMaterial;
        private Texture2D runtimeEnvironmentTexture;
        private readonly List<GameObject> generatedWalls = new List<GameObject>();

        public HomeTurfManifest LoadedManifest { get; private set; }

        public IEnumerator LoadTurf(
            string turfId,
            Action<HomeTurfManifest> onSuccess,
            Action<string> onError
        )
        {
            if (string.IsNullOrWhiteSpace(turfId))
            {
                onError?.Invoke("A turfId is required.");
                yield break;
            }

            string manifestUrl = $"{apiBaseUrl.TrimEnd('/')}/v1/home-turfs/{UnityWebRequest.EscapeURL(turfId)}";
            using UnityWebRequest manifestRequest = UnityWebRequest.Get(manifestUrl);
            manifestRequest.timeout = timeoutSeconds;
            yield return manifestRequest.SendWebRequest();

            if (manifestRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    $"Unable to load turf manifest ({manifestRequest.responseCode}): {manifestRequest.error}"
                );
                yield break;
            }

            HomeTurfManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<HomeTurfManifest>(
                    manifestRequest.downloadHandler.text
                );
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Invalid turf manifest: {exception.Message}");
                yield break;
            }

            if (manifest == null || manifest.schemaVersion != 1)
            {
                onError?.Invoke("Unsupported or missing turf schema version.");
                yield break;
            }

            using UnityWebRequest textureRequest = UnityWebRequest.Get(manifest.environmentUrl);
            textureRequest.timeout = timeoutSeconds;
            yield return textureRequest.SendWebRequest();

            if (textureRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(
                    $"Unable to load turf texture ({textureRequest.responseCode}): {textureRequest.error}"
                );
                yield break;
            }

            byte[] png = textureRequest.downloadHandler.data;
            string calculatedHash = CalculateSha256(png);
            if (!string.Equals(
                    calculatedHash,
                    manifest.environmentSha256,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                onError?.Invoke("The turf texture failed its SHA-256 integrity check.");
                yield break;
            }

            Texture2D loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!loadedTexture.LoadImage(png, true))
            {
                Destroy(loadedTexture);
                onError?.Invoke("The downloaded turf texture is not a valid image.");
                yield break;
            }

            ApplyManifest(manifest, loadedTexture);
            LoadedManifest = manifest;
            onSuccess?.Invoke(manifest);
        }

        private void ApplyManifest(HomeTurfManifest manifest, Texture2D texture)
        {
            if (runtimeEnvironmentTexture != null)
            {
                Destroy(runtimeEnvironmentTexture);
            }
            runtimeEnvironmentTexture = texture;

            if (environmentRenderer != null)
            {
                if (runtimeEnvironmentMaterial == null)
                {
                    runtimeEnvironmentMaterial = environmentRenderer.material;
                }

                if (runtimeEnvironmentMaterial.HasProperty(environmentTextureProperty))
                {
                    runtimeEnvironmentMaterial.SetTexture(environmentTextureProperty, texture);
                }
                else
                {
                    runtimeEnvironmentMaterial.mainTexture = texture;
                }
            }

            if (manifest.goals != null && manifest.goals.Length == 2)
            {
                ApplyGoal(goalA, manifest.goals[0]);
                ApplyGoal(goalB, manifest.goals[1]);
            }

            BuildBoundaryWalls(manifest.boundaries);
        }

        private static void ApplyGoal(Transform goal, GoalTransformData data)
        {
            if (goal == null || data == null || data.position == null || data.rotation == null)
            {
                return;
            }

            goal.SetPositionAndRotation(
                data.position.ToVector3(),
                data.rotation.ToQuaternion()
            );
        }

        private void BuildBoundaryWalls(Vector3Data[] points)
        {
            ClearBoundaryWalls();
            if (boundaryRoot == null || points == null || points.Length < 3)
            {
                return;
            }

            for (int index = 0; index < points.Length; index++)
            {
                Vector3 start = points[index].ToVector3();
                Vector3 end = points[(index + 1) % points.Length].ToVector3();
                Vector3 segment = end - start;
                float length = Vector3.ProjectOnPlane(segment, Vector3.up).magnitude;
                if (length < 0.05f)
                {
                    continue;
                }

                GameObject wall = new GameObject($"Boundary_{index:00}");
                wall.transform.SetParent(boundaryRoot, false);
                wall.transform.position = (start + end) * 0.5f + Vector3.up * (boundaryHeight * 0.5f);
                wall.transform.rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(segment, Vector3.up).normalized,
                    Vector3.up
                );

                BoxCollider collider = wall.AddComponent<BoxCollider>();
                collider.size = new Vector3(boundaryThickness, boundaryHeight, length);
                generatedWalls.Add(wall);
            }
        }

        private void ClearBoundaryWalls()
        {
            foreach (GameObject wall in generatedWalls)
            {
                if (wall != null)
                {
                    Destroy(wall);
                }
            }
            generatedWalls.Clear();
        }

        private static string CalculateSha256(byte[] data)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash = algorithm.ComputeHash(data);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2"));
            }
            return builder.ToString();
        }

        private void OnDestroy()
        {
            ClearBoundaryWalls();
            if (runtimeEnvironmentTexture != null)
            {
                Destroy(runtimeEnvironmentTexture);
            }
            if (runtimeEnvironmentMaterial != null)
            {
                Destroy(runtimeEnvironmentMaterial);
            }
        }
    }
}

