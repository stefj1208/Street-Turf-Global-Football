using UnityEngine;

namespace StreetTurf.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArcadePlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TouchJoystick movementJoystick;
        [SerializeField] private Camera movementCamera;
        [SerializeField] private LayerMask ballLayers;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 6.5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 14f;
        [SerializeField, Min(0f)] private float gravity = 25f;

        [Header("Ball actions")]
        [SerializeField, Min(0.5f)] private float ballControlRadius = 2f;
        [SerializeField, Min(0f)] private float shotSpeed = 15f;
        [SerializeField, Min(0f)] private float shotLift = 3.2f;
        [SerializeField, Min(0f)] private float passSpeed = 8f;
        [SerializeField, Min(0f)] private float passLift = 0.8f;

        [Header("Tackle")]
        [SerializeField, Min(0f)] private float tackleDistance = 1.2f;
        [SerializeField, Min(0.01f)] private float tackleDuration = 0.18f;
        [SerializeField, Min(0f)] private float tackleCooldown = 1f;

        private CharacterController characterController;
        private float verticalVelocity;
        private float tackleRemaining;
        private float tackleCooldownRemaining;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (movementCamera == null)
            {
                movementCamera = Camera.main;
            }
        }

        private void Update()
        {
            tackleCooldownRemaining = Mathf.Max(0f, tackleCooldownRemaining - Time.deltaTime);

            if (tackleRemaining > 0f)
            {
                tackleRemaining -= Time.deltaTime;
                float tackleSpeed = tackleDistance / tackleDuration;
                MoveCharacter(transform.forward * tackleSpeed);
                return;
            }

            Vector2 input = movementJoystick != null ? movementJoystick.Value : Vector2.zero;
            Vector3 movement = CameraRelativeDirection(input);
            if (movement.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            MoveCharacter(movement * movementSpeed);
        }

        public void Shoot()
        {
            UrbanBallController ball = FindNearestBall();
            if (ball != null)
            {
                ball.Kick(transform.forward, shotSpeed, shotLift);
            }
        }

        public void Pass()
        {
            UrbanBallController ball = FindNearestBall();
            if (ball != null)
            {
                ball.Kick(transform.forward, passSpeed, passLift);
            }
        }

        public void Tackle()
        {
            if (tackleCooldownRemaining > 0f || tackleRemaining > 0f)
            {
                return;
            }

            tackleRemaining = tackleDuration;
            tackleCooldownRemaining = tackleCooldown;
        }

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
            if (movementCamera == null)
            {
                return new Vector3(input.x, 0f, input.y);
            }

            Vector3 forward = Vector3.ProjectOnPlane(
                movementCamera.transform.forward,
                Vector3.up
            ).normalized;
            Vector3 right = Vector3.ProjectOnPlane(
                movementCamera.transform.right,
                Vector3.up
            ).normalized;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        private void MoveCharacter(Vector3 horizontalVelocity)
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }

        private UrbanBallController FindNearestBall()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                ballControlRadius,
                ballLayers,
                QueryTriggerInteraction.Collide
            );

            UrbanBallController nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            foreach (Collider hit in hits)
            {
                UrbanBallController candidate = hit.GetComponentInParent<UrbanBallController>();
                if (candidate == null)
                {
                    continue;
                }

                float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }
            return nearest;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, ballControlRadius);
        }
    }
}

