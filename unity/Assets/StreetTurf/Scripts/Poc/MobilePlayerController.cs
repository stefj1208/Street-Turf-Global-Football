using StreetTurf.Gameplay;
using UnityEngine;

namespace StreetTurf.Poc
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MobilePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 6.2f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 1.45f;
        [SerializeField, Min(0f)] private float rotationSpeed = 14f;
        [SerializeField, Min(0f)] private float gravity = 25f;

        [Header("Ball")]
        [SerializeField, Min(0.5f)] private float controlRadius = 2.1f;
        [SerializeField, Min(0f)] private float minimumShotSpeed = 9f;
        [SerializeField, Min(0f)] private float maximumShotSpeed = 18f;
        [SerializeField, Min(0f)] private float maximumShotLift = 4f;
        [SerializeField, Min(0f)] private float passSpeed = 7.5f;

        private CharacterController characterController;
        private TouchJoystick joystick;
        private Transform movementCamera;
        private BallPhysics controlledBall;
        private float verticalVelocity;
        private float shotCharge;
        private bool isChargingShot;
        private bool isSprinting;
        private float tackleRemaining;

        public float ShotCharge01 => shotCharge;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        public void Configure(TouchJoystick inputJoystick, Transform cameraTransform, BallPhysics ball)
        {
            joystick = inputJoystick;
            movementCamera = cameraTransform;
            controlledBall = ball;
        }

        private void Update()
        {
            if (isChargingShot)
            {
                shotCharge = Mathf.Clamp01(shotCharge + Time.deltaTime * 0.85f);
            }

            Vector2 input = joystick != null ? joystick.Value : Vector2.zero;
            Vector3 direction = CameraRelativeDirection(input);
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    rotationSpeed * Time.deltaTime
                );
            }

            float speed = movementSpeed * (isSprinting ? sprintMultiplier : 1f);
            if (tackleRemaining > 0f)
            {
                tackleRemaining -= Time.deltaTime;
                direction = transform.forward;
                speed *= 1.65f;
            }
            Move(direction * speed);
        }

        public void BeginShot()
        {
            isChargingShot = true;
            shotCharge = 0.15f;
        }

        public void ReleaseShot()
        {
            if (!isChargingShot)
            {
                return;
            }

            isChargingShot = false;
            BallPhysics ball = GetControllableBall();
            if (ball != null)
            {
                float speed = Mathf.Lerp(minimumShotSpeed, maximumShotSpeed, shotCharge);
                float lift = Mathf.Lerp(0.8f, maximumShotLift, shotCharge);
                ball.Kick(transform.forward, speed, lift);
            }
            shotCharge = 0f;
        }

        public void Shoot()
        {
            BeginShot();
            shotCharge = 0.65f;
            ReleaseShot();
        }

        public void Pass()
        {
            BallPhysics ball = GetControllableBall();
            if (ball != null)
            {
                ball.Kick(transform.forward, passSpeed, 0.55f);
            }
        }

        public void BeginSprint()
        {
            isSprinting = true;
        }

        public void EndSprint()
        {
            isSprinting = false;
        }

        public void Tackle()
        {
            if (tackleRemaining <= 0f)
            {
                tackleRemaining = 0.22f;
            }
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = true;
            verticalVelocity = 0f;
            shotCharge = 0f;
            isChargingShot = false;
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

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
            if (movementCamera == null)
            {
                return new Vector3(input.x, 0f, input.y);
            }

            Vector3 forward = Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(movementCamera.right, Vector3.up).normalized;
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }

        private BallPhysics GetControllableBall()
        {
            if (controlledBall == null)
            {
                return null;
            }
            return Vector3.Distance(transform.position, controlledBall.transform.position) <= controlRadius
                ? controlledBall
                : null;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            BallPhysics ball = hit.collider.GetComponent<BallPhysics>();
            if (ball != null)
            {
                Vector3 pushDirection = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up).normalized;
                ball.Body.AddForce(pushDirection * 0.7f, ForceMode.VelocityChange);
            }
        }
    }
}

