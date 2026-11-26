using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protocol;

namespace QuanLyCafe_Nhom8.Services
{
    public sealed class NetworkClient
    {
        private static readonly Lazy<NetworkClient> _instance = new Lazy<NetworkClient>(() => new NetworkClient());
        public static NetworkClient Instance => _instance.Value;

        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<PacketMessage>> _pendingRequests =
            new ConcurrentDictionary<string, TaskCompletionSource<PacketMessage>>();

        private CancellationTokenSource _cts;
        private bool _isStarted = false;

        // Đọc IP Azure VM từ App.config (để trống nếu muốn tự dò UDP trong LAN)
        public string ConfiguredServerIp { get; set; } =
            (ConfigurationManager.AppSettings["ServerIp"] ?? "").Trim();

        public string ServerHost { get; private set; } = "127.0.0.1";
        public int ServerPort { get; private set; } = FramingHelper.DefaultTcpPort;
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public event Action<MsgType, string> OnServerEventReceived;
        public event Action<bool, string> OnConnectionStateChanged;

        public readonly List<string> ChatHistory = new List<string>();
        private Form _activeChatForm = null;

        private NetworkClient() { }

        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => MaintainConnectionLoopAsync(_cts.Token));
            Task.Run(() => HeartbeatLoopAsync(_cts.Token));
        }

        // Tự dò Server qua UDP Broadcast cổng 9001 (chỉ dùng khi chạy LAN & không cấu hình IP Azure)
        private async Task<IPEndPoint> DiscoverLanServerAsync(int timeoutMs = 1200)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (UdpClient udp = new UdpClient())
                    {
                        udp.EnableBroadcast = true;
                        udp.Client.ReceiveTimeout = timeoutMs;

                        byte[] reqBytes = Encoding.UTF8.GetBytes(FramingHelper.DiscoveryRequest);
                        udp.Send(reqBytes, reqBytes.Length, new IPEndPoint(IPAddress.Broadcast, FramingHelper.DefaultUdpPort));

                        IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                        byte[] respBytes = udp.Receive(ref remoteEp);
                        string resp = Encoding.UTF8.GetString(respBytes).Trim();

                        if (resp.StartsWith(FramingHelper.DiscoveryResponsePrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string portStr = resp.Substring(FramingHelper.DiscoveryResponsePrefix.Length);
                            int port = int.TryParse(portStr, out int p) ? p : FramingHelper.DefaultTcpPort;
                            return new IPEndPoint(remoteEp.Address, port);
                        }
                    }
                }
                catch { }
                return new IPEndPoint(IPAddress.Loopback, FramingHelper.DefaultTcpPort);
            }).ConfigureAwait(false);
        }

        private async Task<bool> EnsureConnectedAsync()
        {
            if (IsConnected) return true;

            await _connectLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsConnected) return true;

                if (!string.IsNullOrWhiteSpace(ConfiguredServerIp))
                {
                    ServerHost = ConfiguredServerIp;
                    ServerPort = int.TryParse(ConfigurationManager.AppSettings["ServerPort"], out int cfgPort)
                        ? cfgPort
                        : FramingHelper.DefaultTcpPort;
                }
                else
                {
                    IPEndPoint ep = await DiscoverLanServerAsync(1000).ConfigureAwait(false);
                    ServerHost = ep.Address.ToString();
                    ServerPort = ep.Port;
                }

                _tcpClient?.Close();
                _tcpClient = new TcpClient();

                var connectTask = _tcpClient.ConnectAsync(ServerHost, ServerPort);
                var finished = await Task.WhenAny(connectTask, Task.Delay(4000)).ConfigureAwait(false);
                if (finished != connectTask || !_tcpClient.Connected)
                {
                    _tcpClient.Close();
                    return false;
                }

                _stream = _tcpClient.GetStream();
                OnConnectionStateChanged?.Invoke(true, $"Đã kết nối TCP ({ServerHost}:{ServerPort})");

                _ = Task.Run(() => ReceiveLoopAsync(_cts?.Token ?? CancellationToken.None));
                return true;
            }
            catch (Exception ex)
            {
                OnConnectionStateChanged?.Invoke(false, $"Lỗi kết nối ({ServerHost}:{ServerPort}): {ex.Message}");
                return false;
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private async Task MaintainConnectionLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!IsConnected)
                {
                    await EnsureConnectedAsync().ConfigureAwait(false);
                }
                await Task.Delay(3000, token).ConfigureAwait(false);
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(10000, token).ConfigureAwait(false);
                if (IsConnected)
                {
                    try { await SendPacketInternalAsync(new PacketMessage { Type = MsgType.PING }).ConfigureAwait(false); }
                    catch { }
                }
            }
        }

        private async Task SendPacketInternalAsync(PacketMessage packet)
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!IsConnected) throw new IOException("Chưa kết nối tới CafeServer.");
                await FramingHelper.SendPacketAsync(_stream, packet).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    PacketMessage packet = await FramingHelper.ReceivePacketAsync(_stream).ConfigureAwait(false);
                    if (packet == null) break;

                    if (packet.Type == MsgType.PONG) continue;

                    // Nếu là phản hồi của 1 Request đang chờ -> trả về đúng TaskCompletionSource
                    if (!string.IsNullOrEmpty(packet.RequestId) &&
                        _pendingRequests.TryRemove(packet.RequestId, out var tcs))
                    {
                        tcs.TrySetResult(packet);
                        continue;
                    }

                    // Nếu là gói Server Broadcast (Realtime Push)
                    if (packet.Type == MsgType.CHAT_MESSAGE)
                    {
                        lock (ChatHistory)
                        {
                            try
                            {
                                var obj = JObject.Parse(packet.PayloadJson);
                                ChatHistory.Add(obj["Text"]?.ToString() ?? packet.PayloadJson);
                            }
                            catch { ChatHistory.Add(packet.PayloadJson); }
                        }
                    }

                    OnServerEventReceived?.Invoke(packet.Type, packet.PayloadJson);
                }
            }
            catch { }
            finally
            {
                foreach (var kv in _pendingRequests)
                {
                    if (_pendingRequests.TryRemove(kv.Key, out var tcs))
                        tcs.TrySetException(new IOException("Mất kết nối tới CafeServer."));
                }
                try { _tcpClient?.Close(); } catch { }
                _stream = null;
                OnConnectionStateChanged?.Invoke(false, "Mất kết nối TCP - Đang kết nối lại...");
            }
        }

        // Hàm gửi Request chuẩn Protocol và chờ phản hồi theo RequestId
        public async Task<PacketMessage> SendRequestAsync(MsgType type, object payload = null, int timeoutMs = 8000)
        {
            if (!await EnsureConnectedAsync().ConfigureAwait(false))
                throw new Exception($"Không kết nối được tới CafeServer ({ServerHost}:{ServerPort}).");

            var req = new PacketMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Type = type,
                PayloadJson = payload == null ? "{}" : JsonConvert.SerializeObject(payload)
            };

            var tcs = new TaskCompletionSource<PacketMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[req.RequestId] = tcs;

            try
            {
                await SendPacketInternalAsync(req).ConfigureAwait(false);
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (completed != tcs.Task)
                    throw new TimeoutException($"Server không phản hồi yêu cầu {type} sau {timeoutMs / 1000}s.");

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                _pendingRequests.TryRemove(req.RequestId, out _);
            }
        }

        private static T DeserializeOrThrow<T>(PacketMessage res)
        {
            if (!res.Success) throw new Exception(string.IsNullOrWhiteSpace(res.Message) ? res.ErrorCode : res.Message);
            return JsonConvert.DeserializeObject<T>(res.PayloadJson);
        }

        // ==================== API AUTH (CHO FrmAuth) ====================

        public async Task<UserDTO> LoginAsync(string username, string password)
        {
            var res = await SendRequestAsync(MsgType.LOGIN_REQ, new { Username = username, Password = password }).ConfigureAwait(false);
            if (!res.Success) return null;
            return JsonConvert.DeserializeObject<UserDTO>(res.PayloadJson);
        }

        // ==================== API POS & KDS (TƯƠNG THÍCH FrmMainPOS / FrmKDS) ====================

        public List<TableDTO> GetAllTables()
        {
            var res = Task.Run(() => SendRequestAsync(MsgType.GET_TABLES_REQ)).GetAwaiter().GetResult();
            return DeserializeOrThrow<List<TableDTO>>(res);
        }

        public List<ProductDTO> GetActiveProducts()
        {
            var res = Task.Run(() => SendRequestAsync(MsgType.GET_ACTIVE_PRODUCTS_REQ)).GetAwaiter().GetResult();
            return DeserializeOrThrow<List<ProductDTO>>(res);
        }

        public DataTable GetBillDetailsByTable(int tableId)
        {
            var res = Task.Run(() => SendRequestAsync(MsgType.GET_BILL_DETAILS_REQ, new { TableId = tableId })).GetAwaiter().GetResult();
            var items = DeserializeOrThrow<List<OrderItemDTO>>(res);

            DataTable dt = new DataTable();
            dt.Columns.Add("BillDetailId", typeof(int));
            dt.Columns.Add("Tên món", typeof(string));
            dt.Columns.Add("SL", typeof(int));
            dt.Columns.Add("Đơn giá", typeof(decimal));
            dt.Columns.Add("Thành tiền", typeof(decimal));
            dt.Columns.Add("Trạng thái", typeof(string));

            foreach (var it in items)
                dt.Rows.Add(it.BillDetailId, it.ProductName, it.Quantity, it.UnitPrice, it.SubTotal, it.KitchenStatus);
            return dt;
        }

        public bool VerifyManagerAccount(string username, string password, out string managerFullName)
        {
            var res = Task.Run(() => SendRequestAsync(MsgType.VERIFY_MANAGER_REQ, new { Username = username, Password = password })).GetAwaiter().GetResult();
            var dto = DeserializeOrThrow<ManagerVerifyResponseDTO>(res);
            managerFullName = dto.ManagerFullName ?? "";
            return dto.IsApproved;
        }

        public async Task AddItemToTableAsync(int tableId, int productId, int quantity)
        {
            var res = await SendRequestAsync(MsgType.ADD_ITEM_REQ, new { TableId = tableId, ProductId = productId, Quantity = quantity }).ConfigureAwait(false);
            if (!res.Success) throw new Exception(res.Message);
        }

        public async Task<int> SendTableOrderToKitchenAsync(int tableId, string tableName)
        {
            var res = await SendRequestAsync(MsgType.SEND_KITCHEN_REQ, new { TableId = tableId }).ConfigureAwait(false);
            if (!res.Success) throw new Exception(res.Message);
            int count = JObject.Parse(res.PayloadJson)["SentCount"]?.Value<int>() ?? 0;
            if (count > 0)
                await SendChatAsync($"[Quầy Thu Ngân]: Đã gửi {count} món của {tableName} xuống Quầy Pha Chế!").ConfigureAwait(false);
            return count;
        }

        public async Task RemoveBillDetailItemAsync(int billDetailId, int tableId, string tableName, string itemName, bool isSentToKitchen, string managerName)
        {
            var res = await SendRequestAsync(MsgType.REMOVE_ITEM_REQ, new { TableId = tableId, BillDetailId = billDetailId }).ConfigureAwait(false);
            if (!res.Success) throw new Exception(res.Message);
            if (isSentToKitchen)
                await SendChatAsync($"⚠️ [CẢNH BÁO HỦY MÓN] Bàn: {tableName} | Món: {itemName} | Duyệt: {managerName}").ConfigureAwait(false);
        }

        public async Task MoveToEmptyTableAsync(int sourceTableId, int targetTableId)
        {
            var res = await SendRequestAsync(MsgType.MOVE_TABLE_REQ, new { SourceTableId = sourceTableId, TargetTableId = targetTableId }).ConfigureAwait(false);
            if (!res.Success) throw new Exception(res.Message);
        }

        public async Task MergeOccupiedTablesAsync(int sourceTableId, int targetTableId)
        {
            var res = await SendRequestAsync(MsgType.MERGE_TABLE_REQ, new { SourceTableId = sourceTableId, TargetTableId = targetTableId }).ConfigureAwait(false);
            if (!res.Success) throw new Exception(res.Message);
        }

        public async Task<CheckoutResponseDTO> CheckoutTableAsync(int tableId, string tableName, int discountPercent, string paymentMethod)
        {
            var res = await SendRequestAsync(MsgType.CHECKOUT_REQ, new
            {
                TableId = tableId,
                DiscountPercent = discountPercent,
                PaymentMethod = paymentMethod
            }).ConfigureAwait(false);
            var dto = DeserializeOrThrow<CheckoutResponseDTO>(res);
            if (dto.IsSuccess)
                await SendChatAsync($"💰 [THANH TOÁN] {tableName}: +{dto.FinalAmount:N0} VNĐ ({paymentMethod})").ConfigureAwait(false);
            return dto;
        }

        public async Task<Bitmap> GetVietQrBitmapAsync(decimal amount, string tableName)
        {
            var res = await SendRequestAsync(MsgType.GET_VIETQR_REQ, new { Amount = amount, TableName = tableName }, 15000).ConfigureAwait(false);
            var dto = DeserializeOrThrow<QrResponseDTO>(res);
            byte[] bytes = Convert.FromBase64String(dto.Base64Png);
            using (MemoryStream ms = new MemoryStream(bytes))
            using (Image img = Image.FromStream(ms))
                return new Bitmap(img);
        }

        public DataTable GetPendingKitchenItems()
        {
            var res = Task.Run(() => SendRequestAsync(MsgType.GET_PENDING_KDS_REQ)).GetAwaiter().GetResult();
            var items = DeserializeOrThrow<List<KitchenItemDTO>>(res);

            DataTable dt = new DataTable();
            dt.Columns.Add("Mã", typeof(int));
            dt.Columns.Add("Bàn / Đơn", typeof(string));
            dt.Columns.Add("Tên món cần pha", typeof(string));
            dt.Columns.Add("Số lượng", typeof(int));
            dt.Columns.Add("Giờ gọi", typeof(string));
            dt.Columns.Add("Trạng thái", typeof(string));

            foreach (var it in items)
                dt.Rows.Add(it.BillDetailId, it.TableName, it.ProductName, it.Quantity, it.OrderTime, it.KitchenStatus);
            return dt;
        }

        public async Task MarkKitchenItemDoneAsync(int billDetailId, string tableName, string productName)
        {
            var res = await SendRequestAsync(MsgType.KDS_COMPLETE_ITEM_REQ, new { BillDetailId = billDetailId }).ConfigureAwait(false);
            if (!res.Success) throw new Exception(res.Message);
            await SendChatAsync($"✅ [Quầy Pha Chế]: Đã làm xong [{productName}] cho [{tableName}]!").ConfigureAwait(false);
        }

        public async Task SendChatAsync(string text)
        {
            try { await SendRequestAsync(MsgType.CHAT_MESSAGE, new { Text = text }).ConfigureAwait(false); }
            catch { }
        }

        public void ShowInAppChatAndStatsDialog(Form parentForm, string currentRoleName = "Nhân viên")
        {
            if (_activeChatForm != null && !_activeChatForm.IsDisposed)
            {
                _activeChatForm.BringToFront();
                return;
            }

            Form f = new Form { Text = $"Chat nội bộ ({ServerHost}:{ServerPort})", Size = new Size(560, 440), StartPosition = FormStartPosition.CenterParent };
            _activeChatForm = f;

            RichTextBox rtb = new RichTextBox { Location = new Point(12, 12), Size = new Size(520, 330), ReadOnly = true };
            TextBox txt = new TextBox { Location = new Point(12, 355), Size = new Size(410, 30) };
            Button btn = new Button { Text = "Gửi", Location = new Point(430, 353), Size = new Size(102, 32) };

            Action refresh = () =>
            {
                lock (ChatHistory) rtb.Text = string.Join(Environment.NewLine, ChatHistory);
                rtb.SelectionStart = rtb.Text.Length;
                rtb.ScrollToCaret();
            };

            Action<MsgType, string> onEvt = (t, p) =>
            {
                if (t == MsgType.CHAT_MESSAGE && !f.IsDisposed && f.IsHandleCreated)
                    f.BeginInvoke(new Action(refresh));
            };

            OnServerEventReceived += onEvt;
            f.FormClosing += (s, e) => OnServerEventReceived -= onEvt;
            btn.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txt.Text)) return;
                string msg = $"[{currentRoleName} - {DateTime.Now:HH:mm}]: {txt.Text.Trim()}";
                txt.Clear();
                await SendChatAsync(msg);
            };


            // Cho phép bấm phím Enter để gửi tin nhắn luôn (tắt tiếng "bíp" mặc định của Windows)
            txt.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btn.PerformClick();
                }
            };


            f.Controls.AddRange(new Control[] { rtb, txt, btn });
            refresh();
            f.Show(parentForm);
        }
    }
}
