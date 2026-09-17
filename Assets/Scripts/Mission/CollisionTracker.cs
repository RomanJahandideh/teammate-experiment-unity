using Teammate.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Teammate.Mission
{
    /// <summary>
    /// Counts how often this player bumps into a teammate (collision_blocking_count) —
    /// natural in a shared kitchen with clustered resources (the Shared Congestion
    /// layout especially). Runs on the owning client, since CharacterController.Move
    /// collision callbacks only fire for whoever is actually driving the movement; the
    /// count itself is a server-write NetworkVariable so MissionController can read it
    /// authoritatively at mission end.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkObject))]
    public class CollisionTracker : NetworkBehaviour
    {
        [Tooltip("Minimum seconds between counting two bumps against the same teammate, so one shove doesn't count a dozen times.")]
        public float perContactCooldownSeconds = 1f;

        public NetworkVariable<int> BlockingCollisionCount = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float _cooldownRemaining;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsOwner) return;
            if (_cooldownRemaining > 0f) return;

            var otherPlayer = hit.collider.GetComponentInParent<PlayerMotor>();
            if (otherPlayer == null || otherPlayer.gameObject == gameObject) return;

            _cooldownRemaining = perContactCooldownSeconds;
            ReportCollisionServerRpc();
        }

        void Update()
        {
            if (_cooldownRemaining > 0f)
                _cooldownRemaining -= Time.deltaTime;
        }

        [ServerRpc]
        private void ReportCollisionServerRpc(ServerRpcParams rpcParams = default)
        {
            BlockingCollisionCount.Value++;
        }

        public void ServerResetForNewMission()
        {
            if (IsServer) BlockingCollisionCount.Value = 0;
        }
    }
}
