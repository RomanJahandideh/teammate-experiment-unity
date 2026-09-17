using Teammate.Physio;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Teammate.Networking
{
    /// <summary>
    /// One per player. Owns replication of exactly the data the paper says crosses the
    /// network: "the same multiplayer network channel already carrying position and
    /// action data" (handled by PlayerMotor on this same object) plus "only that
    /// player's current [HR/HRV] state" (handled here).
    ///
    /// Only two small enums are ever written to the network — never a raw bpm/RMSSD
    /// value — which is what keeps a teammate's client from ever seeing the wearer's
    /// physiological time series directly.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class TeammatePhysioNetworkPlayer : NetworkBehaviour
    {
        [Header("Local-only source (owner client only)")]
        [Tooltip("Assigned automatically on the owning client if left empty; found via GetComponent.")]
        public LocalPhysioController localPhysio;

        [Header("Identity")]
        public NetworkVariable<FixedString32Bytes> DisplayName = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<PlayerRole> Role = new NetworkVariable<PlayerRole>(
            PlayerRole.Unassigned, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>
        /// "Roles were introduced during training but remained flexible: players could
        /// switch tasks mid-mission, logged as strategy-change events." Counts successful
        /// switches for the mission's strategy_changes_count.
        /// </summary>
        public NetworkVariable<int> StrategyChangeCount = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Replicated physiological cue state")]
        public NetworkVariable<HRState> DisplayedHrState = new NetworkVariable<HRState>(
            HRState.HR1_RestingLowActivation, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public NetworkVariable<HRVState> DisplayedHrvState = new NetworkVariable<HRVState>(
            HRVState.V2_StableVariability, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// Numeric values only exist locally on the owner and are sent only while the
        /// active study condition is Numeric Cue (see ConditionManager). Kept out of a
        /// NetworkVariable by default so the icon-cue and no-cue conditions never expose
        /// a raw number on the wire, matching the paper's "upper bound on explicit
        /// disclosure" framing of the numeric condition as the one arm that *does* share
        /// exact values, by explicit design, not by default.
        /// </summary>
        public NetworkVariable<float> NumericHrBpmIfDisclosed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public NetworkVariable<float> NumericHrvMsIfDisclosed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner) return;

            if (localPhysio == null)
                localPhysio = GetComponent<LocalPhysioController>() ?? GetComponentInParent<LocalPhysioController>();

            if (localPhysio == null)
            {
                Debug.LogWarning("[TeammatePhysioNetworkPlayer] No LocalPhysioController found on the owner; " +
                                  "physiological cue will not replicate for this player.");
                return;
            }

            localPhysio.OnDisplayedStateChanged += HandleLocalStateChanged;

            if (IsServer)
                DisplayName.Value = new FixedString32Bytes($"Player{OwnerClientId}");
            else
                SubmitDisplayNameServerRpc($"Player{OwnerClientId}");
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && localPhysio != null)
                localPhysio.OnDisplayedStateChanged -= HandleLocalStateChanged;

            base.OnNetworkDespawn();
        }

        private void HandleLocalStateChanged(HRState hr, HRVState hrv)
        {
            // Owner-write NetworkVariables can be set directly on the owning client;
            // Netcode replicates the change to the server and all observers.
            DisplayedHrState.Value = hr;
            DisplayedHrvState.Value = hrv;
        }

        /// <summary>Call from the owner client to publish raw numeric values, but only while ConditionManager reports NumericCue.</summary>
        public void PublishNumericIfDisclosed(bool disclose, double hrBpm, double hrvMs)
        {
            if (!IsOwner) return;

            NumericHrBpmIfDisclosed.Value = disclose ? (float)hrBpm : 0f;
            NumericHrvMsIfDisclosed.Value = disclose ? (float)hrvMs : 0f;
        }

        [ServerRpc]
        private void SubmitDisplayNameServerRpc(string desiredName, ServerRpcParams rpcParams = default)
        {
            DisplayName.Value = new FixedString32Bytes(desiredName);
        }

        /// <summary>Call from the owner client (e.g. a Tab-to-cycle-role input) to request switching roles mid-mission.</summary>
        public void RequestRoleSwitch(PlayerRole newRole)
        {
            if (!IsOwner) return;
            RequestRoleSwitchServerRpc(newRole);
        }

        [ServerRpc]
        private void RequestRoleSwitchServerRpc(PlayerRole newRole, ServerRpcParams rpcParams = default)
        {
            if (newRole == PlayerRole.Unassigned || newRole == Role.Value) return;

            Role.Value = newRole;
            StrategyChangeCount.Value++;
        }
    }
}
