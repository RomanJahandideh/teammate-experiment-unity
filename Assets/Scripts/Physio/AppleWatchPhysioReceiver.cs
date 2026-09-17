using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Teammate.Physio
{
    /// <summary>
    /// Local-machine bridge from an Apple Watch (via a companion iOS app forwarding
    /// HealthKit HR + RMSSD over the LAN) into this Unity client, one instance per
    /// player's own machine. Same UDP + background-thread + JSON pattern as
    /// BioTerrarium's UDPHeartRateReceiver, extended with an RMSSD HRV field and a
    /// send-side timestamp so sensor-to-interface latency can be logged (RQ2.4).
    ///
    /// Expected JSON packet from the bridge app:
    /// {
    ///   "heart_rate_bpm": 74.0,
    ///   "hrv_rmssd_ms": 48.2,
    ///   "device": "AppleWatch",
    ///   "timestamp": 1758310976.573597   // seconds since epoch, when the bridge sent it
    /// }
    ///
    /// All processing stays on this client, per the paper's ethics note: raw values
    /// never leave this machine. Only LocalPhysioController's classified HRState/HRVState
    /// are ever handed to the networking layer.
    /// </summary>
    public class AppleWatchPhysioReceiver : MonoBehaviour
    {
        [Serializable]
        private class WatchPhysioPacket
        {
            public double heart_rate_bpm;
            public double hrv_rmssd_ms;
            public string device;
            public double timestamp;
        }

        [Header("UDP")]
        [Tooltip("Must match the port the iOS bridge app on this participant's phone/watch is configured to send to.")]
        public int port = 53880;

        [Header("Live status (read-only)")]
        public double heartRateBpm;
        public double hrvRmssdMs;
        public bool gotPacket;
        public float lastSampleAgeSeconds;
        public double lastLatencyMs;
        public string lastSenderIP = "";

        /// <summary>Fires on the main thread for every accepted sample: (hrBpm, hrvRmssdMs, latencyMs).</summary>
        public event Action<double, double, double> OnSample;

        /// <summary>Fires if no packet has been received for longer than <see cref="dropoutThresholdSeconds"/>.</summary>
        public event Action OnDropoutDetected;

        [Header("Reliability")]
        public float dropoutThresholdSeconds = 3f;

        private UdpClient _udp;
        private Thread _thread;
        private volatile bool _running;
        private float _timeSinceLastSample;
        private bool _dropoutFlagged;

        void Start()
        {
            if (!UnityMainThreadDispatcher.Exists())
            {
                var dispatcherGO = new GameObject("MainThreadDispatcher");
                dispatcherGO.AddComponent<UnityMainThreadDispatcher>();
            }

            try
            {
                _udp = new UdpClient(port);
                _running = true;
                _thread = new Thread(ListenLoop) { IsBackground = true };
                _thread.Start();
                Debug.Log($"[AppleWatchPhysioReceiver] Listening on UDP port {port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppleWatchPhysioReceiver] UDP init failed on port {port}: {e.Message}");
            }
        }

        void Update()
        {
            _timeSinceLastSample += Time.deltaTime;
            lastSampleAgeSeconds = _timeSinceLastSample;

            if (gotPacket && _timeSinceLastSample > dropoutThresholdSeconds && !_dropoutFlagged)
            {
                _dropoutFlagged = true;
                OnDropoutDetected?.Invoke();
            }
        }

        private void ListenLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);

            while (_running)
            {
                try
                {
                    _udp.Client.ReceiveTimeout = 1000;
                    var data = _udp.Receive(ref any);
                    var json = Encoding.UTF8.GetString(data);
                    double receivedAtEpoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

                    var packet = JsonUtility.FromJson<WatchPhysioPacket>(json);
                    if (packet == null || packet.heart_rate_bpm <= 0)
                    {
                        Debug.LogWarning($"[AppleWatchPhysioReceiver] Failed to parse packet: {json}");
                        continue;
                    }

                    double latencyMs = packet.timestamp > 0
                        ? Math.Max(0.0, (receivedAtEpoch - packet.timestamp) * 1000.0)
                        : -1.0;
                    string senderIp = any.Address.ToString();

                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        heartRateBpm = packet.heart_rate_bpm;
                        hrvRmssdMs = packet.hrv_rmssd_ms;
                        lastLatencyMs = latencyMs;
                        lastSenderIP = senderIp;
                        gotPacket = true;
                        _dropoutFlagged = false;
                        _timeSinceLastSample = 0f;

                        OnSample?.Invoke(heartRateBpm, hrvRmssdMs, latencyMs);
                    });
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (_running)
                        Debug.LogError($"[AppleWatchPhysioReceiver] UDP receive error: {e.Message}");
                }
            }
        }

        void OnDestroy()
        {
            _running = false;
            try { _udp?.Close(); } catch (Exception e) { Debug.LogWarning($"[AppleWatchPhysioReceiver] Error closing socket: {e.Message}"); }
            try { if (_thread != null && _thread.IsAlive) _thread.Join(2000); } catch { /* best effort */ }
        }

        [ContextMenu("Send Test Sample")]
        private void SendTestSample()
        {
            heartRateBpm = 78;
            hrvRmssdMs = 46;
            gotPacket = true;
            OnSample?.Invoke(heartRateBpm, hrvRmssdMs, 0);
        }
    }
}
