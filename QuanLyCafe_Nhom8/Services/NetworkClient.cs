using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyCafe_Nhom8.DAL;
using QuanLyCafe_Nhom8.DTO;

namespace QuanLyCafe_Nhom8.Services
{
    // Danh sách các loại thông điệp (MsgType) theo Hợp đồng BE-FE v1.0
    public static class MsgType
    {
        public const string PING = "PING";
        public const string PONG = "PONG";
        public const string TABLE_STATUS_CHANGED = "TABLE_STATUS_CHANGED";
        public const string MENU_UPDATED = "MENU_UPDATED";
        public const string KDS_NEW_ORDER = "KDS_NEW_ORDER";
        public const string KDS_ITEM_DONE = "KDS_ITEM_DONE";
        public const string GET_PENDING_KDS = "GET_PENDING_KDS";
        public const string CHAT_MESSAGE = "CHAT_MESSAGE";
        public const string STATS_COMMAND = "STATS_COMMAND";
    }

    // Thực hiện UDP Broadcast để tự động tìm IP:Port của Server trong mạng LAN
    public static class UdpDiscoveryClient
    {
        public const int UdpDiscoveryPort = 8889;
        public const int DefaultTcpPort = 8888;

        public static async Task<IPEndPoint> DiscoverServerAsync(int timeoutMs = 1200)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (UdpClient udp = new UdpClient())
                    {
                        udp.EnableBroadcast = true;
                        udp.Client.ReceiveTimeout = timeoutMs;

                        byte[] reqBytes = Encoding.UTF8.GetBytes("COFFEE_FEFA_DISCOVER_REQ");
                        IPEndPoint broadcastEp = new IPEndPoint(IPAddress.Broadcast, UdpDiscoveryPort);
                        udp.Send(reqBytes, reqBytes.Length, broadcastEp);

                        IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                        byte[] respBytes = udp.Receive(ref remoteEp);
                        string resp = Encoding.UTF8.GetString(respBytes).Trim();

                        // Phản hồi từ UDP Responder dạng: COFFEE_FEFA_SERVER|192.168.1.10|8888
                        if (resp.StartsWith("COFFEE_FEFA_SERVER", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = resp.Split('|');
                            if (parts.Length >= 3 && IPAddress.TryParse(parts[1], out IPAddress ip) && int.TryParse(parts[2], out int port))
                            {
                                return new IPEndPoint(ip, port);
                            }
                            return new IPEndPoint(remoteEp.Address, DefaultTcpPort);
                        }
                    }
                }
                catch
                {
                    // Fallback về Loopback 127.0.0.1 nếu Firewall chặn Broadcast
                }
                return new IPEndPoint(IPAddress.Loopback, DefaultTcpPort);
            });
        }
    }

    // Thư viện NetworkClient dùng chung cho toàn bộ các màn hình Client (TCP Length-Prefix 4 byte + Auto-Reconnect + Heartbeat)
    public sealed class NetworkClient
    {
        private static readonly Lazy<NetworkClient> _instance = new Lazy<NetworkClient>(() => new NetworkClient());
        public static NetworkClient Instance => _instance.Value;

        public const int MaxPayloadSize = 1024 * 1024; // Giới hạn gói tin <= 1MB chống tràn bộ nhớ
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _cts;
        private bool _isStarted = false;

        public string ServerHost { get; private set; } = "127.0.0.1";
        public int ServerPort { get; private set; } = UdpDiscoveryClient.DefaultTcpPort;
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        // Sự kiện đẩy về cho các Form (FrmMainPOS, FrmKDS, FrmChat) đăng ký nhận Realtime
        public event Action<string, string> OnServerEventReceived;
        public event Action<bool, string> OnConnectionStateChanged;

        // Lưu lịch sử Chat & Thống kê nội bộ trong phiên làm việc
        public readonly List<string> ChatHistory = new List<string>();

        private readonly PosServiceDAL _posDal = new PosServiceDAL();

        private NetworkClient() { }

        // Khởi động cả Embedded TCP/UDP Server (nếu máy hiện tại đóng vai Server) và kết nối TcpSessionClient
        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;

            EmbeddedLanServer.EnsureRunning();
            _cts = new CancellationTokenSource();
            Task.Run(() => MaintainConnectionLoopAsync(_cts.Token));
            Task.Run(() => HeartbeatLoopAsync(_cts.Token));
        }

        // Vòng lặp tự động tìm Server qua UDP, kết nối TCP và tự động Reconnect khi rớt mạng
        private async Task MaintainConnectionLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!IsConnected)
                {
                    try
                    {
                        IPEndPoint ep = await UdpDiscoveryClient.DiscoverServerAsync(800);
                        ServerHost = ep.Address.ToString();
                        ServerPort = ep.Port;

                        _tcpClient?.Close();
                        _tcpClient = new TcpClient();
                        await _tcpClient.ConnectAsync(ServerHost, ServerPort);
                        _stream = _tcpClient.GetStream();

                        OnConnectionStateChanged?.Invoke(true, $"Đã kết nối TCP ({ServerHost}:{ServerPort})");

                        // Bắt đầu đọc luồng khung 4-byte Length-Prefix từ Server
                        await ReceiveLoopAsync(token);
                    }
                    catch
                    {
                        OnConnectionStateChanged?.Invoke(false, "Đang tự động kết nối lại TCP Server...");
                    }
                }
                await Task.Delay(2500, token);
            }
        }

        // Vòng lặp gửi Heartbeat PING định kỳ mỗi 10 giây
        private async Task HeartbeatLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(10000, token);
                if (IsConnected)
                {
                    await SendFrameAsync(MsgType.PING, "PING");
                }
            }
        }

        // Gửi gói tin theo đúng chuẩn 4-byte Int32 Length-Prefix + Payload UTF-8
        public async Task<bool> SendFrameAsync(string msgType, string payload = "")
        {
            if (!IsConnected || _stream == null) return false;

            string packet = $"{msgType}|{payload}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(packet);
            if (payloadBytes.Length > MaxPayloadSize) return false;

            byte[] lengthPrefix = BitConverter.GetBytes(payloadBytes.Length);

            await _writeLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(lengthPrefix, 0, 4);
                await _stream.WriteAsync(payloadBytes, 0, payloadBytes.Length);
                await _stream.FlushAsync();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // Đọc chính xác 4 byte header độ dài trước, sau đó đọc đủ N byte payload chống dính/phân mảnh gói TCP
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] lenBuffer = new byte[4];

            while (!token.IsCancellationRequested && IsConnected)
            {
                int readHeader = await ReadExactBytesAsync(_stream, lenBuffer, 4, token);
                if (readHeader < 4) break;

                int payloadLength = BitConverter.ToInt32(lenBuffer, 0);
                if (payloadLength <= 0 || payloadLength > MaxPayloadSize) break;

                byte[] payloadBuffer = new byte[payloadLength];
                int readPayload = await ReadExactBytesAsync(_stream, payloadBuffer, payloadLength, token);
                if (readPayload < payloadLength) break;

                string rawPacket = Encoding.UTF8.GetString(payloadBuffer);
                int sepIndex = rawPacket.IndexOf('|');
                string msgType = sepIndex >= 0 ? rawPacket.Substring(0, sepIndex) : rawPacket;
                string content = sepIndex >= 0 ? rawPacket.Substring(sepIndex + 1) : "";

                if (msgType == MsgType.PONG) continue;

                if (msgType == MsgType.CHAT_MESSAGE)
                {
                    lock (ChatHistory)
                    {
                        ChatHistory.Add(content);
                    }
                }

                OnServerEventReceived?.Invoke(msgType, content);
            }

            OnConnectionStateChanged?.Invoke(false, "Mất kết nối TCP - Đang kết nối lại...");
        }

        private static async Task<int> ReadExactBytesAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken token)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalRead, count - totalRead, token);
                if (read <= 0) return totalRead;
                totalRead += read;
            }
            return totalRead;
        }

        // ==================== CÁC HÀM GỌI NGHIỆP VỤ POS & KDS QUA NETWORKCLIENT ====================

        public List<TableDTO> GetAllTables() => _posDal.GetAllTables();

        public List<ProductDTO> GetActiveProducts() => _posDal.GetActiveProducts();

        public DataTable GetBillDetailsByTable(int tableId) => _posDal.GetBillDetailsByTable(tableId);

        public bool VerifyManagerAccount(string username, string password, out string managerFullName) =>
            _posDal.VerifyManagerAccount(username, password, out managerFullName);

        public async Task AddItemToTableAsync(int tableId, int productId, int quantity)
        {
            await Task.Run(() => _posDal.AddItemToTable(tableId, productId, quantity));
            await SendFrameAsync(MsgType.TABLE_STATUS_CHANGED, tableId.ToString());
        }

        public async Task<int> SendTableOrderToKitchenAsync(int tableId, string tableName)
        {
            int count = await Task.Run(() => _posDal.SendTableOrderToKitchen(tableId));
            if (count > 0)
            {
                await SendFrameAsync(MsgType.KDS_NEW_ORDER, $"{tableId}|{tableName}|{count}");
                await SendFrameAsync(MsgType.CHAT_MESSAGE, $"[Quầy Thu Ngân]: Đã gửi {count} món của {tableName} xuống Quầy Pha Chế!");
            }
            return count;
        }

        public async Task RemoveBillDetailItemAsync(int billDetailId, int tableId, string tableName, string itemName, bool isSentToKitchen, string managerName)
        {
            await Task.Run(() => _posDal.RemoveBillDetailItem(billDetailId, tableId));
            await SendFrameAsync(MsgType.TABLE_STATUS_CHANGED, tableId.ToString());
            await SendFrameAsync(MsgType.KDS_NEW_ORDER, tableId.ToString());

            if (isSentToKitchen)
            {
                string alert = $"⚠️ [CẢNH BÁO HỦY MÓN] Bàn: {tableName} | Món: {itemName} | Duyệt bởi: {managerName} ({DateTime.Now:HH:mm:ss})";
                await SendFrameAsync(MsgType.CHAT_MESSAGE, alert);
            }
        }

        public async Task MoveToEmptyTableAsync(int sourceTableId, int targetTableId)
        {
            await Task.Run(() => _posDal.MoveToEmptyTable(sourceTableId, targetTableId));
            await SendFrameAsync(MsgType.TABLE_STATUS_CHANGED, $"{sourceTableId},{targetTableId}");
        }

        public async Task MergeOccupiedTablesAsync(int sourceTableId, int targetTableId)
        {
            await Task.Run(() => _posDal.MergeOccupiedTables(sourceTableId, targetTableId));
            await SendFrameAsync(MsgType.TABLE_STATUS_CHANGED, $"{sourceTableId},{targetTableId}");
        }

        public async Task<CheckoutResponseDTO> CheckoutTableAsync(int tableId, string tableName, int discountPercent, string paymentMethod)
        {
            decimal finalPaid = 0;
            bool ok = await Task.Run(() => _posDal.CheckoutTable(tableId, discountPercent, paymentMethod, out finalPaid));
            if (ok)
            {
                await SendFrameAsync(MsgType.TABLE_STATUS_CHANGED, tableId.ToString());
                await SendFrameAsync(MsgType.CHAT_MESSAGE, $"💰 [THANH TOÁN] {tableName}: +{finalPaid:N0} VNĐ ({paymentMethod})");
            }
            return new CheckoutResponseDTO
            {
                IsSuccess = ok,
                TableId = tableId,
                FinalAmount = finalPaid
            };
        }

        public DataTable GetPendingKitchenItems() => _posDal.GetPendingKitchenItems();

        public async Task MarkKitchenItemDoneAsync(int billDetailId, string tableName, string productName)
        {
            await Task.Run(() => _posDal.MarkKitchenItemDone(billDetailId));
            await SendFrameAsync(MsgType.KDS_ITEM_DONE, $"{billDetailId}|{tableName}|{productName}");
            await SendFrameAsync(MsgType.CHAT_MESSAGE, $"✅ [Quầy Pha Chế]: Đã làm xong món [{productName}] cho [{tableName}]!");
        }

        // ==================== CỬA SỔ CHAT NỘI BỘ & TRA CỨU THỐNG KÊ TRÊN APP (THAY TELEGRAM BOT) ====================

        private Form _activeChatForm = null;

        public void ShowInAppChatAndStatsDialog(Form parentForm, string currentRoleName = "Quản lý / Thu ngân")
        {
            // Nếu cửa sổ Chat đang mở sẵn thì đưa lên trên cùng, cho phép kéo thả song song với màn hình POS
            if (_activeChatForm != null && !_activeChatForm.IsDisposed)
            {
                _activeChatForm.BringToFront();
                _activeChatForm.Focus();
                return;
            }

            Form chatForm = new Form
            {
                Text = $"Chat Nội Bộ LAN & Bot Thống Kê Trên App (TCP {ServerHost}:{ServerPort})",
                Size = new Size(760, 560),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(560, 140),
                BackColor = Color.FromArgb(244, 239, 234)
            };
            _activeChatForm = chatForm;

            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(62, 39, 35)
            };

            Label lblHeader = new Label
            {
                Text = "💬 KÊNH CHAT NỘI BỘ LAN && TRA CỨU THỐNG KÊ NHANH TRÊN APP",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 224, 178),
                Location = new Point(16, 16),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblHeader);

            RichTextBox rtbChat = new RichTextBox
            {
                Location = new Point(16, 70),
                Size = new Size(710, 340),
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 4 nút lệnh vừa vặn chiều rộng, không bị khuất chữ
            Button btnCmdToday = CreateQuickCmdButton("📊 /homnay (Hôm nay)", new Point(16, 420), 172, async () =>
                await SendFrameAsync(MsgType.STATS_COMMAND, "/homnay"));

            Button btnCmdYesterday = CreateQuickCmdButton("📅 /homqua (Hôm qua)", new Point(195, 420), 172, async () =>
                await SendFrameAsync(MsgType.STATS_COMMAND, "/homqua"));

            Button btnCmdTables = CreateQuickCmdButton("☕ /ban (Bàn có khách)", new Point(374, 420), 172, async () =>
                await SendFrameAsync(MsgType.STATS_COMMAND, "/ban"));

            Button btnCmdCancelLog = CreateQuickCmdButton("⚠️ /huymon (Hủy món)", new Point(553, 420), 173, async () =>
                await SendFrameAsync(MsgType.STATS_COMMAND, "/huymon"));

            TextBox txtMsg = new TextBox
            {
                Location = new Point(16, 466),
                Size = new Size(575, 32),
                Font = new Font("Segoe UI", 10.5f)
            };

            Button btnSend = new Button
            {
                Text = "Gửi TCP ➔",
                Location = new Point(600, 464),
                Size = new Size(126, 34),
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;

            Action refreshChatBox = () =>
            {
                if (rtbChat.IsDisposed) return;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("💡 MẸO: Bấm các nút bên dưới hoặc gõ /homnay, /homqua, /ban, /huymon để tra cứu qua TCP Socket!");
                sb.AppendLine("--------------------------------------------------------------------------------");
                lock (ChatHistory)
                {
                    foreach (string line in ChatHistory)
                    {
                        sb.AppendLine(line);
                        sb.AppendLine();
                    }
                }
                rtbChat.Text = sb.ToString();
                rtbChat.SelectionStart = rtbChat.Text.Length;
                rtbChat.ScrollToCaret();
            };

            Action<string, string> eventHandler = (type, payload) =>
            {
                if (type == MsgType.CHAT_MESSAGE && !chatForm.IsDisposed && chatForm.IsHandleCreated)
                {
                    chatForm.BeginInvoke(new Action(refreshChatBox));
                }
            };

            OnServerEventReceived += eventHandler;
            chatForm.FormClosing += (s, e) => OnServerEventReceived -= eventHandler;

            Func<Task> sendAction = async () =>
            {
                string raw = txtMsg.Text.Trim();
                if (string.IsNullOrEmpty(raw)) return;
                txtMsg.Clear();

                if (raw.StartsWith("/"))
                {
                    await SendFrameAsync(MsgType.STATS_COMMAND, raw);
                }
                else
                {
                    await SendFrameAsync(MsgType.CHAT_MESSAGE, $"[{currentRoleName} - {DateTime.Now:HH:mm}]: {raw}");
                }
            };

            btnSend.Click += async (s, e) => await sendAction();
            txtMsg.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await sendAction();
                }
            };

            refreshChatBox();

            chatForm.Controls.AddRange(new Control[]
            {
                pnlHeader, rtbChat,
                btnCmdToday, btnCmdYesterday, btnCmdTables, btnCmdCancelLog,
                txtMsg, btnSend
            });

            // Mở dạng Non-Modal (.Show) để người dùng vừa để bảng Chat vừa bấm & kéo thả màn hình POS bên dưới
            chatForm.TopMost = true;
            chatForm.Show(parentForm);
        }

        private static Button CreateQuickCmdButton(string text, Point loc, int width, Func<Task> onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Location = loc,
                Size = new Size(width, 34),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += async (s, e) => await onClick();
            return btn;
        }
    }

    // Máy chủ TCP Đa luồng & UDP Responder tích hợp sẵn (Giúp điều phối gói tin 4-byte Length-Prefix giữa các Client)
    internal static class EmbeddedLanServer
    {
        private static bool _running = false;
        private static TcpListener _listener;
        private static readonly ConcurrentDictionary<Guid, TcpClient> _clients = new ConcurrentDictionary<Guid, TcpClient>();

        public static void EnsureRunning()
        {
            if (_running) return;
            _running = true;

            Task.Run(StartUdpResponderLoop);
            Task.Run(StartTcpServerLoop);
        }

        private static void StartUdpResponderLoop()
        {
            try
            {
                using (UdpClient udpServer = new UdpClient(UdpDiscoveryClient.UdpDiscoveryPort))
                {
                    IPEndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    while (true)
                    {
                        byte[] data = udpServer.Receive(ref remoteEp);
                        string msg = Encoding.UTF8.GetString(data).Trim();
                        if (msg == "COFFEE_FEFA_DISCOVER_REQ")
                        {
                            string reply = $"COFFEE_FEFA_SERVER|127.0.0.1|{UdpDiscoveryClient.DefaultTcpPort}";
                            byte[] replyBytes = Encoding.UTF8.GetBytes(reply);
                            udpServer.Send(replyBytes, replyBytes.Length, remoteEp);
                        }
                    }
                }
            }
            catch
            {
                // Cổng UDP đã có tiến trình Server khác lắng nghe
            }
        }

        private static async Task StartTcpServerLoop()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, UdpDiscoveryClient.DefaultTcpPort);
                _listener.Start();

                while (true)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Guid clientId = Guid.NewGuid();
                    _clients[clientId] = client;
                    _ = Task.Run(() => HandleClientAsync(clientId, client));
                }
            }
            catch
            {
                // Cổng TCP 8888 đã có tiến trình Server riêng lắng nghe
            }
        }

        private static async Task HandleClientAsync(Guid clientId, TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] lenBuf = new byte[4];
                    while (client.Connected)
                    {
                        int rLen = await ReadExactAsync(stream, lenBuf, 4);
                        if (rLen < 4) break;

                        int size = BitConverter.ToInt32(lenBuf, 0);
                        if (size <= 0 || size > NetworkClient.MaxPayloadSize) break;

                        byte[] payloadBuf = new byte[size];
                        int rPay = await ReadExactAsync(stream, payloadBuf, size);
                        if (rPay < size) break;

                        string packet = Encoding.UTF8.GetString(payloadBuf);
                        int idx = packet.IndexOf('|');
                        string type = idx >= 0 ? packet.Substring(0, idx) : packet;
                        string body = idx >= 0 ? packet.Substring(idx + 1) : "";

                        if (type == MsgType.PING)
                        {
                            await SendSingleClientAsync(client, $"{MsgType.PONG}|PONG");
                            continue;
                        }

                        if (type == MsgType.STATS_COMMAND)
                        {
                            string report = BuildStatsReportForAppChat(body);
                            await BroadcastPacketAsync($"{MsgType.CHAT_MESSAGE}|{report}");
                            continue;
                        }

                        // Broadcast sự kiện (KDS_NEW_ORDER, KDS_ITEM_DONE, TABLE_STATUS_CHANGED, CHAT_MESSAGE) tới toàn bộ Client
                        await BroadcastPacketAsync(packet);
                    }
                }
            }
            catch { }
            finally
            {
                _clients.TryRemove(clientId, out _);
            }
        }

        private static async Task BroadcastPacketAsync(string packet)
        {
            byte[] payload = Encoding.UTF8.GetBytes(packet);
            byte[] prefix = BitConverter.GetBytes(payload.Length);

            foreach (var kv in _clients)
            {
                try
                {
                    if (kv.Value.Connected)
                    {
                        NetworkStream ns = kv.Value.GetStream();
                        await ns.WriteAsync(prefix, 0, 4);
                        await ns.WriteAsync(payload, 0, payload.Length);
                        await ns.FlushAsync();
                    }
                }
                catch { }
            }
        }

        private static async Task SendSingleClientAsync(TcpClient client, string packet)
        {
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(packet);
                byte[] prefix = BitConverter.GetBytes(payload.Length);
                NetworkStream ns = client.GetStream();
                await ns.WriteAsync(prefix, 0, 4);
                await ns.WriteAsync(payload, 0, payload.Length);
                await ns.FlushAsync();
            }
            catch { }
        }

        private static async Task<int> ReadExactAsync(NetworkStream s, byte[] buf, int count)
        {
            int total = 0;
            while (total < count)
            {
                int r = await s.ReadAsync(buf, total, count - total);
                if (r <= 0) return total;
                total += r;
            }
            return total;
        }

        // Xử lý lệnh tra cứu thống kê (/homnay, /homqua, /ban, /huymon) ngay trên App Chat qua TCP
        private static string BuildStatsReportForAppChat(string rawCommand)
        {
            string cmd = rawCommand.Trim().ToLowerInvariant().Split(' ')[0];
            try
            {
                if (cmd == "/homnay" || cmd == "/doanhthu")
                {
                    return QueryRevenueByDate(DateTime.Today, "HÔM NAY");
                }
                if (cmd == "/homqua")
                {
                    return QueryRevenueByDate(DateTime.Today.AddDays(-1), "HÔM QUA");
                }
                if (cmd == "/ban")
                {
                    DataTable dt = DatabaseHelper.ExecuteQuery(@"
                        SELECT t.TableName, b.TotalAmount
                        FROM Tables t
                        INNER JOIN Bills b ON t.TableId = b.TableId
                        WHERE b.Status = 0");
                    if (dt.Rows.Count == 0) return "🤖 [BOT THỐNG KÊ APP]: 🟢 Hiện tại tất cả các bàn đều đang TRỐNG.";

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"🤖 [BOT THỐNG KÊ APP] 🟠 ĐANG PHỤC VỤ {dt.Rows.Count} BÀN / ĐƠN:");
                    foreach (DataRow r in dt.Rows)
                    {
                        sb.AppendLine($"  • {r["TableName"]}: tạm tính {Convert.ToDecimal(r["TotalAmount"]):N0} VNĐ");
                    }
                    return sb.ToString().TrimEnd();
                }
                if (cmd == "/huymon")
                {
                    DataTable dt = DatabaseHelper.ExecuteQuery(@"
                        SELECT TOP 5 a.BillId, p.ProductName, a.DeletedQuantity, FORMAT(a.LogTime, 'HH:mm:ss') AS GioHuy
                        FROM AuditLogs a
                        LEFT JOIN Products p ON a.ProductId = p.ProductId
                        WHERE CAST(a.LogTime AS DATE) = CAST(GETDATE() AS DATE)
                        ORDER BY a.LogId DESC");
                    if (dt.Rows.Count == 0) return "🤖 [BOT THỐNG KÊ APP]: ✅ Hôm nay quán không có thao tác hủy món nào.";

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("🤖 [BOT THỐNG KÊ APP] ⚠️ LỊCH SỬ HỦY MÓN HÔM NAY:");
                    foreach (DataRow row in dt.Rows)
                    {
                        sb.AppendLine($"  • Đơn #{row["BillId"]} | Món: {row["ProductName"]} | SL hủy: {row["DeletedQuantity"]} | Lúc: {row["GioHuy"]}");
                    }
                    return sb.ToString().TrimEnd();
                }

                return "🤖 [BOT THỐNG KÊ APP]: Các lệnh hỗ trợ: /homnay , /homqua , /ban , /huymon";
            }
            catch (Exception ex)
            {
                return "🤖 [BOT THỐNG KÊ APP] Lỗi truy vấn: " + ex.Message;
            }
        }

        private static string QueryRevenueByDate(DateTime date, string title)
        {
            var prms = new[] { new System.Data.SqlClient.SqlParameter("@Ngay", date.Date) };
            DataTable dt = DatabaseHelper.ExecuteQuery(@"
                SELECT COUNT(*) AS SoDon, ISNULL(SUM(TotalAmount), 0) AS TongTien
                FROM Bills
                WHERE Status = 1 AND CAST(CheckOutTime AS DATE) = @Ngay", prms);

            int soDon = Convert.ToInt32(dt.Rows[0]["SoDon"]);
            decimal tong = Convert.ToDecimal(dt.Rows[0]["TongTien"]);
            return $"🤖 [BOT THỐNG KÊ APP] 📊 DOANH THU {title} ({date:dd/MM/yyyy}): Tổng {soDon} đơn — Tổng thu: {tong:N0} VNĐ";
        }
    }
}