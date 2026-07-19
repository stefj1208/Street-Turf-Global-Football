using UnityEngine;

namespace StreetTurf.Gameplay
{
    public sealed class DynamicMatchCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform ball;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 7f, -8f);
        [SerializeField, Min(0f)] private float distanceExpansion = 0.18f;
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.18f;
        [SerializeField, Min(0f)] private float lookHeight = 1f;

        private Vector3 positionVelocity;

        private void LateUpdate()
        {
            if (player == null || ball == null)
            {
                return;
            }

            Vector3 focus = Vector3.Lerp(player.position, ball.position, 0.45f);
            float separation = Vector3.Distance(player.position, ball.position);
            Vector3 expandedOffset = localOffset;
            expandedOffset.z -= separation * distanceExpansion;

            Vector3 desiredPosition = focus + player.TransformDirection(expandedOffset);
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime
            );

            Vector3 lookTarget = focus + Vector3.up * lookHeight;
            Vector3 lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }
    }
}

