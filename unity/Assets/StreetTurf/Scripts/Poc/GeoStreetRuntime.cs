using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace StreetTurf.Poc
{
    [Serializable]
    public sealed class GeoStreetData
    {
        public string displayName = string.Empty;
        public double latitude;
        public double longitude;
        public OsmElement[] elements = Array.Empty<OsmElement>();
    }

    [Serializable]
    public sealed class OsmElement
    {
        public string type = string.Empty;
        public long id;
        public OsmTags tags = new OsmTags();
        public OsmGeometryPoint[] geometry = Array.Empty<OsmGeometryPoint>();
    }

    [Serializable]
    public sealed class OsmTags
    {
        public string highway = string.Empty;
        public string building = string.Empty;
        public string buildingLevels = string.Empty;
        public string name = string.Empty;
        public string lanes = string.Empty;
        public string width = string.Empty;
    }

    [Serializable]
    public sealed class OsmGeometryPoint
    {
        public double lat;
        public double lon;
    }

    [Serializable]
    internal sealed class NominatimEnvelope
    {
        public NominatimPlace[] items = Array.Empty<NominatimPlace>();
    }

    [Serializable]
    internal sealed class NominatimPlace
    {
        public string lat = string.Empty;
        public string lon = string.Empty;
        public string display_name = string.Empty;
    }

    [Serializable]
    internal sealed class OverpassEnvelope
    {
        public OsmElement[] elements = Array.Empty<OsmElement>();
    }

    public sealed class GeolocatedStreetService : MonoBehaviour
    {
        private const string NominatimEndpoint = "https://nominatim.openstreetmap.org/search";
        private const string OverpassEndpoint = "https://overpass-api.de/api/interpreter";
        private const string UserAgent = "StreetTurfGlobalFootball/0.2 (github.com/stefj1208/Street-Turf-Global-Football)";

        public IEnumerator ResolveAddress(
            string address,
            Action<string> onProgress,
            Action<GeoStreetData> onSuccess,
            Action<string> onError
        )
        {
            onProgress?.Invoke("Recherche de l'adresse dans OpenStreetMap…");
            string searchUrl = NominatimEndpoint
                + "?format=jsonv2&limit=1&addressdetails=1&q="
                + UnityWebRequest.EscapeURL(address);

            using UnityWebRequest request = UnityWebRequest.Get(searchUrl);
            ConfigureRequest(request, 20);
            request.SetRequestHeader("Accept-Language", "fr");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke("La recherche OpenStreetMap a échoué : " + request.error);
                yield break;
            }

            NominatimEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<NominatimEnvelope>(
                    "{\"items\":" + request.downloadHandler.text + "}"
                );
            }
            catch (Exception exception)
            {
                onError?.Invoke("Réponse de géocodage invalide : " + exception.Message);
                yield break;
            }

            if (envelope == null || envelope.items == null || envelope.items.Length == 0)
            {
                onError?.Invoke("Adresse introuvable dans OpenStreetMap. Essaie avec la ville et le pays.");
                yield break;
            }

            NominatimPlace place = envelope.items[0];
            if (!double.TryParse(place.lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude)
                || !double.TryParse(place.lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
            {
                onError?.Invoke("Les coordonnées reçues sont invalides.");
                yield break;
            }

            yield return LoadCoordinates(
                latitude,
                longitude,
                place.display_name,
                onProgress,
                onSuccess,
                onError
            );
        }

        public IEnumerator LoadCoordinates(
            double latitude,
            double longitude,
            string displayName,
            Action<string> onProgress,
            Action<GeoStreetData> onSuccess,
            Action<string> onError
        )
        {
            onProgress?.Invoke("Téléchargement des routes et bâtiments OSM…");
            string latitudeText = latitude.ToString("F7", CultureInfo.InvariantCulture);
            string longitudeText = longitude.ToString("F7", CultureInfo.InvariantCulture);
            string query = "[out:json][timeout:25];("
                + "way(around:90," + latitudeText + "," + longitudeText + ")[\"highway\"];"
                + "way(around:90," + latitudeText + "," + longitudeText + ")[\"building\"];"
                + ");out tags geom;";

            WWWForm form = new WWWForm();
            form.AddField("data", query);
            using UnityWebRequest request = UnityWebRequest.Post(OverpassEndpoint, form);
            ConfigureRequest(request, 35);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke("Le téléchargement de la rue OSM a échoué : " + request.error);
                yield break;
            }

            OverpassEnvelope envelope;
            try
            {
                string normalizedJson = request.downloadHandler.text.Replace(
                    "\"building:levels\"",
                    "\"buildingLevels\""
                );
                envelope = JsonUtility.FromJson<OverpassEnvelope>(normalizedJson);
            }
            catch (Exception exception)
            {
                onError?.Invoke("La géométrie OpenStreetMap est invalide : " + exception.Message);
                yield break;
            }

            if (envelope == null || envelope.elements == null)
            {
                onError?.Invoke("Aucune géométrie OpenStreetMap n'a été reçue.");
                yield break;
            }

            int roadCount = 0;
            foreach (OsmElement element in envelope.elements)
            {
                if (element != null
                    && element.tags != null
                    && !string.IsNullOrEmpty(element.tags.highway)
                    && element.geometry != null
                    && element.geometry.Length >= 2)
                {
                    roadCount++;
                }
            }
            if (roadCount == 0)
            {
                onError?.Invoke("Aucune rue exploitable n'a été trouvée à cette adresse.");
                yield break;
            }

            onSuccess?.Invoke(new GeoStreetData
            {
                displayName = string.IsNullOrWhiteSpace(displayName) ? "Home Turf OSM" : displayName,
                latitude = latitude,
                longitude = longitude,
                elements = envelope.elements,
            });
        }

        private static void ConfigureRequest(UnityWebRequest request, int timeoutSeconds)
        {
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("User-Agent", UserAgent);
            request.SetRequestHeader("Accept", "application/json");
        }
    }

    public sealed class GeoStreetGenerator : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
        private Transform generatedRoot;
        private double latitudeOrigin;
        private double longitudeOrigin;
        private double metersPerLongitude;
        private Vector2 streetCenter;
        private float streetHeading;

        public PhysicsMaterial RoadPhysicsMaterial { get; private set; }

        public void Generate(GeoStreetData data)
        {
            if (data == null || data.elements == null)
            {
                throw new InvalidOperationException("Les données de rue géolocalisées sont absentes.");
            }

            generatedRoot = new GameObject("GeneratedStreet").transform;
            generatedRoot.SetParent(transform, false);
            latitudeOrigin = data.latitude;
            longitudeOrigin = data.longitude;
            metersPerLongitude = 111320d * Math.Cos(data.latitude * Math.PI / 180d);
            FindStreetFrame(data.elements);

            Material groundMaterial = CreateMaterial("OSM_Ground", new Color(0.2f, 0.27f, 0.22f));
            Material asphaltMaterial = CreateMaterial("OSM_Asphalt", new Color(0.16f, 0.18f, 0.2f));
            Material courtMaterial = CreateMaterial("Playable_Asphalt", new Color(0.24f, 0.25f, 0.27f));
            Material sidewalkMaterial = CreateMaterial("OSM_Sidewalk", new Color(0.48f, 0.49f, 0.5f));
            Material buildingMaterial = CreateMaterial("OSM_Building", new Color(0.54f, 0.34f, 0.29f));
            Material goalMaterial = CreateMaterial("Goal_White", Color.white);
            Material markingMaterial = CreateMaterial("Court_Marking", new Color(0.92f, 0.88f, 0.52f));

            CreateBox("GeoGround", new Vector3(0f, -0.18f, 0f), new Vector3(150f, 0.3f, 150f), groundMaterial, true);
            BuildRoadNetwork(data.elements, asphaltMaterial);
            BuildBuildings(data.elements, buildingMaterial);
            CreatePlayableCourt(courtMaterial, sidewalkMaterial, markingMaterial);
            CreateBoundaryColliders();
            CreateGoal("Goal_North", new Vector3(0f, 0f, 15.4f), false, 0, goalMaterial);
            CreateGoal("Goal_South", new Vector3(0f, 0f, -15.4f), true, 1, goalMaterial);

            RoadPhysicsMaterial = new PhysicsMaterial("GeolocatedAsphaltPhysics")
            {
                dynamicFriction = 0.64f,
                staticFriction = 0.72f,
                bounciness = 0.32f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };
            runtimeAssets.Add(RoadPhysicsMaterial);
            Transform playableRoad = generatedRoot.Find("PlayableRoad");
            if (playableRoad != null)
            {
                playableRoad.GetComponent<BoxCollider>().material = RoadPhysicsMaterial;
            }
        }

        private void FindStreetFrame(OsmElement[] elements)
        {
            float nearestDistance = float.MaxValue;
            streetCenter = Vector2.zero;
            streetHeading = 0f;
            foreach (OsmElement element in elements)
            {
                if (!IsRoad(element))
                {
                    continue;
                }
                for (int pointIndex = 0; pointIndex < element.geometry.Length - 1; pointIndex++)
                {
                    Vector2 start = RawMeters(element.geometry[pointIndex]);
                    Vector2 end = RawMeters(element.geometry[pointIndex + 1]);
                    Vector2 closest = ClosestPointOnSegment(Vector2.zero, start, end);
                    float distance = closest.sqrMagnitude;
                    if (distance >= nearestDistance || (end - start).sqrMagnitude < 1f)
                    {
                        continue;
                    }
                    nearestDistance = distance;
                    streetCenter = closest;
                    Vector2 direction = (end - start).normalized;
                    streetHeading = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                }
            }
        }

        private void BuildRoadNetwork(OsmElement[] elements, Material material)
        {
            foreach (OsmElement element in elements)
            {
                if (!IsRoad(element))
                {
                    continue;
                }
                float roadWidth = RoadWidth(element.tags);
                for (int pointIndex = 0; pointIndex < element.geometry.Length - 1; pointIndex++)
                {
                    Vector3 start = ToWorld(element.geometry[pointIndex]);
                    Vector3 end = ToWorld(element.geometry[pointIndex + 1]);
                    if (start.sqrMagnitude > 6400f && end.sqrMagnitude > 6400f)
                    {
                        continue;
                    }
                    CreateSegment("OSM_Road", start, end, roadWidth, 0.08f, material);
                }
            }
        }

        private void BuildBuildings(OsmElement[] elements, Material material)
        {
            foreach (OsmElement element in elements)
            {
                if (element == null
                    || element.tags == null
                    || string.IsNullOrEmpty(element.tags.building)
                    || element.geometry == null
                    || element.geometry.Length < 3)
                {
                    continue;
                }

                Vector3 minimum = new Vector3(float.MaxValue, 0f, float.MaxValue);
                Vector3 maximum = new Vector3(float.MinValue, 0f, float.MinValue);
                foreach (OsmGeometryPoint point in element.geometry)
                {
                    Vector3 worldPoint = ToWorld(point);
                    minimum.x = Mathf.Min(minimum.x, worldPoint.x);
                    minimum.z = Mathf.Min(minimum.z, worldPoint.z);
                    maximum.x = Mathf.Max(maximum.x, worldPoint.x);
                    maximum.z = Mathf.Max(maximum.z, worldPoint.z);
                }
                Vector3 center = (minimum + maximum) * 0.5f;
                if (new Vector2(center.x, center.z).sqrMagnitude > 4900f)
                {
                    continue;
                }
                float width = Mathf.Max(1.8f, maximum.x - minimum.x);
                float depth = Mathf.Max(1.8f, maximum.z - minimum.z);
                int levels = ParseLevels(element.tags.buildingLevels, element.id);
                float height = levels * 2.8f;
                center.y = height * 0.5f;
                CreateBox(
                    "OSM_Building_" + element.id,
                    center,
                    new Vector3(width, height, depth),
                    material,
                    true
                );
            }
        }

        private void CreatePlayableCourt(Material roadMaterial, Material sidewalkMaterial, Material markingMaterial)
        {
            CreateBox("PlayableRoad", new Vector3(0f, 0.02f, 0f), new Vector3(12f, 0.16f, 32f), roadMaterial, true);
            CreateBox("Sidewalk_Left", new Vector3(-6.75f, 0.14f, 0f), new Vector3(1.5f, 0.24f, 32f), sidewalkMaterial, true);
            CreateBox("Sidewalk_Right", new Vector3(6.75f, 0.14f, 0f), new Vector3(1.5f, 0.24f, 32f), sidewalkMaterial, true);
            CreateBox("CenterLine", new Vector3(0f, 0.115f, 0f), new Vector3(0.08f, 0.025f, 28f), markingMaterial, false);
            CreateBox("NorthGoalLine", new Vector3(0f, 0.118f, 13.8f), new Vector3(11f, 0.028f, 0.08f), markingMaterial, false);
            CreateBox("SouthGoalLine", new Vector3(0f, 0.118f, -13.8f), new Vector3(11f, 0.028f, 0.08f), markingMaterial, false);
        }

        private void CreateBoundaryColliders()
        {
            CreateInvisibleWall("Wall_Left", new Vector3(-6f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 32f));
            CreateInvisibleWall("Wall_Right", new Vector3(6f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 32f));
            CreateInvisibleWall("Wall_North", new Vector3(0f, 1.2f, 16f), new Vector3(12f, 2.4f, 0.2f));
            CreateInvisibleWall("Wall_South", new Vector3(0f, 1.2f, -16f), new Vector3(12f, 2.4f, 0.2f));
        }

        private void CreateInvisibleWall(string objectName, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject(objectName);
            wall.transform.SetParent(generatedRoot, false);
            wall.transform.localPosition = position;
            BoxCollider wallCollider = wall.AddComponent<BoxCollider>();
            wallCollider.size = size;
        }

        private void CreateGoal(string objectName, Vector3 position, bool facesNorth, int teamThatScores, Material material)
        {
            const float width = 4.5f;
            const float height = 2.2f;
            const float post = 0.16f;
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(generatedRoot, false);
            root.transform.localPosition = position;
            root.transform.localRotation = facesNorth ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            CreateBoxPart(root.transform, "Post_Left", new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(post, height, post), material);
            CreateBoxPart(root.transform, "Post_Right", new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(post, height, post), material);
            CreateBoxPart(root.transform, "Crossbar", new Vector3(0f, height, 0f), new Vector3(width + post, post, post), material);
            GameObject triggerObject = new GameObject("GoalTrigger");
            triggerObject.transform.SetParent(root.transform, false);
            triggerObject.transform.localPosition = new Vector3(0f, height * 0.45f, 0.35f);
            BoxCollider triggerCollider = triggerObject.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(width - post, height * 0.9f, 0.7f);
            triggerObject.AddComponent<GoalTrigger>().Configure(teamThatScores);
        }

        private void CreateSegment(string objectName, Vector3 start, Vector3 end, float width, float height, Material material)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < 0.2f)
            {
                return;
            }
            GameObject segment = CreateBox(objectName, (start + end) * 0.5f, new Vector3(width, height, length), material, false);
            segment.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(generatedRoot, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Destroy(box.GetComponent<BoxCollider>());
            }
            return box;
        }

        private static void CreateBoxPart(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private Material CreateMaterial(string name, Color color)
        {
            Material material = RuntimeSurfaceMaterial.Create(name, color);
            runtimeAssets.Add(material);
            return material;
        }

        private Vector2 RawMeters(OsmGeometryPoint point)
        {
            return new Vector2(
                (float)((point.lon - longitudeOrigin) * metersPerLongitude),
                (float)((point.lat - latitudeOrigin) * 111320d)
            );
        }

        private Vector3 ToWorld(OsmGeometryPoint point)
        {
            Vector2 raw = RawMeters(point) - streetCenter;
            return Quaternion.Euler(0f, -streetHeading, 0f) * new Vector3(raw.x, 0f, raw.y);
        }

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float denominator = segment.sqrMagnitude;
            if (denominator < 0.0001f)
            {
                return start;
            }
            float interpolation = Mathf.Clamp01(Vector2.Dot(point - start, segment) / denominator);
            return start + segment * interpolation;
        }

        private static bool IsRoad(OsmElement element)
        {
            return element != null
                && element.tags != null
                && !string.IsNullOrEmpty(element.tags.highway)
                && element.geometry != null
                && element.geometry.Length >= 2;
        }

        private static float RoadWidth(OsmTags tags)
        {
            if (tags != null
                && float.TryParse(tags.width, NumberStyles.Float, CultureInfo.InvariantCulture, out float explicitWidth))
            {
                return Mathf.Clamp(explicitWidth, 3f, 14f);
            }
            if (tags == null)
            {
                return 6f;
            }
            switch (tags.highway)
            {
                case "motorway":
                case "trunk":
                case "primary":
                    return 11f;
                case "secondary":
                case "tertiary":
                    return 8.5f;
                case "service":
                case "living_street":
                    return 5f;
                case "footway":
                case "path":
                case "pedestrian":
                    return 2.5f;
                default:
                    return 6.5f;
            }
        }

        private static int ParseLevels(string levelsText, long elementId)
        {
            if (int.TryParse(levelsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int levels))
            {
                return Mathf.Clamp(levels, 1, 12);
            }
            return 2 + (int)(Math.Abs(elementId) % 4L);
        }

        private void OnDestroy()
        {
            foreach (UnityEngine.Object runtimeAsset in runtimeAssets)
            {
                if (runtimeAsset != null)
                {
                    Destroy(runtimeAsset);
                }
            }
            runtimeAssets.Clear();
        }
    }
}
