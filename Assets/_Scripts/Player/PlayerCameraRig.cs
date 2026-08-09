using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Player
{
    /// <summary>
    /// Smooth third-person orbit camera (GDD 6, Phase 3). One rig exists in the scene and
    /// latches onto whichever body is the local player.
    ///
    /// Includes a wall-collision pull-in so the camera does not clip through the house —
    /// which matters a lot in a game about standing inside small rooms.
    /// </summary>
    public class PlayerCameraRig : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);

        [Header("Orbit")]
        [SerializeField] private float distance = 5.2f;

        [Tooltip("Sideways offset so the character sits off-centre and does not block the view.")]
        [SerializeField] private float shoulderOffset = 0.65f;

        [Tooltip("Never pull closer than this. Below roughly 1.8m the camera is inside the character.")]
        [SerializeField] private float minDistance = 2.1f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float mouseSensitivity = 2.6f;
        [SerializeField] private float followSharpness = 16f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.28f;
        [SerializeField] private LayerMask collisionMask = GameLayers.CameraCollisionMask;

        private Transform _target;
        private float _yaw;
        private float _pitch = 14f;
        private float _currentDistance;

        public Transform CameraTransform => _camera != null ? _camera.transform : transform;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponentInChildren<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _currentDistance = distance;
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                SnapToTarget();
            }
        }

        // Deliberately no cursor handling on enable. The rig exists from the moment the
        // scene loads, but the lobby is showing then — grabbing the cursor here would
        // make the menu's own buttons unclickable. Cursor state follows the game state
        // instead, driven by LobbyUI and the end-of-round screens.

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            if (_target == null)
            {
                return;
            }

            // Any open menu releases the cursor; don't spin the camera while the player
            // is clicking through the furniture catalog.
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                _yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
                _pitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }
        }

        private void LateUpdate()
        {
            if (_target == null || _camera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // Offset the pivot to the shoulder so the character does not sit dead centre
            // with their own head over everything they are trying to aim at. The offset is
            // swept rather than applied blindly: standing with your shoulder against a wall
            // would otherwise put the pivot on the far side of it, and everything below
            // would then measure its distance from inside the neighbouring room.
            Vector3 head = _target.position + pivotOffset;
            Vector3 side = rotation * Vector3.right;
            float sideDistance = shoulderOffset;

            if (Physics.SphereCast(head, collisionRadius, side, out RaycastHit sideHit, shoulderOffset,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                sideDistance = Mathf.Max(0f, sideHit.distance - 0.05f);
            }

            Vector3 pivot = head + side * sideDistance;

            float desiredDistance = distance;
            Vector3 back = rotation * Vector3.back;

            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Clamped at minDistance rather than 0.6. In rooms this size the old floor
                // let a wall behind you drag the camera inside your own torso — you ended up
                // looking out through the back of the model, seeing the arm and tool poking
                // out in front and nothing else, which reads as "there is no character".
                desiredDistance = Mathf.Max(minDistance, hit.distance - 0.05f);
            }

            // Snap in fast when a wall appears, ease out slowly when it clears —
            // the reverse feels like the camera is dragging through geometry.
            float sharpness = desiredDistance < _currentDistance ? 40f : 8f;
            _currentDistance = Mathf.Lerp(
                _currentDistance, desiredDistance, 1f - Mathf.Exp(-sharpness * Time.deltaTime));

            Vector3 desiredPosition = pivot + back * _currentDistance;

            transform.position = Vector3.Lerp(
                transform.position, desiredPosition, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            transform.rotation = rotation;
        }

        private void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _target.position + pivotOffset
                                 + rotation * Vector3.right * shoulderOffset
                                 + rotation * Vector3.back * distance;
            transform.rotation = rotation;
            _currentDistance = distance;
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
