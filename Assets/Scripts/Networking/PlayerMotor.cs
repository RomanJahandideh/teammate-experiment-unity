using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Teammate.Networking
{
    /// <summary>
    /// Minimal top-down movement + interact action for the cooperative-cooking prototype.
    /// This is the "position and action data" the paper says the physiological cue rides
    /// alongside on the same network channel — see TeammatePhysioNetworkPlayer on this
    /// same GameObject. Station/ingredient interaction logic (chopping, plating, delivery,
    /// scoring) is intentionally out of scope here; InteractRequested is the hook a
    /// separate cooking-mechanics layer would subscribe to.
    ///
    /// Requires a NetworkTransform (or ClientNetworkTransform) component on this prefab,
    /// set to owner/client authoritative, so only the owning client's movement here needs
    /// to run — Netcode handles replicating the resulting transform to everyone else.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        [Header("Movement")]
        public float moveSpeedMetersPerSecond = 3.5f;
        public float rotationDegreesPerSecond = 540f;
        public float gravity = -9.81f;

        [Header("Networked action state")]
        [Tooltip("True for a short window after an Interact press; read by remote clients to drive interaction animations/VFX.")]
        public NetworkVariable<bool> IsInteracting = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Tooltip("Fires locally (on every client) whenever this player interacts, with this object as the source. Hook cooking-station logic here.")]
        public System.Action<PlayerMotor> InteractRequested;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _interactTimer;
        private const float InteractHoldSeconds = 0.25f;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (!IsOwner) return; // Only the owning client drives its own player; others read replicated transform/state.

            HandleMovement();
            HandleInteract();
        }

        private void HandleMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 axis = Vector2.zero;
            if (keyboard.wKey.isPressed) axis.y += 1f;
            if (keyboard.sKey.isPressed) axis.y -= 1f;
            if (keyboard.dKey.isPressed) axis.x += 1f;
            if (keyboard.aKey.isPressed) axis.x -= 1f;
            axis = Vector2.ClampMagnitude(axis, 1f);

            Vector3 moveDir = new Vector3(axis.x, 0f, axis.y);

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -0.5f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = moveDir * moveSpeedMetersPerSecond;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationDegreesPerSecond * Time.deltaTime);
            }
        }

        private void HandleInteract()
        {
            var keyboard = Keyboard.current;
            bool pressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;

            if (pressed)
            {
                _interactTimer = InteractHoldSeconds;
                IsInteracting.Value = true;
                InteractRequested?.Invoke(this);
                NotifyInteractServerRpc();
            }

            if (_interactTimer > 0f)
            {
                _interactTimer -= Time.deltaTime;
                if (_interactTimer <= 0f)
                    IsInteracting.Value = false;
            }
        }

        /// <summary>
        /// Server-side notification hook for scripted pressure events / mission logic
        /// that needs to know an interaction happened (e.g. "appropriate support action"
        /// logging for H2.3). No gameplay state changes here by default.
        /// </summary>
        [ServerRpc]
        private void NotifyInteractServerRpc(ServerRpcParams rpcParams = default)
        {
            // Intentionally empty: hook mission/pressure-event scoring here if/when the
            // cooking-mechanics layer is added. Left as an explicit extension point
            // rather than a silent no-op comment buried in PlayerMotor's Update loop.
        }
    }
}
