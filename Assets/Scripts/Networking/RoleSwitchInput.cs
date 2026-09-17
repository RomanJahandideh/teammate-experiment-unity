using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Teammate.Networking
{
    /// <summary>Tab cycles this player's own role Prep → Cook → Runner → Prep, mid-mission ("players could switch tasks").</summary>
    [RequireComponent(typeof(TeammatePhysioNetworkPlayer))]
    public class RoleSwitchInput : NetworkBehaviour
    {
        private static readonly PlayerRole[] CycleOrder = { PlayerRole.Prep, PlayerRole.Cook, PlayerRole.Runner };
        private TeammatePhysioNetworkPlayer _player;

        void Awake() => _player = GetComponent<TeammatePhysioNetworkPlayer>();

        void Update()
        {
            if (!IsOwner) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame) return;

            int currentIndex = System.Array.IndexOf(CycleOrder, _player.Role.Value);
            var next = CycleOrder[(currentIndex + 1 + CycleOrder.Length) % CycleOrder.Length];
            _player.RequestRoleSwitch(next);
        }
    }
}
