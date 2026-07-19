using UnityEngine;

namespace StreetTurf.Poc
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class BallPhysics : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float rollingResistance = 1.1f;
        [SerializeField, Min(1f)] private float maximumSpeed = 28f;

        private Rigidbody body;
        private SphereCollider sphereCollider;
        private GameManager gameManager;

        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            body.mass = 0.43f;
            body.linearDamping = 0.04f;
            body.angularDamping = 0.05f;
            body.maxAngularVelocity = 55f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void Configure(GameManager manager, PhysicsMaterial roadMaterial)
        {
            gameManager = manager;
            sphereCollider.material = roadMaterial;
        }

        private void FixedUpdate()
        {
            Vector3 velocity = body.linearVelocity;
            if (velocity.magnitude > maximumSpeed)
            {
                body.linearVelocity = velocity.normalized * maximumSpeed;
            }

            if (!Physics.Raycast(transform.position, Vector3.down, 0.42f, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (horizontalVelocity.sqrMagnitude > 0.04f)
            {
                body.AddForce(-horizontalVelocity.normalized * rollingResistance, ForceMode.Acceleration);
            }
        }

        public void Kick(Vector3 worldDirection, float speed, float lift)
        {
            Vector3 flatDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up).normalized;
            if (flatDirection.sqrMagnitude < 0.01f)
            {
                return;
            }

            body.AddForce(
                flatDirection * Mathf.Max(0f, speed) + Vector3.up * Mathf.Max(0f, lift),
                ForceMode.VelocityChange
            );
        }

        public void ResetTo(Vector3 position)
        {
            body.position = position;
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        private void OnTriggerEnter(Collider other)
        {
            GoalTrigger goal = other.GetComponent<GoalTrigger>();
            if (goal != null && gameManager != null)
            {
                gameManager.RegisterGoal(goal.TeamThatScores);
            }
        }
    }
}

