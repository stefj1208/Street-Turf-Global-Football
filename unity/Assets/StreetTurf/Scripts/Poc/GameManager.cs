using System.Collections;
using UnityEngine;

namespace StreetTurf.Poc
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField, Min(30f)] private float matchDurationSeconds = 180f;

        private BallPhysics ball;
        private MobilePlayerController player;
        private Vector3 ballSpawn;
        private Vector3 playerSpawn;
        private Quaternion playerRotation;
        private float remainingSeconds;
        private int homeScore;
        private int awayScore;
        private bool goalResetInProgress;
        private bool matchFinished;

        public static GameManager Instance { get; private set; }
        public int HomeScore => homeScore;
        public int AwayScore => awayScore;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            remainingSeconds = matchDurationSeconds;
        }

        public void Configure(
            BallPhysics matchBall,
            MobilePlayerController controlledPlayer,
            Vector3 newBallSpawn,
            Vector3 newPlayerSpawn
        )
        {
            ball = matchBall;
            player = controlledPlayer;
            ballSpawn = newBallSpawn;
            playerSpawn = newPlayerSpawn;
            playerRotation = Quaternion.identity;
            ResetPositions();
        }

        private void Update()
        {
            if (matchFinished || ball == null || player == null)
            {
                return;
            }

            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (remainingSeconds <= 0f)
            {
                matchFinished = true;
                ball.Body.isKinematic = true;
            }
        }

        public void RegisterGoal(int teamThatScores)
        {
            if (goalResetInProgress || matchFinished)
            {
                return;
            }

            if (teamThatScores == 0)
            {
                homeScore++;
            }
            else
            {
                awayScore++;
            }
            StartCoroutine(ResetAfterGoal());
        }

        private IEnumerator ResetAfterGoal()
        {
            goalResetInProgress = true;
            ball.Body.isKinematic = true;
            yield return new WaitForSeconds(1.2f);
            ball.Body.isKinematic = false;
            ResetPositions();
            goalResetInProgress = false;
        }

        private void ResetPositions()
        {
            if (ball != null)
            {
                ball.ResetTo(ballSpawn);
            }
            if (player != null)
            {
                player.ResetTo(playerSpawn, playerRotation);
            }
        }

        private void OnGUI()
        {
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.width / 24, 20, 44),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.width / 48, 14, 24),
                normal = { textColor = new Color(0.92f, 0.95f, 1f) },
            };

            int totalSeconds = Mathf.CeilToInt(remainingSeconds);
            string clock = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            Rect safe = Screen.safeArea;
            GUI.Label(new Rect(safe.x, safe.y + 8f, safe.width, 56f), $"HOME  {homeScore}  —  {awayScore}  AWAY", scoreStyle);
            GUI.Label(new Rect(safe.x, safe.y + 56f, safe.width, 38f), clock, infoStyle);
            GUI.Label(new Rect(safe.x, safe.yMax - 42f, safe.width, 32f), "PoC local • JSON simulé • Match 3 minutes", infoStyle);

            if (player != null && player.ShotCharge01 > 0f)
            {
                float width = Mathf.Min(320f, safe.width * 0.45f);
                Rect background = new Rect(safe.center.x - width * 0.5f, safe.yMax - 96f, width, 18f);
                GUI.Box(background, GUIContent.none);
                Color previous = GUI.color;
                GUI.color = Color.Lerp(Color.green, Color.red, player.ShotCharge01);
                GUI.Box(new Rect(background.x + 2f, background.y + 2f, (background.width - 4f) * player.ShotCharge01, background.height - 4f), GUIContent.none);
                GUI.color = previous;
            }

            if (matchFinished)
            {
                GUI.Label(new Rect(safe.x, safe.center.y - 50f, safe.width, 100f), "MATCH TERMINÉ", scoreStyle);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}

