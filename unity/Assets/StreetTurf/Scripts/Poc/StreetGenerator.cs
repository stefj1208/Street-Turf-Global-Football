using System;
using System.Collections.Generic;
using UnityEngine;

namespace StreetTurf.Poc
{
    /// <summary>
    /// Reads the simulated Vision JSON and builds a deterministic playable street.
    /// OSM geometry can replace the simple dimensions later without changing the biome contract.
    /// </summary>
    public sealed class StreetGenerator : MonoBehaviour
    {
        public const string DefaultBiomeJson =
            "{\"schemaVersion\":1,\"road\":{\"materialId\":\"asphalt_worn\",\"baseColor\":\"#4A4B4D\",\"roughness\":0.84,\"friction\":0.62,\"bounce\":0.36},\"sidewalk\":{\"materialId\":\"gray_pavers\",\"baseColor\":\"#888A8D\",\"curbHeightMeters\":0.14},\"buildings\":{\"materialId\":\"red_brick\",\"style\":\"industrial\",\"baseColor\":\"#87483F\",\"estimatedFloors\":3},\"lighting\":{\"preset\":\"day_overcast\",\"sunIntensity\":0.75,\"fogDensity\":0.008},\"confidence\":0.88,\"notes\":[\"PoC simulated biome\"]}";

        [Header("Simulated backend response")]
        [SerializeField] private TextAsset simulatedBiomeJson;

        [Header("Whitebox dimensions")]
        [SerializeField, Min(18f)] private float roadLength = 36f;
        [SerializeField, Min(7f)] private float roadWidth = 12f;
        [SerializeField, Min(0.8f)] private float sidewalkWidth = 1.6f;
        [SerializeField, Range(4, 12)] private int buildingCountPerSide = 7;

        private readonly List<UnityEngine.Object> runtimeAssets = new List<UnityEngine.Object>();
        private Transform generatedRoot;

        public StreetBiomeData CurrentBiome { get; private set; }
        public PhysicsMaterial RoadPhysicsMaterial { get; private set; }
        public Vector3 BallSpawn => new Vector3(0f, 0.32f, 0f);
        public Vector3 PlayerSpawn => new Vector3(0f, 0.02f, -8f);

        public StreetBiomeData Generate()
        {
            ClearGeneratedStreet();

            string json = simulatedBiomeJson != null
                ? simulatedBiomeJson.text
                : DefaultBiomeJson;
            CurrentBiome = JsonUtility.FromJson<StreetBiomeData>(json);
            ValidateBiome(CurrentBiome);

            generatedRoot = new GameObject("GeneratedStreet").transform;
            generatedRoot.SetParent(transform, false);

            Material roadMaterial = CreateMaterial(
                "Road_Asphalt",
                ParseColor(CurrentBiome.road.baseColor, new Color(0.29f, 0.30f, 0.31f)),
                CurrentBiome.road.roughness,
                11.5f,
                1337
            );
            Material sidewalkMaterial = CreateMaterial(
                "Sidewalk_Pavers",
                ParseColor(CurrentBiome.sidewalk.baseColor, new Color(0.53f, 0.54f, 0.55f)),
                0.75f,
                5f,
                2048
            );
            Material buildingMaterial = CreateMaterial(
                "Building_Facade",
                ParseColor(CurrentBiome.buildings.baseColor, new Color(0.53f, 0.28f, 0.25f)),
                0.7f,
                3f,
                4096
            );
            Material markingMaterial = CreateFlatMaterial("Road_Markings", new Color(0.9f, 0.88f, 0.68f));
            Material goalMaterial = CreateFlatMaterial("Goal_White", Color.white);

            GameObject road = CreateBox(
                "PlayableRoad",
                Vector3.down * 0.1f,
                new Vector3(roadWidth, 0.2f, roadLength),
                roadMaterial
            );
            RoadPhysicsMaterial = new PhysicsMaterial("BiomeRoadPhysics")
            {
                dynamicFriction = CurrentBiome.road.friction,
                staticFriction = Mathf.Clamp01(CurrentBiome.road.friction + 0.08f),
                bounciness = CurrentBiome.road.bounce,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };
            runtimeAssets.Add(RoadPhysicsMaterial);
            road.GetComponent<BoxCollider>().material = RoadPhysicsMaterial;

            float curbHeight = CurrentBiome.sidewalk.curbHeightMeters;
            float sidewalkX = roadWidth * 0.5f + sidewalkWidth * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBox(
                    side < 0 ? "Sidewalk_Left" : "Sidewalk_Right",
                    new Vector3(side * sidewalkX, curbHeight * 0.5f, 0f),
                    new Vector3(sidewalkWidth, curbHeight, roadLength),
                    sidewalkMaterial
                );
            }

            CreateRoadMarkings(markingMaterial);
            CreateBuildings(buildingMaterial);
            CreateBoundaryColliders();
            CreateGoal("Goal_North", new Vector3(0f, 0f, roadLength * 0.5f - 0.6f), false, 0, goalMaterial);
            CreateGoal("Goal_South", new Vector3(0f, 0f, -roadLength * 0.5f + 0.6f), true, 1, goalMaterial);
            ConfigureLighting();
            return CurrentBiome;
        }

        private static void ValidateBiome(StreetBiomeData biome)
        {
            if (biome == null || biome.schemaVersion != 1)
            {
                throw new InvalidOperationException("Street biome schemaVersion must be 1.");
            }
            if (biome.road == null || biome.sidewalk == null || biome.buildings == null || biome.lighting == null)
            {
                throw new InvalidOperationException("Street biome is incomplete.");
            }
            biome.road.friction = Mathf.Clamp(biome.road.friction, 0.1f, 1f);
            biome.road.bounce = Mathf.Clamp01(biome.road.bounce);
            biome.road.roughness = Mathf.Clamp01(biome.road.roughness);
            biome.buildings.estimatedFloors = Mathf.Clamp(biome.buildings.estimatedFloors, 1, 8);
        }

        private void CreateRoadMarkings(Material material)
        {
            GameObject centerLine = CreateBox(
                "CenterLine",
                new Vector3(0f, 0.012f, 0f),
                new Vector3(0.08f, 0.015f, roadLength - 4f),
                material
            );
            Destroy(centerLine.GetComponent<BoxCollider>());

            for (int direction = -1; direction <= 1; direction += 2)
            {
                GameObject goalLine = CreateBox(
                    direction < 0 ? "SouthGoalLine" : "NorthGoalLine",
                    new Vector3(0f, 0.014f, direction * (roadLength * 0.5f - 2.3f)),
                    new Vector3(roadWidth - 1f, 0.018f, 0.08f),
                    material
                );
                Destroy(goalLine.GetComponent<BoxCollider>());
            }
        }

        private void CreateBuildings(Material material)
        {
            float depth = 4.5f;
            float x = roadWidth * 0.5f + sidewalkWidth + depth * 0.5f + 0.25f;
            float spacing = roadLength / buildingCountPerSide;
            float floorHeight = 2.6f;

            for (int side = -1; side <= 1; side += 2)
            {
                for (int index = 0; index < buildingCountPerSide; index++)
                {
                    int floors = Mathf.Clamp(
                        CurrentBiome.buildings.estimatedFloors + ((index + side + 1) % 3) - 1,
                        1,
                        8
                    );
                    float height = floors * floorHeight;
                    float z = -roadLength * 0.5f + spacing * (index + 0.5f);
                    CreateBox(
                        $"Building_{(side < 0 ? "L" : "R")}_{index:00}",
                        new Vector3(side * x, height * 0.5f, z),
                        new Vector3(depth, height, spacing - 0.25f),
                        material
                    );
                }
            }
        }

        private void CreateBoundaryColliders()
        {
            float wallHeight = 2.4f;
            float x = roadWidth * 0.5f;
            CreateInvisibleWall("Wall_Left", new Vector3(-x, wallHeight * 0.5f, 0f), new Vector3(0.2f, wallHeight, roadLength));
            CreateInvisibleWall("Wall_Right", new Vector3(x, wallHeight * 0.5f, 0f), new Vector3(0.2f, wallHeight, roadLength));
            CreateInvisibleWall("Wall_North", new Vector3(0f, wallHeight * 0.5f, roadLength * 0.5f), new Vector3(roadWidth, wallHeight, 0.2f));
            CreateInvisibleWall("Wall_South", new Vector3(0f, wallHeight * 0.5f, -roadLength * 0.5f), new Vector3(roadWidth, wallHeight, 0.2f));
        }

        private void CreateInvisibleWall(string objectName, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject(objectName);
            wall.transform.SetParent(generatedRoot, false);
            wall.transform.localPosition = position;
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private void CreateGoal(
            string objectName,
            Vector3 position,
            bool facesNorth,
            int teamThatScores,
            Material material
        )
        {
            const float width = 4.5f;
            const float height = 2.2f;
            const float post = 0.16f;

            GameObject root = new GameObject(objectName);
            root.transform.SetParent(generatedRoot, false);
            root.transform.localPosition = position;
            root.transform.localRotation = facesNorth
                ? Quaternion.identity
                : Quaternion.Euler(0f, 180f, 0f);

            CreateGoalPart(root.transform, "Post_Left", new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(post, height, post), material);
            CreateGoalPart(root.transform, "Post_Right", new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(post, height, post), material);
            CreateGoalPart(root.transform, "Crossbar", new Vector3(0f, height, 0f), new Vector3(width + post, post, post), material);

            GameObject triggerObject = new GameObject("GoalTrigger");
            triggerObject.transform.SetParent(root.transform, false);
            triggerObject.transform.localPosition = new Vector3(0f, height * 0.45f, 0.35f);
            BoxCollider triggerCollider = triggerObject.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(width - post, height * 0.9f, 0.7f);
            GoalTrigger trigger = triggerObject.AddComponent<GoalTrigger>();
            trigger.Configure(teamThatScores);
        }

        private void CreateGoalPart(Transform parent, string objectName, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(generatedRoot, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private Material CreateMaterial(string materialName, Color baseColor, float roughness, float textureScale, int seed)
        {
            Material material = CreateFlatMaterial(materialName, baseColor);
            Texture2D texture = CreateProceduralTexture(baseColor, seed);
            material.mainTexture = texture;
            material.mainTextureScale = new Vector2(textureScale, textureScale);
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 1f - Mathf.Clamp01(roughness));
            }
            runtimeAssets.Add(texture);
            return material;
        }

        private Material CreateFlatMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible Lit shader was found.");
            }

            Material material = new Material(shader) { name = materialName, color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            runtimeAssets.Add(material);
            return material;
        }

        private static Texture2D CreateProceduralTexture(Color baseColor, int seed)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = $"ProceduralSurface_{seed}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise((x + seed) * 0.11f, (y + seed) * 0.11f);
                    float variation = Mathf.Lerp(0.78f, 1.12f, noise);
                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(baseColor.r * variation),
                        Mathf.Clamp01(baseColor.g * variation),
                        Mathf.Clamp01(baseColor.b * variation),
                        1f
                    );
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private void ConfigureLighting()
        {
            RenderSettings.fog = CurrentBiome.lighting.fogDensity > 0.0001f;
            RenderSettings.fogDensity = CurrentBiome.lighting.fogDensity;
            RenderSettings.fogColor = new Color(0.58f, 0.63f, 0.68f);
            RenderSettings.ambientLight = new Color(0.48f, 0.5f, 0.54f);

            Light sun = FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.intensity = CurrentBiome.lighting.sunIntensity;
                sun.color = CurrentBiome.lighting.preset == "night"
                    ? new Color(0.45f, 0.58f, 1f)
                    : new Color(1f, 0.94f, 0.82f);
            }
        }

        private static Color ParseColor(string htmlColor, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(htmlColor, out Color parsed)
                ? parsed
                : fallback;
        }

        private void ClearGeneratedStreet()
        {
            if (generatedRoot != null)
            {
                Destroy(generatedRoot.gameObject);
                generatedRoot = null;
            }

            foreach (UnityEngine.Object asset in runtimeAssets)
            {
                if (asset != null)
                {
                    Destroy(asset);
                }
            }
            runtimeAssets.Clear();
        }

        private void OnDestroy()
        {
            ClearGeneratedStreet();
        }
    }
}
