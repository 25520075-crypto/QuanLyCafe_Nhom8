using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Protocol;

namespace QuanLyCafe_Nhom8.Network
{
    public class CafeNetworkClient
    {
        private static readonly Lazy<CafeNetworkClient> _instance =
            new Lazy<CafeNetworkClient>(() => new CafeNetworkClient());
        public static CafeNetworkClient Instance => _instance.Value;

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<PacketMessage>> _pendingRequests =
            new ConcurrentDictionary<string, TaskCompletionSource<PacketMessage>>();

        public string ServerIp { get; private set; } = "127.0.0.1";
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;
        public UserDTO CurrentUser { get; set; }

        // Sự kiện nhận thông báo Broadcast từ Server (cập nhật bàn, menu, bếp KDS)
        public event Action<PacketMessage> OnServerPush;
        public event Action<string> OnDisconnected;

        private CafeNetworkClient() { }

        /// <summary>
        /// Gửi gói tin UDP Broadcast ra mạng LAN để tự động tìm IP của CafeServer
        /// </summary>
        public async Task<string> DiscoverServerIpAsync(int timeoutMs = 2000)
        {
            using (UdpClient udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                byte[] reqBytes = Encoding.UTF8.GetBytes(FramingHelper.DiscoveryRequest);
                IPEndPoint broadcastEp = new IPEndPoint(IPAddress.Broadcast, FramingHelper.DefaultUdpPort);

                await udp.SendAsync(reqBytes, reqBytes.Length, broadcastEp);

                Task<UdpReceiveResult> receiveTask = udp.ReceiveAsync();
                Task completed = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs));

                if (completed == receiveTask)
                {
                    UdpReceiveResult result = await receiveTask;
                    string reply = Encoding.UTF8.GetString(result.Buffer).Trim();
                    if (reply.StartsWith(FramingHelper.DiscoveryResponsePrefix))
                    {
                        return result.RemoteEndPoint.Address.ToString();
                    }
                }
            }
            // Nếu chạy cùng 1 máy hoặc router chặn broadcast thì fallback về localhost
            return "127.0.0.1";
        }

        /// <summary>
        /// Kết nối tới CafeServer (tự dò IP nếu truyền null/rỗng)
        /// </summary>
        public async Task ConnectAsync(string manualIp = null)
        {
            if (IsConnected) return;

            ServerIp = string.IsNullOrWhiteSpace(manualIp)
                ? await DiscoverServerIpAsync()
                : manualIp.Trim();

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(ServerIp, FramingHelper.DefaultTcpPort);
            _stream = _tcpClient.GetStream();
            _cts = new CancellationTokenSource();

            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Gửi Request lên Server và chờ dữ liệu trả về đúng kiểu T
        /// </summary>
        public async Task<T> SendRequestAsync<T>(MsgType type, object payload = null, int timeoutMs = 8000)
        {
            if (!IsConnected)
                await ConnectAsync(ServerIp);

            var packet = new PacketMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Type = type,
                PayloadJson = payload == null ? "{}" : JsonConvert.SerializeObject(payload)
            };

            var tcs = new TaskCompletionSource<PacketMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[packet.RequestId] = tcs;

            try
            {
                await _sendLock.WaitAsync();
                try
                {
                    await FramingHelper.SendPacketAsync(_stream, packet);
                }
                finally
                {
                    _sendLock.Release();
                }

                Task completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
                if (completed != tcs.Task)
                    throw new TimeoutException($"Server không phản hồi yêu cầu {type} sau {timeoutMs / 1000}s.");

                PacketMessage response = await tcs.Task;
                if (!response.Success)
                    throw new Exception(string.IsNullOrWhiteSpace(response.Message) ? response.ErrorCode : response.Message);

                if (typeof(T) == typeof(PacketMessage))
                    return (T)(object)response;

                return JsonConvert.DeserializeObject<T>(response.PayloadJson);
            }
            finally
            {
                _pendingRequests.TryRemove(packet.RequestId, out _);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    PacketMessage msg = await FramingHelper.ReceivePacketAsync(_stream);
                    if (msg == null) break;

                    // Nếu khớp RequestId đang chờ thì trả kết quả về cho hàm gọi
                    if (!string.IsNullOrEmpty(msg.RequestId) &&
                        _pendingRequests.TryRemove(msg.RequestId, out var tcs))
                    {
                        tcs.TrySetResult(msg);
                    }
                    else
                    {
                        // Ngược lại đây là gói Broadcast chủ động từ Server đẩy xuống
                        OnServerPush?.Invoke(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    OnDisconnected?.Invoke(ex.Message);
            }
            finally
            {
                foreach (var kv in _pendingRequests)
                {
                    kv.Value.TrySetException(new Exception("Mất kết nối tới Server."));
                }
                _pendingRequests.Clear();
            }
        }

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _tcpClient?.Close();
            }
            catch { }
        }
    }
}