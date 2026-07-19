using System.Collections;
using UnityEngine;

namespace StreetTurf.Poc
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField, Min(30f)] private float matchDurationSeconds = 120f;

        private BallPhysics ball;
        private MobilePlayerController player;
        private StreetOpponentController opponent;
        private Vector3 ballSpawn;
        private Vector3 playerSpawn;
        private Vector3 opponentSpawn;
        private float remainingSeconds;
        private int homeScore;
        private int awayScore;
        private bool goalResetInProgress;
        private bool matchFinished;
        private string turfLabel = "HOME TURF";

        public static GameManager Instance { get; private set; }
        public int HomeScore => homeScore;
        public int AwayScore => awayScore;
        public bool MatchFinished => matchFinished;

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
            StreetOpponentController awayOpponent,
            Vector3 newBallSpawn,
            Vector3 newPlayerSpawn,
            Vector3 newOpponentSpawn,
            string newTurfLabel
        )
        {
            ball = matchBall;
            player = controlledPlayer;
            opponent = awayOpponent;
            ballSpawn = newBallSpawn;
            playerSpawn = newPlayerSpawn;
            opponentSpawn = newOpponentSpawn;
            turfLabel = string.IsNullOrWhiteSpace(newTurfLabel) ? "HOME TURF" : newTurfLabel;
            ResetPositions();
        }

        private void Update()
        {
            if (matchFinished || ball == null || player == null)
            {
                return;
            }

            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (remainingSeconds > 0f)
            {
                return;
            }

            matchFinished = true;
            ball.Body.isKinematic = true;
            player.enabled = false;
            if (opponent != null)
            {
                opponent.enabled = false;
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
            yield return new WaitForSeconds(1.1f);
            ball.Body.isKinematic = false;
            ResetPositions();
            goalResetInProgress = false;
        }

        private void RestartMatch()
        {
            StopAllCoroutines();
            homeScore = 0;
            awayScore = 0;
            remainingSeconds = matchDurationSeconds;
            matchFinished = false;
            goalResetInProgress = false;
            ball.Body.isKinematic = false;
            player.enabled = true;
            if (opponent != null)
            {
                opponent.enabled = true;
            }
            ResetPositions();
        }

        private void ResetPositions()
        {
            if (ball != null)
            {
                ball.ResetTo(ballSpawn);
            }
            if (player != null)
            {
                player.ResetTo(playerSpawn, Quaternion.identity);
            }
            if (opponent != null)
            {
                opponent.ResetTo(opponentSpawn, Quaternion.Euler(0f, 180f, 0f));
            }
        }

        private void OnGUI()
        {
            Rect safe = ToGuiRect(Screen.safeArea);
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.width / 25, 22, 46),
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.width / 55, 14, 23),
                normal = { textColor = new Color(0.82f, 0.94f, 1f) },
            };

            int totalSeconds = Mathf.CeilToInt(remainingSeconds);
            string clock = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            GUI.Label(
                new Rect(safe.x, safe.y + 8f, safe.width, 52f),
                $"HOME  {homeScore}  —  {awayScore}  AWAY",
                scoreStyle
            );
            GUI.Label(new Rect(safe.x, safe.y + 52f, safe.width, 34f), clock, infoStyle);
            GUI.Label(
                new Rect(safe.x, safe.yMax - 36f, safe.width, 28f),
                $"{turfLabel}  •  Match urbain 2 minutes",
                infoStyle
            );

            if (player != null && player.ShotCharge01 > 0f)
            {
                float width = Mathf.Min(320f, safe.width * 0.42f);
                Rect background = new Rect(
                    safe.center.x - width * 0.5f,
                    safe.yMax - 82f,
                    width,
                    18f
                );
                GUI.Box(background, GUIContent.none);
                Color previousColor = GUI.color;
                GUI.color = Color.Lerp(Color.green, Color.red, player.ShotCharge01);
                GUI.Box(
                    new Rect(
                        background.x + 2f,
                        background.y + 2f,
                        (background.width - 4f) * player.ShotCharge01,
                        background.height - 4f
                    ),
                    GUIContent.none
                );
                GUI.color = previousColor;
            }

            if (!matchFinished)
            {
                return;
            }

            string result = homeScore == awayScore
                ? "MATCH NUL"
                : homeScore > awayScore ? "VICTOIRE HOME" : "VICTOIRE AWAY";
            Rect panel = new Rect(safe.center.x - 230f, safe.center.y - 100f, 460f, 200f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 18f, panel.width, 72f), result, scoreStyle);
            if (GUI.Button(new Rect(panel.x + 80f, panel.y + 112f, panel.width - 160f, 58f), "REJOUER"))
            {
                RestartMatch();
            }
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
