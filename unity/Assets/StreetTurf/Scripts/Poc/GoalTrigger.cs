using UnityEngine;

namespace StreetTurf.Poc
{
    public sealed class GoalTrigger : MonoBehaviour
    {
        [SerializeField] private int teamThatScores;

        public int TeamThatScores => teamThatScores;

        public void Configure(int teamIndex)
        {
            teamThatScores = Mathf.Clamp(teamIndex, 0, 1);
        }
    }
}

