using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using StreetTurf.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StreetTurf.Poc
{
    public sealed class PocBootstrap : MonoBehaviour
    {
        private enum ExperienceState
        {
            MainMenu,
            AddressSelection,
            Masking,
            GoalPlacement,
            Customization,
            Generating,
            Match,
        }

        private const string AddressKey = "StreetTurf.Home.Address";
        private const string SavedKey = "StreetTurf.Home.Saved";
        private const string BoundaryWidthKey = "StreetTurf.Home.Width";
        private const string BoundaryLengthKey = "StreetTurf.Home.Length";
        private const string NeonKey = "StreetTurf.Home.Neon";
        private const string GraffitiKey = "StreetTurf.Home.Graffiti";
        private const string WeatherKey = "StreetTurf.Home.Weather";
        private const string SouthGoalXKey = "StreetTurf.Home.SouthGoalX";
        private const string SouthGoalZKey = "StreetTurf.Home.SouthGoalZ";
        private const string NorthGoalXKey = "StreetTurf.Home.NorthGoalX";
        private const string NorthGoalZKey = "StreetTurf.Home.NorthGoalZ";
        private const string LatitudeKey = "StreetTurf.Home.Latitude";
        private const string LongitudeKey = "StreetTurf.Home.Longitude";

        private readonly List<GameObject> obstacles = new List<GameObject>();
        private readonly HashSet<GameObject> maskedObstacles = new HashSet<GameObject>();
        private readonly List<GameObject> maskMarks = new List<GameObject>();
        private readonly List<Transform> boundaryPreview = new List<Transform>();
        private readonly List<Transform> rainDrops = new List<Transform>();
        private readonly List<Material> runtimeMaterials = new List<Material>();

        private ExperienceState state;
        private Camera sceneCamera;
        private Light sun;
        private GameObject worldHost;
        private GameObject matchRoot;
        private GameObject obstaclesRoot;
        private GameObject editorVisualsRoot;
        private GameObject cosmeticsRoot;
        private GameObject rainRoot;
        private StreetGenerator streetGenerator;
        private GeoStreetGenerator geoStreetGenerator;
        private PhysicsMaterial roadPhysicsMaterial;
        private Transform northGoal;
        private Transform southGoal;
        private Material maskMaterial;
        private Material classicGoalMaterial;
        private Material neonGoalMaterial;
        private string address = "10 rue de la Paix, Paris";
        private string startupError;
        private float boundaryWidth = 10.5f;
        private float boundaryLength = 32f;
        private int selectedGoal;
        private bool neonGoals = true;
        private bool graffitiEnabled = true;
        private int weatherIndex;
        private float generationProgress;
        private string generationStatus = string.Empty;
        private string locationStatus = string.Empty;
        private double selectedLatitude;
        private double selectedLongitude;
        private bool locationLoading;
        private bool isGeolocatedStreet;
        private Vector3 lastMaskPoint = new Vector3(float.PositiveInfinity, 0f, 0f);

        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle centeredBodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle secondaryButtonStyle;
        private GUIStyle fieldStyle;
        private GUIStyle badgeStyle;
        private Texture2D backgroundTexture;
        private Texture2D panelTexture;
        private Texture2D accentTexture;
        private Texture2D secondaryTexture;

        private bool HasSavedTurf => PlayerPrefs.GetInt(SavedKey, 0) == 1;
        private Transform GeneratedStreetRoot => worldHost != null
            ? worldHost.transform.Find("GeneratedStreet")
            : null;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            try
            {
                sceneCamera = CreateCamera();
                sun = CreateSun();
                state = ExperienceState.MainMenu;
            }
            catch (Exception exception)
            {
                startupError = "Street Turf n'a pas pu démarrer.\n" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void Update()
        {
            if (state == ExperienceState.Masking)
            {
                HandleMaskPainting();
            }
            else if (state == ExperienceState.GoalPlacement)
            {
                HandleGoalPlacement();
            }

            UpdateRain();
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            if (!string.IsNullOrEmpty(startupError))
            {
                DrawFatalError();
                return;
            }

            switch (state)
            {
                case ExperienceState.MainMenu:
                    DrawMainMenu();
                    break;
                case ExperienceState.AddressSelection:
                    DrawAddressSelection();
                    break;
                case ExperienceState.Masking:
                    DrawMaskingUi();
                    break;
                case ExperienceState.GoalPlacement:
                    DrawGoalPlacementUi();
                    break;
                case ExperienceState.Customization:
                    DrawCustomizationUi();
                    break;
                case ExperienceState.Generating:
                    DrawGenerationUi();
                    break;
                case ExperienceState.Match:
                    DrawMatchUi();
                    break;
            }
        }

        private void DrawMainMenu()
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), backgroundTexture);
            Rect safe = ToGuiRect(Screen.safeArea);
            float panelWidth = Mathf.Min(720f, safe.width * 0.74f);
            Rect panel = new Rect(
                safe.center.x - panelWidth * 0.5f,
                safe.y + safe.height * 0.08f,
                panelWidth,
                safe.height * 0.84f
            );
            GUI.DrawTexture(panel, panelTexture);

            GUILayout.BeginArea(Inset(panel, 42f));
            GUILayout.Label("STREET TURF", titleStyle);
            GUILayout.Label("GLOBAL FOOTBALL", headingStyle);
            GUILayout.Space(10f);
            GUILayout.Label(
                "Transforme ta rue en stade, personnalise ton terrain et défends-le dans un match de football arcade.",
                centeredBodyStyle
            );
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("CRÉER MON HOME TURF", buttonStyle, GUILayout.Height(68f)))
            {
                state = ExperienceState.AddressSelection;
            }

            if (HasSavedTurf)
            {
                GUILayout.Space(12f);
                if (GUILayout.Button("JOUER SUR MON TERRAIN", secondaryButtonStyle, GUILayout.Height(62f)))
                {
                    StartSavedTurfMatch();
                }
            }

            GUILayout.Space(12f);
            if (GUILayout.Button("MATCH RAPIDE — RUE DÉMO", secondaryButtonStyle, GUILayout.Height(62f)))
            {
                address = "Rue des Champions — Terrain démo";
                boundaryWidth = 10.5f;
                boundaryLength = 32f;
                neonGoals = true;
                graffitiEnabled = true;
                weatherIndex = 0;
                BuildStreet(false, true);
                ApplyCosmetics();
                StartMatch();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                "GÉOLOCALISATION OSM  •  APERÇU STREET VIEW  •  HOME / AWAY",
                badgeStyle
            );
            GUILayout.EndArea();
        }

        private void DrawAddressSelection()
        {
            DrawFullScreenBackdrop();
            Rect safe = ToGuiRect(Screen.safeArea);
            float panelWidth = Mathf.Min(820f, safe.width * 0.82f);
            Rect panel = new Rect(
                safe.center.x - panelWidth * 0.5f,
                safe.y + safe.height * 0.08f,
                panelWidth,
                safe.height * 0.84f
            );
            GUI.DrawTexture(panel, panelTexture);

            GUILayout.BeginArea(Inset(panel, 38f));
            GUILayout.Label("1 / 4  •  RUE GÉOLOCALISÉE", badgeStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Où se trouve ton Home Turf ?", headingStyle);
            GUILayout.Label(
                "L'adresse est localisée avec OpenStreetMap. Les routes et bâtiments réels autour du point deviennent ton décor 3D.",
                bodyStyle
            );
            GUILayout.Space(22f);
            GUILayout.Label("ADRESSE", badgeStyle);
            address = GUILayout.TextField(address, 80, fieldStyle, GUILayout.Height(58f));
            GUILayout.Space(14f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PARIS", secondaryButtonStyle, GUILayout.Height(50f)))
            {
                address = "10 rue de la Paix, Paris";
            }
            if (GUILayout.Button("MARSEILLE", secondaryButtonStyle, GUILayout.Height(50f)))
            {
                address = "25 rue Sainte, Marseille";
            }
            if (GUILayout.Button("DAKAR", secondaryButtonStyle, GUILayout.Height(50f)))
            {
                address = "Avenue Cheikh Anta Diop, Dakar";
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(locationStatus))
            {
                GUILayout.Space(12f);
                GUILayout.Label(locationStatus, centeredBodyStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = !locationLoading;
            if (GUILayout.Button("RETOUR", secondaryButtonStyle, GUILayout.Height(60f)))
            {
                state = ExperienceState.MainMenu;
            }
            if (GUILayout.Button(
                    locationLoading ? "CHARGEMENT OSM…" : "CHARGER LA VRAIE RUE",
                    buttonStyle,
                    GUILayout.Height(60f)
                ))
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = "Ma rue";
                }
                StartCoroutine(LoadGeolocatedStreet(address, true, false));
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawMaskingUi()
        {
            Rect safe = ToGuiRect(Screen.safeArea);
            DrawEditorHeader(
                safe,
                "2 / 4  •  MASQUE IA",
                "Peins en rouge les voitures et poubelles à effacer",
                $"{maskedObstacles.Count} obstacle(s) marqué(s) — © OpenStreetMap contributors"
            );

            Rect footer = new Rect(safe.x + 18f, safe.yMax - 112f, safe.width - 36f, 94f);
            GUI.DrawTexture(footer, panelTexture);
            GUILayout.BeginArea(Inset(footer, 12f));
            GUILayout.BeginHorizontal();
            if (isGeolocatedStreet
                && GUILayout.Button("VOIR STREET VIEW", secondaryButtonStyle, GUILayout.Height(62f)))
            {
                OpenStreetView();
            }
            if (GUILayout.Button("MARQUAGE AUTO", secondaryButtonStyle, GUILayout.Height(62f)))
            {
                MarkAllObstacles();
            }
            GUI.enabled = maskedObstacles.Count > 0;
            if (GUILayout.Button("VALIDER LE MASQUE", buttonStyle, GUILayout.Height(62f)))
            {
                state = ExperienceState.GoalPlacement;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawGoalPlacementUi()
        {
            Rect safe = ToGuiRect(Screen.safeArea);
            DrawEditorHeader(
                safe,
                "3 / 4  •  CAGES ET LIMITES",
                "Sélectionne une cage puis touche la route pour la déplacer",
                selectedGoal == 0 ? "Cage HOME sélectionnée" : "Cage AWAY sélectionnée"
            );

            Rect footer = new Rect(safe.x + 18f, safe.yMax - 184f, safe.width - 36f, 166f);
            GUI.DrawTexture(footer, panelTexture);
            GUILayout.BeginArea(Inset(footer, 12f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CAGE HOME", selectedGoal == 0 ? buttonStyle : secondaryButtonStyle, GUILayout.Height(48f)))
            {
                selectedGoal = 0;
            }
            if (GUILayout.Button("CAGE AWAY", selectedGoal == 1 ? buttonStyle : secondaryButtonStyle, GUILayout.Height(48f)))
            {
                selectedGoal = 1;
            }
            if (GUILayout.Button("CONTINUER", buttonStyle, GUILayout.Height(48f)))
            {
                ApplyCosmetics();
                state = ExperienceState.Customization;
            }
            GUILayout.EndHorizontal();

            float previousWidth = boundaryWidth;
            float previousLength = boundaryLength;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Largeur {boundaryWidth:0.0} m", bodyStyle, GUILayout.Width(180f));
            boundaryWidth = GUILayout.HorizontalSlider(boundaryWidth, 8f, 11.5f, GUILayout.Height(22f));
            GUILayout.Label($"Longueur {boundaryLength:0} m", bodyStyle, GUILayout.Width(190f));
            boundaryLength = GUILayout.HorizontalSlider(boundaryLength, 24f, 34f, GUILayout.Height(22f));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(previousWidth, boundaryWidth)
                || !Mathf.Approximately(previousLength, boundaryLength))
            {
                ApplyBoundary(false);
            }
            GUILayout.EndArea();
        }

        private void DrawCustomizationUi()
        {
            Rect safe = ToGuiRect(Screen.safeArea);
            float panelWidth = Mathf.Min(500f, safe.width * 0.42f);
            Rect panel = new Rect(safe.xMax - panelWidth - 18f, safe.y + 18f, panelWidth, safe.height - 36f);
            GUI.DrawTexture(panel, panelTexture);

            GUILayout.BeginArea(Inset(panel, 24f));
            GUILayout.Label("4 / 4  •  STYLE", badgeStyle);
            GUILayout.Label("Personnalise ta rue", headingStyle);
            GUILayout.Label("Les options sont sauvegardées avec ton Home Turf.", bodyStyle);
            GUILayout.Space(16f);

            if (GUILayout.Button(
                    graffitiEnabled ? "GRAFFITI : ACTIVÉ" : "GRAFFITI : DÉSACTIVÉ",
                    graffitiEnabled ? buttonStyle : secondaryButtonStyle,
                    GUILayout.Height(58f)
                ))
            {
                graffitiEnabled = !graffitiEnabled;
                ApplyCosmetics();
            }

            GUILayout.Space(10f);
            if (GUILayout.Button(
                    neonGoals ? "FILETS NÉON : ACTIVÉS" : "CAGES CLASSIQUES",
                    neonGoals ? buttonStyle : secondaryButtonStyle,
                    GUILayout.Height(58f)
                ))
            {
                neonGoals = !neonGoals;
                ApplyCosmetics();
            }

            GUILayout.Space(10f);
            if (GUILayout.Button(
                    $"MÉTÉO : {WeatherLabel()}",
                    secondaryButtonStyle,
                    GUILayout.Height(58f)
                ))
            {
                weatherIndex = (weatherIndex + 1) % 3;
                ApplyCosmetics();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("GÉNÉRER MON HOME TURF", buttonStyle, GUILayout.Height(68f)))
            {
                StartCoroutine(GenerateHomeTurf());
            }
            GUILayout.EndArea();
        }

        private void DrawGenerationUi()
        {
            Rect safe = ToGuiRect(Screen.safeArea);
            Rect panel = new Rect(safe.center.x - 310f, safe.center.y - 145f, 620f, 290f);
            GUI.DrawTexture(panel, panelTexture);
            GUILayout.BeginArea(Inset(panel, 34f));
            GUILayout.Label("INPAINTING CIBLÉ", headingStyle);
            GUILayout.Label(generationStatus, centeredBodyStyle);
            GUILayout.FlexibleSpace();
            Rect progressRect = GUILayoutUtility.GetRect(500f, 30f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(progressRect, secondaryTexture);
            GUI.DrawTexture(
                new Rect(
                    progressRect.x,
                    progressRect.y,
                    progressRect.width * generationProgress,
                    progressRect.height
                ),
                accentTexture
            );
            GUILayout.Space(12f);
            GUILayout.Label($"{Mathf.RoundToInt(generationProgress * 100f)} %", badgeStyle);
            GUILayout.EndArea();
        }

        private void DrawMatchUi()
        {
            Rect safe = ToGuiRect(Screen.safeArea);
            if (GUI.Button(
                    new Rect(safe.x + 14f, safe.y + 14f, 112f, 44f),
                    "MENU",
                    secondaryButtonStyle
                ))
            {
                ReturnToMenu();
            }
            GUI.Label(
                new Rect(safe.xMax - 340f, safe.y + 16f, 320f, 40f),
                ShortAddress(address),
                badgeStyle
            );
            if (isGeolocatedStreet)
            {
                GUI.Label(
                    new Rect(safe.xMax - 360f, safe.yMax - 68f, 340f, 34f),
                    "© OpenStreetMap contributors",
                    badgeStyle
                );
            }
        }

        private void DrawEditorHeader(Rect safe, string step, string title, string subtitle)
        {
            Rect header = new Rect(safe.x + 18f, safe.y + 18f, safe.width - 36f, 112f);
            GUI.DrawTexture(header, panelTexture);
            GUI.Label(new Rect(header.x + 22f, header.y + 12f, header.width - 44f, 26f), step, badgeStyle);
            GUI.Label(new Rect(header.x + 22f, header.y + 38f, header.width - 44f, 38f), title, headingStyle);
            GUI.Label(new Rect(header.x + 22f, header.y + 76f, header.width - 44f, 26f), subtitle, bodyStyle);
        }

        private void DrawFullScreenBackdrop()
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), backgroundTexture);
        }

        private void DrawFatalError()
        {
            DrawFullScreenBackdrop();
            Rect safe = ToGuiRect(Screen.safeArea);
            Rect panel = new Rect(safe.center.x - 320f, safe.center.y - 100f, 640f, 200f);
            GUI.DrawTexture(panel, panelTexture);
            GUI.Label(Inset(panel, 28f), startupError, centeredBodyStyle);
        }

        private void BuildStreet(bool includeObstacles, bool resetLayout)
        {
            ClearStreet();
            worldHost = new GameObject("HomeTurfWorld");
            streetGenerator = worldHost.AddComponent<StreetGenerator>();
            streetGenerator.Generate();
            roadPhysicsMaterial = streetGenerator.RoadPhysicsMaterial;
            isGeolocatedStreet = false;
            CompleteWorldSetup(includeObstacles, resetLayout);
        }

        private void BuildGeolocatedStreet(GeoStreetData data, bool includeObstacles, bool resetLayout)
        {
            ClearStreet();
            worldHost = new GameObject("HomeTurfWorld");
            geoStreetGenerator = worldHost.AddComponent<GeoStreetGenerator>();
            geoStreetGenerator.Generate(data);
            roadPhysicsMaterial = geoStreetGenerator.RoadPhysicsMaterial;
            selectedLatitude = data.latitude;
            selectedLongitude = data.longitude;
            address = data.displayName;
            isGeolocatedStreet = true;
            CompleteWorldSetup(includeObstacles, resetLayout);
        }

        private void CompleteWorldSetup(bool includeObstacles, bool resetLayout)
        {
            Transform generatedRoot = GeneratedStreetRoot;
            northGoal = generatedRoot != null ? generatedRoot.Find("Goal_North") : null;
            southGoal = generatedRoot != null ? generatedRoot.Find("Goal_South") : null;

            obstaclesRoot = CreateChildRoot(worldHost.transform, "StreetObstacles");
            editorVisualsRoot = CreateChildRoot(worldHost.transform, "EditorVisuals");
            cosmeticsRoot = CreateChildRoot(worldHost.transform, "Cosmetics");
            rainRoot = CreateChildRoot(worldHost.transform, "Rain");

            maskMaterial = CreateRuntimeMaterial("Mask_Red", new Color(1f, 0.04f, 0.04f));
            classicGoalMaterial = CreateRuntimeMaterial("Goal_Classic", new Color(0.96f, 0.97f, 1f));
            neonGoalMaterial = CreateRuntimeMaterial("Goal_Neon", new Color(0.05f, 1f, 0.9f));

            if (resetLayout)
            {
                boundaryWidth = 10.5f;
                boundaryLength = 32f;
            }
            ApplyBoundary(resetLayout);
            CreateBoundaryPreview();

            if (includeObstacles)
            {
                CreateStreetObstacles();
            }

            FrameEditorCamera();
            ApplyCosmetics();
        }

        private IEnumerator LoadGeolocatedStreet(string query, bool includeObstacles, bool startMatchAfterLoad)
        {
            locationLoading = true;
            locationStatus = "Connexion aux données OpenStreetMap…";
            GeoStreetData resolvedData = null;
            string loadError = null;
            GeolocatedStreetService service = GetComponent<GeolocatedStreetService>();
            if (service == null)
            {
                service = gameObject.AddComponent<GeolocatedStreetService>();
            }

            if (startMatchAfterLoad
                && Math.Abs(selectedLatitude) > 0.000001d
                && Math.Abs(selectedLongitude) > 0.000001d)
            {
                yield return service.LoadCoordinates(
                    selectedLatitude,
                    selectedLongitude,
                    query,
                    progress => locationStatus = progress,
                    data => resolvedData = data,
                    error => loadError = error
                );
            }
            else
            {
                yield return service.ResolveAddress(
                    query,
                    progress => locationStatus = progress,
                    data => resolvedData = data,
                    error => loadError = error
                );
            }

            locationLoading = false;
            if (resolvedData == null)
            {
                locationStatus = string.IsNullOrEmpty(loadError)
                    ? "Impossible de charger cette rue."
                    : loadError;
                state = ExperienceState.AddressSelection;
                yield break;
            }

            locationStatus = "Rue réelle chargée — © OpenStreetMap contributors";
            BuildGeolocatedStreet(resolvedData, includeObstacles, !startMatchAfterLoad);
            if (startMatchAfterLoad)
            {
                RestoreSavedLayout();
                StartMatch();
            }
            else
            {
                state = ExperienceState.Masking;
            }
        }

        private void ClearStreet()
        {
            if (matchRoot != null)
            {
                Destroy(matchRoot);
                matchRoot = null;
            }
            PocCameraFollow cameraFollow = sceneCamera != null
                ? sceneCamera.GetComponent<PocCameraFollow>()
                : null;
            if (cameraFollow != null)
            {
                Destroy(cameraFollow);
            }
            if (worldHost != null)
            {
                Destroy(worldHost);
                worldHost = null;
            }

            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            runtimeMaterials.Clear();
            obstacles.Clear();
            maskedObstacles.Clear();
            maskMarks.Clear();
            boundaryPreview.Clear();
            rainDrops.Clear();
            streetGenerator = null;
            geoStreetGenerator = null;
            roadPhysicsMaterial = null;
            northGoal = null;
            southGoal = null;
        }

        private void CreateStreetObstacles()
        {
            CreateCar("Voiture_Jaune", new Vector3(-2.2f, 0f, -3.5f), new Color(0.98f, 0.68f, 0.05f));
            CreateCar("Voiture_Bleue", new Vector3(2.1f, 0f, 5.2f), new Color(0.06f, 0.46f, 0.95f));
            CreateTrashCan("Poubelle_01", new Vector3(-4.7f, 0f, 6.8f));
            CreateTrashCan("Poubelle_02", new Vector3(4.6f, 0f, -7.2f));
        }

        private void CreateCar(string objectName, Vector3 position, Color color)
        {
            GameObject car = CreateChildRoot(obstaclesRoot.transform, objectName);
            car.transform.localPosition = position;
            Material bodyMaterial = CreateRuntimeMaterial(objectName + "_Material", color);
            Material glassMaterial = CreateRuntimeMaterial(objectName + "_Glass", new Color(0.07f, 0.14f, 0.22f));
            Material wheelMaterial = CreateRuntimeMaterial(objectName + "_Wheels", new Color(0.025f, 0.025f, 0.03f));

            CreatePrimitivePart(car.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.48f, 0f), new Vector3(2.1f, 0.55f, 3.7f), bodyMaterial, true);
            CreatePrimitivePart(car.transform, PrimitiveType.Cube, "Cabin", new Vector3(0f, 0.95f, 0.15f), new Vector3(1.72f, 0.58f, 1.95f), glassMaterial, true);
            for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
            {
                for (int endIndex = -1; endIndex <= 1; endIndex += 2)
                {
                    GameObject wheel = CreatePrimitivePart(
                        car.transform,
                        PrimitiveType.Cylinder,
                        "Wheel",
                        new Vector3(sideIndex * 1.03f, 0.3f, endIndex * 1.15f),
                        new Vector3(0.38f, 0.18f, 0.38f),
                        wheelMaterial,
                        true
                    );
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            obstacles.Add(car);
        }

        private void CreateTrashCan(string objectName, Vector3 position)
        {
            GameObject trashCan = CreateChildRoot(obstaclesRoot.transform, objectName);
            trashCan.transform.localPosition = position;
            Material trashMaterial = CreateRuntimeMaterial(objectName + "_Material", new Color(0.13f, 0.32f, 0.22f));
            CreatePrimitivePart(
                trashCan.transform,
                PrimitiveType.Cylinder,
                "Container",
                new Vector3(0f, 0.65f, 0f),
                new Vector3(0.75f, 0.65f, 0.75f),
                trashMaterial,
                true
            );
            CreatePrimitivePart(
                trashCan.transform,
                PrimitiveType.Cylinder,
                "Lid",
                new Vector3(0f, 1.3f, 0f),
                new Vector3(0.86f, 0.08f, 0.86f),
                trashMaterial,
                true
            );
            obstacles.Add(trashCan);
        }

        private void HandleMaskPainting()
        {
            if (!TryGetPaintPointer(out Vector2 pointerPosition, out bool strokeEnded))
            {
                if (strokeEnded)
                {
                    lastMaskPoint = new Vector3(float.PositiveInfinity, 0f, 0f);
                }
                return;
            }
            if (!IsEditorInteractionPoint(pointerPosition, 138f, 126f))
            {
                return;
            }

            Ray ray = sceneCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            GameObject obstacle = FindObstacle(hit.collider.transform);
            if (obstacle != null)
            {
                MarkObstacle(obstacle);
            }

            if (hit.point.y < 2.1f
                && (float.IsPositiveInfinity(lastMaskPoint.x)
                    || Vector3.Distance(lastMaskPoint, hit.point) > 0.38f))
            {
                CreateMaskMark(hit.point);
                lastMaskPoint = hit.point;
            }
        }

        private void HandleGoalPlacement()
        {
            if (!TryGetTap(out Vector2 pointerPosition)
                || !IsEditorInteractionPoint(pointerPosition, 138f, 198f))
            {
                return;
            }

            Ray ray = sceneCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)
                || hit.collider.gameObject.name != "PlayableRoad")
            {
                return;
            }

            Transform goal = selectedGoal == 0 ? southGoal : northGoal;
            if (goal == null)
            {
                return;
            }

            float halfWidth = boundaryWidth * 0.5f - 2.35f;
            float halfLength = boundaryLength * 0.5f - 0.8f;
            float goalZ = selectedGoal == 0
                ? Mathf.Clamp(hit.point.z, -halfLength, -5f)
                : Mathf.Clamp(hit.point.z, 5f, halfLength);
            goal.localPosition = new Vector3(
                Mathf.Clamp(hit.point.x, -halfWidth, halfWidth),
                0f,
                goalZ
            );
            FaceGoalsTowardEachOther();
        }

        private GameObject FindObstacle(Transform hitTransform)
        {
            foreach (GameObject obstacle in obstacles)
            {
                if (obstacle != null && (hitTransform == obstacle.transform || hitTransform.IsChildOf(obstacle.transform)))
                {
                    return obstacle;
                }
            }
            return null;
        }

        private void MarkObstacle(GameObject obstacle)
        {
            if (obstacle == null || !maskedObstacles.Add(obstacle))
            {
                return;
            }
            foreach (Renderer obstacleRenderer in obstacle.GetComponentsInChildren<Renderer>())
            {
                obstacleRenderer.sharedMaterial = maskMaterial;
            }
        }

        private void MarkAllObstacles()
        {
            foreach (GameObject obstacle in obstacles)
            {
                MarkObstacle(obstacle);
            }
        }

        private void CreateMaskMark(Vector3 position)
        {
            GameObject mark = CreatePrimitivePart(
                editorVisualsRoot.transform,
                PrimitiveType.Cylinder,
                "MaskBrush",
                position + Vector3.up * 0.035f,
                new Vector3(0.42f, 0.018f, 0.42f),
                maskMaterial,
                false
            );
            maskMarks.Add(mark);
        }

        private void ApplyBoundary(bool resetGoals)
        {
            if (worldHost == null || GeneratedStreetRoot == null)
            {
                return;
            }

            float halfWidth = boundaryWidth * 0.5f;
            float halfLength = boundaryLength * 0.5f;
            SetWall("Wall_Left", new Vector3(-halfWidth, 1.2f, 0f), new Vector3(0.2f, 2.4f, boundaryLength));
            SetWall("Wall_Right", new Vector3(halfWidth, 1.2f, 0f), new Vector3(0.2f, 2.4f, boundaryLength));
            SetWall("Wall_North", new Vector3(0f, 1.2f, halfLength), new Vector3(boundaryWidth, 2.4f, 0.2f));
            SetWall("Wall_South", new Vector3(0f, 1.2f, -halfLength), new Vector3(boundaryWidth, 2.4f, 0.2f));

            if (resetGoals && northGoal != null && southGoal != null)
            {
                northGoal.localPosition = new Vector3(0f, 0f, halfLength - 0.65f);
                southGoal.localPosition = new Vector3(0f, 0f, -halfLength + 0.65f);
            }
            else
            {
                ClampGoalToBoundary(northGoal, true);
                ClampGoalToBoundary(southGoal, false);
            }

            UpdateBoundaryPreview();
            FaceGoalsTowardEachOther();
        }

        private void SetWall(string wallName, Vector3 position, Vector3 size)
        {
            Transform wall = GeneratedStreetRoot.Find(wallName);
            if (wall == null)
            {
                return;
            }
            wall.localPosition = position;
            BoxCollider wallCollider = wall.GetComponent<BoxCollider>();
            if (wallCollider != null)
            {
                wallCollider.size = size;
            }
        }

        private void ClampGoalToBoundary(Transform goal, bool northSide)
        {
            if (goal == null)
            {
                return;
            }
            float maximumX = Mathf.Max(0f, boundaryWidth * 0.5f - 2.35f);
            float maximumZ = boundaryLength * 0.5f - 0.65f;
            Vector3 position = goal.localPosition;
            position.x = Mathf.Clamp(position.x, -maximumX, maximumX);
            position.z = northSide
                ? Mathf.Clamp(position.z, 5f, maximumZ)
                : Mathf.Clamp(position.z, -maximumZ, -5f);
            goal.localPosition = position;
        }

        private void FaceGoalsTowardEachOther()
        {
            if (northGoal == null || southGoal == null)
            {
                return;
            }
            Vector3 southToNorth = Vector3.ProjectOnPlane(northGoal.position - southGoal.position, Vector3.up);
            if (southToNorth.sqrMagnitude < 0.01f)
            {
                return;
            }
            southGoal.rotation = Quaternion.LookRotation(southToNorth.normalized, Vector3.up);
            northGoal.rotation = Quaternion.LookRotation(-southToNorth.normalized, Vector3.up);
        }

        private void CreateBoundaryPreview()
        {
            Material previewMaterial = CreateRuntimeMaterial("Boundary_Neon", new Color(1f, 0.78f, 0.04f));
            for (int previewIndex = 0; previewIndex < 4; previewIndex++)
            {
                GameObject preview = CreatePrimitivePart(
                    editorVisualsRoot.transform,
                    PrimitiveType.Cube,
                    "BoundaryPreview",
                    Vector3.zero,
                    Vector3.one,
                    previewMaterial,
                    false
                );
                boundaryPreview.Add(preview.transform);
            }
            UpdateBoundaryPreview();
        }

        private void UpdateBoundaryPreview()
        {
            if (boundaryPreview.Count != 4)
            {
                return;
            }
            float halfWidth = boundaryWidth * 0.5f;
            float halfLength = boundaryLength * 0.5f;
            SetPreview(boundaryPreview[0], new Vector3(-halfWidth, 0.05f, 0f), new Vector3(0.09f, 0.05f, boundaryLength));
            SetPreview(boundaryPreview[1], new Vector3(halfWidth, 0.05f, 0f), new Vector3(0.09f, 0.05f, boundaryLength));
            SetPreview(boundaryPreview[2], new Vector3(0f, 0.05f, halfLength), new Vector3(boundaryWidth, 0.05f, 0.09f));
            SetPreview(boundaryPreview[3], new Vector3(0f, 0.05f, -halfLength), new Vector3(boundaryWidth, 0.05f, 0.09f));
        }

        private static void SetPreview(Transform preview, Vector3 position, Vector3 scale)
        {
            preview.localPosition = position;
            preview.localScale = scale;
        }

        private void ApplyCosmetics()
        {
            if (worldHost == null)
            {
                return;
            }
            Material selectedGoalMaterial = neonGoals ? neonGoalMaterial : classicGoalMaterial;
            ApplyGoalMaterial(northGoal, selectedGoalMaterial);
            ApplyGoalMaterial(southGoal, selectedGoalMaterial);
            BuildGraffiti();
            ApplyWeather();
        }

        private static void ApplyGoalMaterial(Transform goal, Material material)
        {
            if (goal == null || material == null)
            {
                return;
            }
            foreach (Renderer goalRenderer in goal.GetComponentsInChildren<Renderer>())
            {
                goalRenderer.sharedMaterial = material;
            }
        }

        private void BuildGraffiti()
        {
            if (cosmeticsRoot == null)
            {
                return;
            }
            Transform previousGraffiti = cosmeticsRoot.transform.Find("GraffitiWall");
            if (previousGraffiti != null)
            {
                Destroy(previousGraffiti.gameObject);
            }
            if (!graffitiEnabled)
            {
                return;
            }

            GameObject graffiti = CreateChildRoot(cosmeticsRoot.transform, "GraffitiWall");
            Material darkMaterial = CreateRuntimeMaterial("Graffiti_Dark", new Color(0.035f, 0.045f, 0.075f));
            Material pinkMaterial = CreateRuntimeMaterial("Graffiti_Pink", new Color(1f, 0.08f, 0.52f));
            Material cyanMaterial = CreateRuntimeMaterial("Graffiti_Cyan", new Color(0.04f, 0.9f, 1f));
            CreatePrimitivePart(graffiti.transform, PrimitiveType.Cube, "Panel", new Vector3(-7.78f, 3.3f, 0f), new Vector3(0.12f, 2.9f, 7.5f), darkMaterial, false);
            for (int stripeIndex = 0; stripeIndex < 6; stripeIndex++)
            {
                float verticalPosition = 2.25f + stripeIndex * 0.42f;
                float depthPosition = -2.25f + stripeIndex * 0.9f;
                CreatePrimitivePart(
                    graffiti.transform,
                    PrimitiveType.Cube,
                    "Tag",
                    new Vector3(-7.69f, verticalPosition, depthPosition),
                    new Vector3(0.08f, 0.22f + (stripeIndex % 2) * 0.16f, 2.4f),
                    stripeIndex % 2 == 0 ? pinkMaterial : cyanMaterial,
                    false
                );
            }
        }

        private void ApplyWeather()
        {
            if (sceneCamera == null || sun == null)
            {
                return;
            }
            if (weatherIndex == 1)
            {
                sceneCamera.backgroundColor = new Color(0.01f, 0.025f, 0.08f);
                sun.intensity = 0.28f;
                sun.color = new Color(0.36f, 0.5f, 1f);
                RenderSettings.ambientLight = new Color(0.12f, 0.17f, 0.28f);
                RenderSettings.fogColor = new Color(0.04f, 0.07f, 0.15f);
            }
            else if (weatherIndex == 2)
            {
                sceneCamera.backgroundColor = new Color(0.18f, 0.25f, 0.34f);
                sun.intensity = 0.48f;
                sun.color = new Color(0.7f, 0.82f, 1f);
                RenderSettings.ambientLight = new Color(0.28f, 0.34f, 0.42f);
                RenderSettings.fogColor = new Color(0.28f, 0.34f, 0.42f);
            }
            else
            {
                sceneCamera.backgroundColor = new Color(0.24f, 0.5f, 0.76f);
                sun.intensity = 0.85f;
                sun.color = new Color(1f, 0.94f, 0.82f);
                RenderSettings.ambientLight = new Color(0.48f, 0.5f, 0.54f);
                RenderSettings.fogColor = new Color(0.58f, 0.63f, 0.68f);
            }
            SetRainActive(weatherIndex == 2);
        }

        private void SetRainActive(bool isActive)
        {
            if (rainRoot == null)
            {
                return;
            }
            if (rainDrops.Count == 0)
            {
                Material rainMaterial = CreateRuntimeMaterial("Rain_Drops", new Color(0.42f, 0.78f, 1f));
                UnityEngine.Random.State previousState = UnityEngine.Random.state;
                UnityEngine.Random.InitState(1708);
                for (int dropIndex = 0; dropIndex < 48; dropIndex++)
                {
                    GameObject rainDrop = CreatePrimitivePart(
                        rainRoot.transform,
                        PrimitiveType.Cube,
                        "RainDrop",
                        new Vector3(
                            UnityEngine.Random.Range(-5.4f, 5.4f),
                            UnityEngine.Random.Range(2f, 14f),
                            UnityEngine.Random.Range(-16f, 16f)
                        ),
                        new Vector3(0.025f, 0.5f, 0.025f),
                        rainMaterial,
                        false
                    );
                    rainDrops.Add(rainDrop.transform);
                }
                UnityEngine.Random.state = previousState;
            }
            rainRoot.SetActive(isActive);
        }

        private void UpdateRain()
        {
            if (rainRoot == null || !rainRoot.activeSelf)
            {
                return;
            }
            foreach (Transform rainDrop in rainDrops)
            {
                if (rainDrop == null)
                {
                    continue;
                }
                rainDrop.localPosition += new Vector3(-1.3f, -18f, 0.4f) * Time.deltaTime;
                if (rainDrop.localPosition.y < 0.2f)
                {
                    Vector3 resetPosition = rainDrop.localPosition;
                    resetPosition.y = 13f;
                    resetPosition.x = Mathf.Repeat(resetPosition.x + 10.8f, 10.8f) - 5.4f;
                    rainDrop.localPosition = resetPosition;
                }
            }
        }

        private IEnumerator GenerateHomeTurf()
        {
            state = ExperienceState.Generating;
            generationProgress = 0.05f;
            generationStatus = "Préparation de l'image et du masque…";
            yield return AnimateProgress(0.22f, 0.55f);
            generationStatus = "Envoi du masque ciblé au moteur IA…";
            yield return AnimateProgress(0.46f, 0.7f);
            generationStatus = "Reconstruction de la route derrière les obstacles…";
            yield return AnimateProgress(0.76f, 1.2f);

            foreach (GameObject maskedObstacle in maskedObstacles)
            {
                if (maskedObstacle != null)
                {
                    maskedObstacle.SetActive(false);
                }
            }
            foreach (GameObject maskMark in maskMarks)
            {
                if (maskMark != null)
                {
                    maskMark.SetActive(false);
                }
            }

            generationStatus = "Compilation du Home Turf jouable…";
            yield return AnimateProgress(1f, 0.7f);
            SaveHomeTurf();
            yield return new WaitForSeconds(0.35f);
            StartMatch();
        }

        private IEnumerator AnimateProgress(float target, float duration)
        {
            float start = generationProgress;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                generationProgress = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            generationProgress = target;
        }

        private void SaveHomeTurf()
        {
            PlayerPrefs.SetInt(SavedKey, 1);
            PlayerPrefs.SetString(AddressKey, address);
            PlayerPrefs.SetFloat(BoundaryWidthKey, boundaryWidth);
            PlayerPrefs.SetFloat(BoundaryLengthKey, boundaryLength);
            PlayerPrefs.SetInt(NeonKey, neonGoals ? 1 : 0);
            PlayerPrefs.SetInt(GraffitiKey, graffitiEnabled ? 1 : 0);
            PlayerPrefs.SetInt(WeatherKey, weatherIndex);
            PlayerPrefs.SetString(
                LatitudeKey,
                selectedLatitude.ToString("R", CultureInfo.InvariantCulture)
            );
            PlayerPrefs.SetString(
                LongitudeKey,
                selectedLongitude.ToString("R", CultureInfo.InvariantCulture)
            );
            if (southGoal != null)
            {
                PlayerPrefs.SetFloat(SouthGoalXKey, southGoal.localPosition.x);
                PlayerPrefs.SetFloat(SouthGoalZKey, southGoal.localPosition.z);
            }
            if (northGoal != null)
            {
                PlayerPrefs.SetFloat(NorthGoalXKey, northGoal.localPosition.x);
                PlayerPrefs.SetFloat(NorthGoalZKey, northGoal.localPosition.z);
            }
            PlayerPrefs.Save();
        }

        private void StartSavedTurfMatch()
        {
            address = PlayerPrefs.GetString(AddressKey, "Mon Home Turf");
            LoadSavedOptions();
            state = ExperienceState.AddressSelection;
            StartCoroutine(LoadGeolocatedStreet(address, false, true));
        }

        private void LoadSavedOptions()
        {
            boundaryWidth = PlayerPrefs.GetFloat(BoundaryWidthKey, 10.5f);
            boundaryLength = PlayerPrefs.GetFloat(BoundaryLengthKey, 32f);
            neonGoals = PlayerPrefs.GetInt(NeonKey, 1) == 1;
            graffitiEnabled = PlayerPrefs.GetInt(GraffitiKey, 1) == 1;
            weatherIndex = PlayerPrefs.GetInt(WeatherKey, 0);
            double.TryParse(
                PlayerPrefs.GetString(LatitudeKey, "0"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out selectedLatitude
            );
            double.TryParse(
                PlayerPrefs.GetString(LongitudeKey, "0"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out selectedLongitude
            );
        }

        private void RestoreSavedLayout()
        {
            LoadSavedOptions();
            if (southGoal != null)
            {
                southGoal.localPosition = new Vector3(
                    PlayerPrefs.GetFloat(SouthGoalXKey, 0f),
                    0f,
                    PlayerPrefs.GetFloat(SouthGoalZKey, -15.35f)
                );
            }
            if (northGoal != null)
            {
                northGoal.localPosition = new Vector3(
                    PlayerPrefs.GetFloat(NorthGoalXKey, 0f),
                    0f,
                    PlayerPrefs.GetFloat(NorthGoalZKey, 15.35f)
                );
            }
            ApplyBoundary(false);
            ApplyCosmetics();
        }

        private void StartMatch()
        {
            state = ExperienceState.Match;
            if (editorVisualsRoot != null)
            {
                editorVisualsRoot.SetActive(false);
            }
            ApplyCosmetics();

            matchRoot = new GameObject("PlayableMatch");
            GameObject managerObject = CreateChildRoot(matchRoot.transform, "GameManager");
            GameManager manager = managerObject.AddComponent<GameManager>();

            Vector3 ballSpawn = new Vector3(0f, 0.32f, 0f);
            Vector3 playerSpawn = new Vector3(0f, 0.02f, -7.5f);
            Vector3 opponentSpawn = new Vector3(0f, 0.02f, 7.5f);
            BallPhysics ball = CreateBall(ballSpawn, manager, roadPhysicsMaterial);
            MobilePlayerController player = CreatePlayer(playerSpawn, true);
            StreetOpponentController opponent = CreateOpponent(opponentSpawn);
            TouchJoystick joystick = CreateMobileUi(player);

            player.Configure(joystick, sceneCamera.transform, ball);
            opponent.Configure(ball, southGoal);
            manager.Configure(
                ball,
                player,
                opponent,
                ballSpawn,
                playerSpawn,
                opponentSpawn,
                ShortAddress(address)
            );

            PocCameraFollow follow = sceneCamera.gameObject.AddComponent<PocCameraFollow>();
            follow.Configure(player.transform, ball.transform);
            sceneCamera.transform.position = new Vector3(0f, 9f, -13f);
        }

        private void OpenStreetView()
        {
            if (!isGeolocatedStreet)
            {
                return;
            }
            string viewpoint = selectedLatitude.ToString("F7", CultureInfo.InvariantCulture)
                + ","
                + selectedLongitude.ToString("F7", CultureInfo.InvariantCulture);
            string url = "https://www.google.com/maps/@?api=1&map_action=pano&viewpoint="
                + Uri.EscapeDataString(viewpoint)
                + "&heading=0&pitch=0&fov=90";
            Application.OpenURL(url);
        }

        private BallPhysics CreateBall(Vector3 spawn, GameManager manager, PhysicsMaterial roadMaterial)
        {
            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "StreetFootball";
            ballObject.transform.SetParent(matchRoot.transform, false);
            ballObject.transform.position = spawn;
            ballObject.transform.localScale = Vector3.one * 0.44f;
            ballObject.GetComponent<Renderer>().sharedMaterial = CreateRuntimeMaterial(
                "BallMaterial",
                new Color(0.93f, 0.95f, 0.98f)
            );
            Rigidbody body = ballObject.AddComponent<Rigidbody>();
            body.mass = 0.43f;
            BallPhysics ball = ballObject.AddComponent<BallPhysics>();
            ball.Configure(manager, roadMaterial);
            return ball;
        }

        private MobilePlayerController CreatePlayer(Vector3 spawn, bool isHome)
        {
            GameObject root = CreateCharacterRoot(isHome ? "HomePlayer" : "AwayPlayer", spawn);
            Material playerMaterial = CreateRuntimeMaterial(
                isHome ? "HomeKit" : "AwayKit",
                isHome ? new Color(0.04f, 0.48f, 1f) : new Color(1f, 0.12f, 0.2f)
            );
            Material accentMaterial = CreateRuntimeMaterial(
                isHome ? "HomeKitAccent" : "AwayKitAccent",
                isHome ? new Color(0.02f, 0.95f, 0.85f) : new Color(1f, 0.75f, 0.04f)
            );
            BuildCharacterVisual(root.transform, playerMaterial, accentMaterial);
            return root.AddComponent<MobilePlayerController>();
        }

        private StreetOpponentController CreateOpponent(Vector3 spawn)
        {
            GameObject root = CreateCharacterRoot("AwayOpponent", spawn);
            Material playerMaterial = CreateRuntimeMaterial("AwayKit", new Color(1f, 0.12f, 0.2f));
            Material accentMaterial = CreateRuntimeMaterial("AwayKitAccent", new Color(1f, 0.75f, 0.04f));
            BuildCharacterVisual(root.transform, playerMaterial, accentMaterial);
            return root.AddComponent<StreetOpponentController>();
        }

        private GameObject CreateCharacterRoot(string objectName, Vector3 spawn)
        {
            GameObject root = CreateChildRoot(matchRoot.transform, objectName);
            root.transform.position = spawn;
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.radius = 0.36f;
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 45f;
            return root;
        }

        private void BuildCharacterVisual(Transform parent, Material kitMaterial, Material accentMaterial)
        {
            CreatePrimitivePart(parent, PrimitiveType.Capsule, "PlayerBody", Vector3.up * 0.9f, new Vector3(0.72f, 0.9f, 0.72f), kitMaterial, false);
            CreatePrimitivePart(parent, PrimitiveType.Cube, "JerseyStripe", new Vector3(0f, 1.05f, 0.33f), new Vector3(0.36f, 0.62f, 0.05f), accentMaterial, false);
            Material skinMaterial = CreateRuntimeMaterial("Skin", new Color(0.58f, 0.31f, 0.18f));
            CreatePrimitivePart(parent, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.88f, 0f), Vector3.one * 0.46f, skinMaterial, false);
        }

        private TouchJoystick CreateMobileUi(MobilePlayerController player)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule)
            );
            eventSystemObject.transform.SetParent(matchRoot.transform, false);

            GameObject canvasObject = new GameObject(
                "MobileControls",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            canvasObject.transform.SetParent(matchRoot.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform joystickBackground = CreatePanel(
                canvas.transform,
                "Joystick",
                new Color(0.04f, 0.08f, 0.16f, 0.62f),
                new Vector2(0f, 0f),
                new Vector2(190f, 190f),
                new Vector2(150f, 150f)
            );
            RectTransform joystickHandle = CreatePanel(
                joystickBackground,
                "Handle",
                new Color(0.05f, 0.9f, 1f, 0.92f),
                new Vector2(0.5f, 0.5f),
                new Vector2(82f, 82f),
                Vector2.zero
            );
            TouchJoystick joystick = joystickBackground.gameObject.AddComponent<TouchJoystick>();
            joystick.Configure(joystickBackground, joystickHandle);

            TouchActionButton shoot = CreateActionButton(canvas.transform, "TIR", new Vector2(-132f, 142f), new Color(1f, 0.15f, 0.2f, 0.9f));
            shoot.AddPressedListener(player.BeginShot);
            shoot.AddReleasedListener(player.ReleaseShot);
            TouchActionButton pass = CreateActionButton(canvas.transform, "PASSE", new Vector2(-308f, 105f), new Color(0.1f, 0.78f, 0.38f, 0.9f));
            pass.AddPressedListener(player.Pass);
            TouchActionButton sprint = CreateActionButton(canvas.transform, "SPRINT", new Vector2(-165f, 315f), new Color(1f, 0.66f, 0.04f, 0.9f));
            sprint.AddPressedListener(player.BeginSprint);
            sprint.AddReleasedListener(player.EndSprint);
            TouchActionButton tackle = CreateActionButton(canvas.transform, "TACLE", new Vector2(-362f, 270f), new Color(0.6f, 0.2f, 1f, 0.9f));
            tackle.AddPressedListener(player.Tackle);
            return joystick;
        }

        private RectTransform CreatePanel(
            Transform parent,
            string objectName,
            Color color,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition
        )
        {
            GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private TouchActionButton CreateActionButton(Transform parent, string label, Vector2 position, Color color)
        {
            RectTransform buttonRect = CreatePanel(
                parent,
                "Button_" + label,
                color,
                new Vector2(1f, 0f),
                new Vector2(138f, 138f),
                position
            );
            TouchActionButton button = buttonRect.gameObject.AddComponent<TouchActionButton>();
            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(buttonRect, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 25;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return button;
        }

        private void ReturnToMenu()
        {
            ClearStreet();
            RenderSettings.fog = false;
            sceneCamera.transform.position = new Vector3(0f, 9f, -13f);
            sceneCamera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.03f, 0.06f, 0.12f);
            state = ExperienceState.MainMenu;
        }

        private void FrameEditorCamera()
        {
            sceneCamera.transform.position = new Vector3(13f, 19f, -21f);
            sceneCamera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0.5f, 0f) - sceneCamera.transform.position,
                Vector3.up
            );
            sceneCamera.fieldOfView = 55f;
        }

        private static Camera CreateCamera()
        {
            Camera existing = Camera.main;
            if (existing != null)
            {
                return existing;
            }
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0f, 9f, -13f);
            camera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            camera.fieldOfView = 58f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.06f, 0.12f);
            return camera;
        }

        private static Light CreateSun()
        {
            Light existing = FindFirstObjectByType<Light>();
            if (existing != null)
            {
                return existing;
            }
            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.85f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            return light;
        }

        private GameObject CreatePrimitivePart(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider
        )
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Collider partCollider = part.GetComponent<Collider>();
                if (partCollider != null)
                {
                    Destroy(partCollider);
                }
            }
            return part;
        }

        private static GameObject CreateChildRoot(Transform parent, string objectName)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            return child;
        }

        private Material CreateRuntimeMaterial(string materialName, Color color)
        {
            Material material = RuntimeSurfaceMaterial.Create(materialName, color);
            runtimeMaterials.Add(material);
            return material;
        }

        private static bool TryGetTap(out Vector2 position)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                position = touch.position;
                return touch.phase == TouchPhase.Began;
            }
            position = Input.mousePosition;
            return Input.GetMouseButtonDown(0);
        }

        private static bool TryGetPaintPointer(out Vector2 position, out bool strokeEnded)
        {
            strokeEnded = false;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                position = touch.position;
                strokeEnded = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                return touch.phase == TouchPhase.Began
                    || touch.phase == TouchPhase.Moved
                    || touch.phase == TouchPhase.Stationary;
            }
            position = Input.mousePosition;
            strokeEnded = Input.GetMouseButtonUp(0);
            return Input.GetMouseButton(0);
        }

        private static bool IsEditorInteractionPoint(Vector2 position, float topReserved, float bottomReserved)
        {
            Rect safe = Screen.safeArea;
            return position.x >= safe.x
                && position.x <= safe.xMax
                && position.y >= safe.y + bottomReserved
                && position.y <= safe.yMax - topReserved;
        }

        private string WeatherLabel()
        {
            return weatherIndex == 1 ? "NUIT" : weatherIndex == 2 ? "PLUIE" : "JOUR";
        }

        private static string ShortAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "HOME TURF";
            }
            return value.Length <= 34 ? value.ToUpperInvariant() : value.Substring(0, 31).ToUpperInvariant() + "…";
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
            {
                return;
            }
            backgroundTexture = MakeTexture(new Color(0.015f, 0.03f, 0.07f, 1f));
            panelTexture = MakeTexture(new Color(0.025f, 0.06f, 0.12f, 0.94f));
            accentTexture = MakeTexture(new Color(0.02f, 0.82f, 0.88f, 1f));
            secondaryTexture = MakeTexture(new Color(0.08f, 0.15f, 0.24f, 1f));
            int titleSize = Mathf.Clamp(Screen.width / 16, 44, 84);
            int headingSize = Mathf.Clamp(Screen.width / 29, 26, 48);
            int bodySize = Mathf.Clamp(Screen.width / 58, 17, 25);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = titleSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.04f, 0.94f, 1f) },
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = headingSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white },
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = bodySize,
                wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.9f, 0.98f) },
            };
            centeredBodyStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = bodySize,
                fontStyle = FontStyle.Bold,
                normal = { background = accentTexture, textColor = new Color(0.01f, 0.08f, 0.12f) },
                hover = { background = accentTexture, textColor = new Color(0.01f, 0.08f, 0.12f) },
                active = { background = accentTexture, textColor = Color.white },
            };
            secondaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = bodySize,
                fontStyle = FontStyle.Bold,
                normal = { background = secondaryTexture, textColor = Color.white },
                hover = { background = secondaryTexture, textColor = Color.white },
                active = { background = accentTexture, textColor = Color.white },
            };
            fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = bodySize,
                padding = new RectOffset(18, 18, 8, 8),
                normal = { background = secondaryTexture, textColor = Color.white },
                focused = { background = secondaryTexture, textColor = Color.white },
            };
            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(14, bodySize - 3),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.18f, 0.92f, 1f) },
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static Rect ToGuiRect(Rect screenRect)
        {
            return new Rect(
                screenRect.x,
                Screen.height - screenRect.yMax,
                screenRect.width,
                screenRect.height
            );
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(
                rect.x + amount,
                rect.y + amount,
                rect.width - amount * 2f,
                rect.height - amount * 2f
            );
        }

        private void OnDestroy()
        {
            ClearStreet();
            if (backgroundTexture != null)
            {
                Destroy(backgroundTexture);
                Destroy(panelTexture);
                Destroy(accentTexture);
                Destroy(secondaryTexture);
            }
        }
    }

    public sealed class PocCameraFollow : MonoBehaviour
    {
        private Transform player;
        private Transform ball;
        private Vector3 velocity;

        public void Configure(Transform playerTransform, Transform ballTransform)
        {
            player = playerTransform;
            ball = ballTransform;
        }

        private void LateUpdate()
        {
            if (player == null || ball == null)
            {
                return;
            }
            Vector3 focus = Vector3.Lerp(player.position, ball.position, 0.38f);
            float separation = Vector3.Distance(player.position, ball.position);
            Vector3 desired = focus + new Vector3(0f, 8.5f, -10f - separation * 0.12f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.16f);
            transform.rotation = Quaternion.LookRotation(focus + Vector3.up * 0.7f - transform.position, Vector3.up);
        }
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class StreetOpponentController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float movementSpeed = 5.4f;
        [SerializeField, Min(0f)] private float rotationSpeed = 10f;
        [SerializeField, Min(0f)] private float gravity = 25f;
        [SerializeField, Min(0.5f)] private float kickDistance = 1.8f;
        [SerializeField, Min(0f)] private float kickSpeed = 11.5f;

        private CharacterController characterController;
        private BallPhysics ball;
        private Transform targetGoal;
        private float verticalVelocity;
        private float kickCooldown;
        private float decisionOffset;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            decisionOffset = UnityEngine.Random.Range(-1.2f, 1.2f);
        }

        public void Configure(BallPhysics matchBall, Transform attackGoal)
        {
            ball = matchBall;
            targetGoal = attackGoal;
        }

        private void Update()
        {
            if (ball == null || targetGoal == null)
            {
                return;
            }

            kickCooldown = Mathf.Max(0f, kickCooldown - Time.deltaTime);
            Vector3 ballPosition = ball.transform.position;
            Vector3 desiredPosition = ballPosition;
            desiredPosition.x += decisionOffset;
            Vector3 direction = Vector3.ProjectOnPlane(desiredPosition - transform.position, Vector3.up);
            float distanceToBall = Vector3.Distance(transform.position, ballPosition);

            if (distanceToBall <= kickDistance && kickCooldown <= 0f)
            {
                Vector3 shotDirection = targetGoal.position - ballPosition;
                shotDirection.x += UnityEngine.Random.Range(-0.7f, 0.7f);
                ball.Kick(shotDirection, kickSpeed, UnityEngine.Random.Range(0.7f, 1.8f));
                kickCooldown = UnityEngine.Random.Range(0.8f, 1.35f);
                decisionOffset = UnityEngine.Random.Range(-1.2f, 1.2f);
            }

            if (direction.sqrMagnitude > 0.02f)
            {
                direction.Normalize();
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    rotationSpeed * Time.deltaTime
                );
            }

            Move(direction * movementSpeed);
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = true;
            verticalVelocity = 0f;
            kickCooldown = 0.7f;
        }

        private void Move(Vector3 horizontalVelocity)
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            characterController.Move(
                (horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime
            );
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            BallPhysics hitBall = hit.collider.GetComponent<BallPhysics>();
            if (hitBall == null)
            {
                return;
            }

            Vector3 pushDirection = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up).normalized;
            hitBall.Body.AddForce(pushDirection * 0.55f, ForceMode.VelocityChange);
        }
    }
}
