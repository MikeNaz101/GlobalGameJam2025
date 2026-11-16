using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ishana.CameraSystem
{
    /// <summary>
    /// Orbit-style 3rd-person camera for Ishana, built on the Cinemachine virtual cameras already in the
    /// player prefab.
    ///
    /// - Reads the "Look" action from the player's PlayerInput, so mouse AND gamepad right stick both work.
    /// - Rotates a CameraTarget (yaw/pitch) that the virtual cameras follow with Cinemachine3rdPersonFollow,
    ///   which also gives camera collision (no more clipping into trees and rocks).
    /// - Pulls in over the shoulder while a shot is charging (aim mode).
    /// - Adds CameraShake to the virtual cameras so combat feedback can shake the view.
    ///
    /// Added to the player at runtime by IshanaBootstrap if it isn't already on the prefab. Add it to the
    /// player prefab yourself to tune the values in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // Run LateUpdate before CinemachineBrain
    [AddComponentMenu("Ishana/Third Person Camera Controller")]
    public class ThirdPersonCameraController : MonoBehaviour
    {
        /// <summary>Global look sensitivity multiplier, for a future settings menu.</summary>
        public static float SensitivityMultiplier = 1f;

        [Header("References (found automatically when empty)")]
        public Transform cameraTarget;
        public CinemachineVirtualCamera gameplayCamera;
        public CinemachineVirtualCamera aimCamera;
        public PlayerInput playerInput;
        public string lookActionName = "Look";

        [Header("Look")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full right-stick deflection.")]
        public float gamepadSensitivity = 220f;
        public bool invertY = false;
        public float minPitch = -35f;
        public float maxPitch = 70f;
        [Tooltip("Pitch the camera starts at.")]
        public float startPitch = 12f;

        [Header("Framing (for a 2-unit-tall character; scaled to the real size if enabled)")]
        public bool scaleWithCharacterSize = true;
        [Tooltip("Configure the Cinemachine cameras from the values below when the game starts.")]
        public bool configureVirtualCameras = true;
        public float cameraDistance = 5.5f;
        public Vector3 shoulderOffset = new Vector3(0.6f, 0.25f, 0f);
        public float verticalArmLength = 0.35f;
        [Tooltip("Follow lag per axis (seconds). Small values feel responsive, larger values feel floaty.")]
        public Vector3 followDamping = new Vector3(0.1f, 0.3f, 0.2f);
        public float fieldOfView = 60f;

        [Header("Aim Mode (while charging a shot)")]
        public float aimDistance = 3.2f;
        public Vector3 aimShoulderOffset = new Vector3(0.9f, 0.35f, 0f);
        public float aimFieldOfView = 48f;
        [Tooltip("How fast the camera moves into and out of aim mode.")]
        public float aimBlendSpeed = 5f;

        [Header("Collision")]
        [Tooltip("Layers the camera won't pass through. Leave as Nothing to use everything except the player's own layer.")]
        public LayerMask collisionLayers = 0;
        public float cameraRadius = 0.25f;
        [Tooltip("Objects with this tag never block the camera.")]
        public string ignoreTag = "Player";

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public bool IsAiming => _aimBlend > 0.5f;

        InputAction _lookAction;
        PlayerShooting _shooting;
        Cinemachine3rdPersonFollow _gameplayBody;
        float _yaw, _pitch, _aimBlend, _sizeScale = 1f;

        void Awake()
        {
            if (playerInput == null) playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
                _lookAction = playerInput.actions.FindAction(lookActionName, throwIfNotFound: false);

            _shooting = GetComponentInChildren<PlayerShooting>();
            if (_shooting != null)
            {
                if (gameplayCamera == null) gameplayCamera = _shooting.gameplayVCam;
                if (aimCamera == null) aimCamera = _shooting.teleportAimVCam;
            }

            // The old rotator turned the body with the mouse; movement now turns the body instead.
            var player = GetComponent<PlayerStateManager>();
            var oldRotator = GetComponent<PlayerBodyRotator>();
            if (oldRotator != null && (player == null || player.cameraRelativeMovement)) oldRotator.enabled = false;

            var cc = GetComponent<CharacterController>();
            float localHeight = cc != null ? cc.height : 2f;
            if (scaleWithCharacterSize) _sizeScale = Mathf.Max(0.25f, localHeight * transform.lossyScale.y / 2f);

            if (cameraTarget == null)
            {
                cameraTarget = new GameObject("CameraTarget").transform;
                cameraTarget.SetParent(transform, false);
                float centerY = cc != null ? cc.center.y : 1f;
                cameraTarget.localPosition = new Vector3(0f, centerY + localHeight * 0.35f, 0f);
            }

            if (collisionLayers.value == 0)
                collisionLayers = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);

            _yaw = transform.eulerAngles.y;
            _pitch = startPitch;

            if (configureVirtualCameras)
            {
                _gameplayBody = Configure(gameplayCamera, cameraDistance, shoulderOffset, fieldOfView);
                Configure(aimCamera, aimDistance, aimShoulderOffset, aimFieldOfView);
            }
            else if (gameplayCamera != null)
            {
                _gameplayBody = gameplayCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            }

            EnsureShake(gameplayCamera);
            EnsureShake(aimCamera);

            if (gameplayCamera == null)
                Debug.LogWarning("ThirdPersonCameraController: no gameplay CinemachineVirtualCamera found. Assign one in the Inspector.", this);
        }

        Cinemachine3rdPersonFollow Configure(CinemachineVirtualCamera vcam, float distance, Vector3 shoulder, float fov)
        {
            if (vcam == null) return null;

            vcam.Follow = cameraTarget;
            vcam.LookAt = null;

            // Remove the old POV/Composer aim: the 3rd-person body takes its rotation from the CameraTarget.
            if (vcam.GetCinemachineComponent<CinemachinePOV>() != null) vcam.DestroyCinemachineComponent<CinemachinePOV>();
            if (vcam.GetCinemachineComponent<CinemachineComposer>() != null) vcam.DestroyCinemachineComponent<CinemachineComposer>();

            var body = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (body == null) body = vcam.AddCinemachineComponent<Cinemachine3rdPersonFollow>(); // Replaces the Transposer

            body.CameraDistance = distance * _sizeScale;
            body.ShoulderOffset = shoulder * _sizeScale;
            body.VerticalArmLength = verticalArmLength * _sizeScale;
            body.CameraSide = 1f;
            body.Damping = followDamping;
            body.CameraCollisionFilter = collisionLayers;
            body.IgnoreTag = ignoreTag;
            body.CameraRadius = cameraRadius * _sizeScale;
            body.DampingIntoCollision = 0.05f;
            body.DampingFromCollision = 0.4f;

            var lens = vcam.m_Lens;
            lens.FieldOfView = fov;
            vcam.m_Lens = lens;

            return body;
        }

        static void EnsureShake(CinemachineVirtualCamera vcam)
        {
            if (vcam != null && vcam.GetComponent<CameraShake>() == null) vcam.gameObject.AddComponent<CameraShake>();
        }

        void LateUpdate()
        {
            if (cameraTarget == null) return;

            if (!PauseMenuController.GameIsPaused)
            {
                Vector2 look = ReadLook(out bool fromMouse);
                float scale = (fromMouse ? mouseSensitivity : gamepadSensitivity * Time.unscaledDeltaTime) * SensitivityMultiplier;
                _yaw += look.x * scale;
                _pitch += (invertY ? 1f : -1f) * look.y * scale;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
                _yaw = Mathf.Repeat(_yaw, 360f);
            }

            // World-space rotation so the body turning underneath doesn't drag the camera around
            cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            UpdateAimMode();
        }

        Vector2 ReadLook(out bool fromMouse)
        {
            fromMouse = true;
            if (_lookAction != null)
            {
                var control = _lookAction.activeControl;
                fromMouse = control == null || control.device is Pointer;
                return _lookAction.ReadValue<Vector2>();
            }
            // Fallback when there is no PlayerInput: read the mouse directly
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        }

        void UpdateAimMode()
        {
            if (gameplayCamera == null || _gameplayBody == null) return;

            float target = _shooting != null && _shooting.IsCharging ? 1f : 0f;
            _aimBlend = Mathf.MoveTowards(_aimBlend, target, aimBlendSpeed * Time.unscaledDeltaTime);
            float t = Mathf.SmoothStep(0f, 1f, _aimBlend);

            _gameplayBody.CameraDistance = Mathf.Lerp(cameraDistance, aimDistance, t) * _sizeScale;
            _gameplayBody.ShoulderOffset = Vector3.Lerp(shoulderOffset, aimShoulderOffset, t) * _sizeScale;

            var lens = gameplayCamera.m_Lens;
            lens.FieldOfView = Mathf.Lerp(fieldOfView, aimFieldOfView, t);
            gameplayCamera.m_Lens = lens;
        }

        /// <summary>Snap the camera behind a direction (e.g. after a teleport or respawn).</summary>
        public void SnapBehind(Vector3 forward)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) return;
            _yaw = Quaternion.LookRotation(forward).eulerAngles.y;
            _pitch = startPitch;
        }
    }
}
