using UnityEngine;

namespace StreetTurf.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class UrbanBallController : MonoBehaviour
    {
        [Header("Urban surface")]
        [SerializeField] private LayerMask urbanGroundLayers;
        [SerializeField, Min(0f)] private float rollingResistance = 1.25f;
        [SerializeField, Range(0f, 1f)] private float linearDamping = 0.08f;
        [SerializeField, Range(0f, 1f)] private float angularDamping = 0.06f;
        [SerializeField, Min(1f)] private float maximumSpeed = 30f;

        [Header("Ground check")]
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.18f;
        [SerializeField, Min(0f)] private float groundCheckOffset = 0.28f;

        private Rigidbody body;

        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = 0.43f;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.maxAngularVelocity = 60f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            Vector3 velocity = body.linearVelocity;
            if (velocity.magnitude > maximumSpeed)
            {
                body.linearVelocity = velocity.normalized * maximumSpeed;
            }

            Vector3 checkPosition = transform.position + Vector3.down * groundCheckOffset;
            bool isOnUrbanGround = Physics.CheckSphere(
                checkPosition,
                groundCheckRadius,
                urbanGroundLayers,
                QueryTriggerInteraction.Ignore
            );

            if (!isOnUrbanGround)
            {
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (horizontalVelocity.sqrMagnitude > 0.01f)
            {
                body.AddForce(
                    -horizontalVelocity.normalized * rollingResistance,
                    ForceMode.Acceleration
                );
            }
        }

        public void Kick(Vector3 direction, float speed, float lift)
        {
            Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (flatDirection.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector3 impulse = flatDirection * Mathf.Max(0f, speed) + Vector3.up * Mathf.Max(0f, lift);
            body.AddForce(impulse, ForceMode.VelocityChange);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                transform.position + Vector3.down * groundCheckOffset,
                groundCheckRadius
            );
        }
    }
}

