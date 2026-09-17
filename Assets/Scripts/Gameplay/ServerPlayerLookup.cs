using Unity.Netcode;

namespace Teammate.Gameplay
{
    /// <summary>Server-side helper: every station RPC needs "which player called this?" — this is the one place that logic lives.</summary>
    public static class ServerPlayerLookup
    {
        public static PlayerCarrier GetCarrier(ulong clientId)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return null;

            var playerObject = client.PlayerObject;
            if (playerObject == null) return null;

            return playerObject.GetComponent<PlayerCarrier>();
        }

        public static Teammate.Networking.TeammatePhysioNetworkPlayer GetPhysioPlayer(ulong clientId)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                return null;

            var playerObject = client.PlayerObject;
            return playerObject == null ? null : playerObject.GetComponent<Teammate.Networking.TeammatePhysioNetworkPlayer>();
        }
    }
}
