using System;
using StreetTurf.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StreetTurf.Poc
{
    /// <summary>
    /// Creates the complete playable PoC at runtime so the GitHub-built APK is immediately testable.
    /// </summary>
    public sealed class PocBootstrap : MonoBehaviour
    {
        private string startupError;

        private void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            try
            {
                BuildDemo();
            }
            catch (Exception exception)
            {
                startupError = "Le terrain n'a pas pu demarrer. Consultez la console Unity.\n" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(startupError))
            {
                return;
            }

            Rect safe = Screen.safeArea;
            Rect panel = new Rect(safe.x + 36f, safe.center.y - 85f, safe.width - 72f, 170f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(
                new Rect(panel.x + 24f, panel.y + 24f, panel.width - 48f, panel.height - 48f),
                startupError
            );
        }

        private void BuildDemo()
        {
            Camera matchCamera = CreateCamera();
            CreateSun();

            GameObject managerObject = new GameObject("GameManager");
            GameManager manager = managerObject.AddComponent<GameManager>();

            GameObject generatorObject = new GameObject("StreetGenerator");
            StreetGenerator generator = generatorObject.AddComponent<StreetGenerator>();
            generator.Generate();

            BallPhysics ball = CreateBall(generator.BallSpawn, manager, generator.RoadPhysicsMaterial);
            MobilePlayerController player = CreatePlayer(generator.PlayerSpawn);
            TouchJoystick joystick = CreateMobileUi(player);

            player.Configure(joystick, matchCamera.transform, ball);
            manager.Configure(ball, player, generator.BallSpawn, generator.PlayerSpawn);

            PocCameraFollow follow = matchCamera.gameObject.AddComponent<PocCameraFollow>();
            follow.Configure(player.transform, ball.transform);
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

        private static void CreateSun()
        {
            if (FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            GameObject lightObject = new GameObject("Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        }

        private static BallPhysics CreateBall(
            Vector3 spawn,
            GameManager manager,
            PhysicsMaterial roadMaterial
        )
        {
            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "StreetFootball";
            ballObject.transform.position = spawn;
            ballObject.transform.localScale = Vector3.one * 0.44f;
            ballObject.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(
                "BallMaterial",
                new Color(0.93f, 0.95f, 0.98f)
            );

            Rigidbody body = ballObject.AddComponent<Rigidbody>();
            body.mass = 0.43f;
            BallPhysics ball = ballObject.AddComponent<BallPhysics>();
            ball.Configure(manager, roadMaterial);
            return ball;
        }

        private static MobilePlayerController CreatePlayer(Vector3 spawn)
        {
            GameObject root = new GameObject("LocalPlayer");
            root.transform.position = spawn;
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.radius = 0.36f;
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 45f;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PlayerVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.up * 0.9f;
            visual.transform.localScale = new Vector3(0.72f, 0.9f, 0.72f);
            Destroy(visual.GetComponent<CapsuleCollider>());
            visual.GetComponent<Renderer>().sharedMaterial = CreateColorMaterial(
                "PlayerMaterial",
                new Color(0.08f, 0.45f, 0.95f)
            );

            return root.AddComponent<MobilePlayerController>();
        }

        private static TouchJoystick CreateMobileUi(MobilePlayerController player)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule)
            );
            eventSystemObject.transform.SetParent(null);

            GameObject canvasObject = new GameObject(
                "MobileControls",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
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
                new Color(0.04f, 0.08f, 0.16f, 0.58f),
                new Vector2(0f, 0f),
                new Vector2(170f, 170f),
                new Vector2(145f, 145f)
            );
            RectTransform joystickHandle = CreatePanel(
                joystickBackground,
                "Handle",
                new Color(0.2f, 0.72f, 1f, 0.9f),
                new Vector2(0.5f, 0.5f),
                new Vector2(76f, 76f),
                Vector2.zero
            );
            TouchJoystick joystick = joystickBackground.gameObject.AddComponent<TouchJoystick>();
            joystick.Configure(joystickBackground, joystickHandle);

            TouchActionButton shoot = CreateActionButton(
                canvas.transform,
                "TIR",
                new Vector2(1f, 0f),
                new Vector2(-130f, 135f),
                new Color(0.92f, 0.24f, 0.17f, 0.88f)
            );
            shoot.AddPressedListener(player.BeginShot);
            shoot.AddReleasedListener(player.ReleaseShot);

            TouchActionButton pass = CreateActionButton(
                canvas.transform,
                "PASSE",
                new Vector2(1f, 0f),
                new Vector2(-300f, 100f),
                new Color(0.15f, 0.68f, 0.3f, 0.88f)
            );
            pass.AddPressedListener(player.Pass);

            TouchActionButton sprint = CreateActionButton(
                canvas.transform,
                "SPRINT",
                new Vector2(1f, 0f),
                new Vector2(-160f, 300f),
                new Color(0.95f, 0.62f, 0.08f, 0.88f)
            );
            sprint.AddPressedListener(player.BeginSprint);
            sprint.AddReleasedListener(player.EndSprint);

            TouchActionButton tackle = CreateActionButton(
                canvas.transform,
                "TACLE",
                new Vector2(1f, 0f),
                new Vector2(-355f, 260f),
                new Color(0.46f, 0.25f, 0.88f, 0.88f)
            );
            tackle.AddPressedListener(player.Tackle);
            return joystick;
        }

        private static RectTransform CreatePanel(
            Transform parent,
            string objectName,
            Color color,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition
        )
        {
            GameObject panel = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
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

        private static TouchActionButton CreateActionButton(
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Color color
        )
        {
            RectTransform buttonRect = CreatePanel(
                parent,
                $"Button_{label}",
                color,
                anchor,
                new Vector2(132f, 132f),
                anchoredPosition
            );
            TouchActionButton button = buttonRect.gameObject.AddComponent<TouchActionButton>();

            GameObject textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
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

        private static Material CreateColorMaterial(string materialName, Color color)
        {
            return RuntimeSurfaceMaterial.Create(materialName, color);
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

            Vector3 focus = Vector3.Lerp(player.position, ball.position, 0.35f);
            float separation = Vector3.Distance(player.position, ball.position);
            Vector3 desired = focus + new Vector3(0f, 8.5f, -10f - separation * 0.12f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.16f);
            transform.rotation = Quaternion.LookRotation(focus + Vector3.up * 0.7f - transform.position, Vector3.up);
        }
    }
}
