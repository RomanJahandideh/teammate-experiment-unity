using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Teammate.Networking
{
    /// <summary>
    /// Lab-session connection screen for a 3-player LAN game: one machine starts a Host,
    /// the other two type in its LAN IP and Join. Assigns Prep/Cook/Runner round-robin
    /// as players connect, matching "organized into 12 teams of three players in soft
    /// Prep, Cook, and Runner roles."
    ///
    /// Deliberately plain (OnGUI, direct IP entry) rather than a matchmaking/relay
    /// service: Study 2 ran all three players in the same physical session, and the
    /// paper's ethics note keeps processing on-device rather than on a remote server, so
    /// a direct LAN connection is the natural fit and needs nothing beyond Unity
    /// Transport's default UDP socket.
    /// </summary>
    public class TeammateNetworkBootstrap : MonoBehaviour
    {
        [Header("Config")]
        public ushort port = 7777;
        [Tooltip("Prefab with NetworkObject, PlayerMotor, TeammatePhysioNetworkPlayer, LocalPhysioController, AppleWatchPhysioReceiver, BaselineCalibrator.")]
        public GameObject playerPrefab;

        private string _joinAddress = "127.0.0.1";
        private string _statusMessage = "";
        private static readonly PlayerRole[] RoleOrder = { PlayerRole.Prep, PlayerRole.Cook, PlayerRole.Runner };
        private int _nextRoleIndex;

        void Awake()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[TeammateNetworkBootstrap] No NetworkManager in the scene. Add one (with a UnityTransport component) before Play.");
                return;
            }

            if (playerPrefab != null)
                nm.NetworkConfig.PlayerPrefab = playerPrefab;

            nm.OnServerStarted += () => _statusMessage = $"Hosting on {GetLocalIPAddress()}:{port}";
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += id => _statusMessage = $"Client {id} disconnected";
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            var role = RoleOrder[_nextRoleIndex % RoleOrder.Length];
            _nextRoleIndex++;

            if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId)
                    .TryGetComponent(out TeammatePhysioNetworkPlayer physioPlayer))
            {
                physioPlayer.Role.Value = role;
            }

            _statusMessage = $"Client {clientId} connected as {role}";
            Debug.Log($"[TeammateNetworkBootstrap] {_statusMessage}");
        }

        void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            const int w = 260;
            GUI.Box(new Rect(10, 10, w, 130), "Teammate Session");

            if (!nm.IsClient && !nm.IsServer)
            {
                if (GUI.Button(new Rect(20, 40, w - 20, 30), "Host (start session)"))
                    StartHost();

                _joinAddress = GUI.TextField(new Rect(20, 80, w - 90, 25), _joinAddress);
                if (GUI.Button(new Rect(w - 60, 80, 70, 25), "Join"))
                    StartClient(_joinAddress);
            }
            else
            {
                GUI.Label(new Rect(20, 40, w - 20, 60), _statusMessage);
                if (GUI.Button(new Rect(20, 95, w - 20, 25), "Disconnect"))
                    nm.Shutdown();
            }
        }

        public void StartHost()
        {
            ConfigureTransport(null);
            NetworkManager.Singleton.StartHost();
        }

        public void StartClient(string address)
        {
            ConfigureTransport(address);
            NetworkManager.Singleton.StartClient();
        }

        private void ConfigureTransport(string connectAddress)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[TeammateNetworkBootstrap] NetworkManager has no UnityTransport component.");
                return;
            }

            // Host: bind to all interfaces (0.0.0.0) but still advertise/listen on `port`.
            // Client: dial the address the experimenter typed in.
            string address = string.IsNullOrEmpty(connectAddress) ? "0.0.0.0" : connectAddress;
            transport.SetConnectionData(address, port);
        }

        private static string GetLocalIPAddress()
        {
            // Same lookup pattern as BioTerrarium's UDPHeartRateReceiver.GetLocalIPAddress().
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            return "Unknown";
        }
    }
}
