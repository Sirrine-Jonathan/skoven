using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Flowstate.Network
{
    /// <summary>
    /// Manages the WebSocket connection to the companion app.
    /// Thread-safe: messages are queued and dispatched on the Unity main thread.
    /// </summary>
    public class WebSocketClient : MonoBehaviour
    {
        [Header("Connection")]
        [Tooltip("IP address of the machine running the companion app.")]
        public string hostAddress = "192.168.1.100";
        public int port = 8765;
        public float reconnectDelaySec = 3f;

        public event Action<string> OnMessageReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _incomingQueue = new();
        private readonly ConcurrentQueue<string> _outgoingQueue = new();
        private bool _reconnecting;

        // ── Unity lifecycle ────────────────────────────────────────────────

        private async void Start()
        {
            await ConnectAsync();
        }

        private void Update()
        {
            // Dispatch all queued messages on the main thread
            while (_incomingQueue.TryDequeue(out var msg))
            {
                OnMessageReceived?.Invoke(msg);
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _ws?.Dispose();
        }

        // ── Connection ─────────────────────────────────────────────────────

        public async Task ConnectAsync()
        {
            if (_reconnecting) return;
            _reconnecting = true;

            while (true)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                _ws?.Dispose();
                _ws = new ClientWebSocket();

                try
                {
                    var uri = new Uri($"ws://{hostAddress}:{port}");
                    Debug.Log($"[Flowstate] Connecting to {uri}...");
                    await _ws.ConnectAsync(uri, _cts.Token);
                    Debug.Log("[Flowstate] Connected to companion app.");
                    _reconnecting = false;
                    _incomingQueue.Enqueue("{\"_meta\":\"connected\"}");
                    OnConnected?.Invoke();

                    // Start send/receive loops concurrently
                    var recvTask = ReceiveLoopAsync(_cts.Token);
                    var sendTask = SendLoopAsync(_cts.Token);
                    await Task.WhenAny(recvTask, sendTask);

                    OnDisconnected?.Invoke();
                    Debug.Log("[Flowstate] Disconnected from companion app.");
                }
                catch (OperationCanceledException)
                {
                    return; // App is shutting down
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Flowstate] Connection failed: {ex.Message}. Retrying in {reconnectDelaySec}s...");
                }

                _reconnecting = true;
                await Task.Delay(TimeSpan.FromSeconds(reconnectDelaySec), _cts.Token);
            }
        }

        // ── Send ───────────────────────────────────────────────────────────

        public void Send(string json)
        {
            _outgoingQueue.Enqueue(json);
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                if (_outgoingQueue.TryDequeue(out var msg))
                {
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    await _ws.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        ct
                    );
                }
                else
                {
                    await Task.Delay(16, ct); // ~60fps polling
                }
            }
        }

        // ── Receive ────────────────────────────────────────────────────────

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[64 * 1024]; // 64 KB receive buffer
            var messageBuffer = new StringBuilder();

            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                }
                catch (WebSocketException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                    break;
                }

                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    _incomingQueue.Enqueue(messageBuffer.ToString());
                    messageBuffer.Clear();
                }
            }
        }
    }
}
