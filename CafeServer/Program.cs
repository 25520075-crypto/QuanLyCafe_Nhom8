using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protocol;
using CafeServer.DAL;
using CafeServer.Services;

namespace CafeServer
{
    internal class ClientSession
    {
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }
        public SemaphoreSlim SendLock { get; } = new SemaphoreSlim(1, 1);
        public string Username { get; set; } = "";
        public string Role { get; set; } = ""; // "Admin" hoặc "Staff"
        public bool IsAdmin => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    }

    internal class Program
    {
        private static readonly ConcurrentDictionary<string, ClientSession> _sessions = new ConcurrentDictionary<string, ClientSession>();
        private static readonly ConcurrentDictionary<int, object> _tableLocks = new ConcurrentDictionary<int, object>();
        private static readonly ConcurrentDictionary<string, (string Otp, DateTime Expiry)> _otpStore = new ConcurrentDictionary<string, (string, DateTime)>();

        private static readonly UserDAL _userDal = new UserDAL();
        private static readonly MenuDAL _menuDal = new MenuDAL();
        private static readonly PosServiceDAL _posDal = new PosServiceDAL();
        private static readonly ServerDataAdapter _adapter = new ServerDataAdapter();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "COFFEE FEFA SERVER - NHÓM 8";
            Console.WriteLine("=================================================");
            Console.WriteLine("   COFFEE FEFA TCP/UDP SERVER - NHÓM 8 (UIT)     ");
            Console.WriteLine("=================================================");

            // Kiểm tra kết nối SQL Server ngay khi khởi động
            try
            {
                var tables = _posDal.GetAllTables();
                Console.WriteLine($"[SQL] Kết nối CSDL CAFE thành công ({tables.Count} bàn/kênh).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SQL ERROR] Không kết nối được SQL Server: {ex.Message}");
            }

            // Chạy UDP Discovery Responder nền
            Task.Run(() => RunUdpDiscoveryAsync());

            // Chạy TCP Server đa luồng
            TcpListener listener = new TcpListener(IPAddress.Any, FramingHelper.DefaultTcpPort);
            listener.Start();
            Console.WriteLine($"[TCP] Đang lắng nghe tại cổng {FramingHelper.DefaultTcpPort}...");
            Console.WriteLine($"[UDP] Discovery đang lắng nghe tại cổng {FramingHelper.DefaultUdpPort}...\n");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Task.Run(() => HandleClientConnectionAsync(client));
            }
        }

        private static async Task RunUdpDiscoveryAsync()
        {
            using (UdpClient udp = new UdpClient(FramingHelper.DefaultUdpPort))
            {
                while (true)
                {
                    try
                    {
                        UdpReceiveResult res = await udp.ReceiveAsync();
                        string msg = Encoding.UTF8.GetString(res.Buffer).Trim();
                        if (msg == FramingHelper.DiscoveryRequest)
                        {
                            byte[] reply = Encoding.UTF8.GetBytes($"{FramingHelper.DiscoveryResponsePrefix}{FramingHelper.DefaultTcpPort}");
                            await udp.SendAsync(reply, reply.Length, res.RemoteEndPoint);
                            Console.WriteLine($"[UDP] Phản hồi dò tìm Server tới {res.RemoteEndPoint}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UDP WARN] {ex.Message}");
                    }
                }
            }
        }

        private static async Task HandleClientConnectionAsync(TcpClient tcpClient)
        {
            var session = new ClientSession
            {
                Client = tcpClient,
                Stream = tcpClient.GetStream()
            };
            _sessions[session.SessionId] = session;
            string remoteIp = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            Console.WriteLine($"[+] Kết nối mới: {remoteIp} (Session: {session.SessionId.Substring(0, 6)})");

            try
            {
                while (tcpClient.Connected)
                {
                    PacketMessage req = await FramingHelper.ReceivePacketAsync(session.Stream);
                    if (req == null) break;

                    PacketMessage res = await ProcessRequestAsync(session, req);
                    if (res != null)
                    {
                        res.RequestId = req.RequestId;
                        await SendToSessionAsync(session, res);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Ngắt bất thường ({remoteIp}): {ex.Message}");
            }
            finally
            {
                _sessions.TryRemove(session.SessionId, out _);
                tcpClient.Close();
                Console.WriteLine($"[-] Đã ngắt kết nối: {remoteIp}");
            }
        }

        private static async Task<PacketMessage> ProcessRequestAsync(ClientSession session, PacketMessage req)
        {
            try
            {
                JObject data = string.IsNullOrWhiteSpace(req.PayloadJson) ? new JObject() : JObject.Parse(req.PayloadJson);

                switch (req.Type)
                {
                    case MsgType.CHAT_MESSAGE:
                        await BroadcastAsync(new PacketMessage { Type = MsgType.CHAT_MESSAGE, PayloadJson = req.PayloadJson });
                        return Ok(req.Type, new { Sent = true });

                    case MsgType.PING:
                        return new PacketMessage { Type = MsgType.PONG };

                    // ================= AUTH & USER =================
                    case MsgType.LOGIN_REQ:
                        {
                            string u = data["Username"]?.ToString() ?? "";
                            string p = data["Password"]?.ToString() ?? "";
                            UserDTO user = _userDal.CheckLogin(u, p);
                            if (user == null)
                                return Fail(MsgType.AUTH_FAIL, "AUTH_FAIL", "Sai tên đăng nhập, mật khẩu hoặc tài khoản bị khóa!");

                            session.Username = user.Username;
                            session.Role = user.Role;
                            Console.WriteLine($"[AUTH] {user.Username} ({user.Role}) đăng nhập thành công.");
                            return Ok(MsgType.LOGIN_OK, user);
                        }

                    case MsgType.FORGOT_SEND_OTP_REQ:
                        {
                            string email = data["Email"]?.ToString()?.Trim() ?? "";
                            if (!_userDal.CheckEmailExist(email))
                                return Fail(req.Type, "EMAIL_NOT_FOUND", "Gmail này chưa được liên kết với tài khoản nào!");

                            string otp = AuthService.GenerateOTP();
                            if (!AuthService.SendOtpEmail(email, otp, "Khôi phục mật khẩu", out string err))
                                return Fail(req.Type, "SMTP_ERROR", err);

                            _otpStore[$"FORGOT:{email.ToLower()}"] = (otp, DateTime.Now.AddMinutes(5));
                            return Ok(req.Type, new { Sent = true }, "Đã gửi mã OTP qua Gmail!");
                        }

                    case MsgType.FORGOT_RESET_PASS_REQ:
                        {
                            string email = data["Email"]?.ToString()?.Trim() ?? "";
                            string inputOtp = data["Otp"]?.ToString()?.Trim() ?? "";
                            string newPass = data["NewPassword"]?.ToString()?.Trim() ?? "";

                            if (!VerifyStoredOtp($"FORGOT:{email.ToLower()}", inputOtp, out string otpErr))
                                return Fail(req.Type, "INVALID_OTP", otpErr);

                            bool updated = _userDal.UpdatePasswordByEmail(email, newPass);
                            return updated ? Ok(req.Type, new { Updated = true }) : Fail(req.Type, "DB_ERROR", "Không thể cập nhật mật khẩu.");
                        }

                    case MsgType.GET_USERS_REQ:
                        if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin được xem danh sách nhân sự!");
                        return Ok(req.Type, _adapter.GetAllUsersDto());

                    case MsgType.ADMIN_SEND_CREATE_OTP_REQ:
                        {
                            if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền này!");
                            string u = data["Username"]?.ToString()?.Trim() ?? "";
                            string email = data["Email"]?.ToString()?.Trim() ?? "";
                            if (_userDal.IsUsernameOrEmailExist(u, email))
                                return Fail(req.Type, "DUPLICATE_USER", "Tên đăng nhập hoặc Gmail đã tồn tại!");

                            string otp = AuthService.GenerateOTP();
                            if (!AuthService.SendOtpEmail(email, otp, $"Xác thực liên kết tài khoản mới [{u}]", out string err))
                                return Fail(req.Type, "SMTP_ERROR", err);

                            _otpStore[$"CREATE:{email.ToLower()}"] = (otp, DateTime.Now.AddMinutes(5));
                            return Ok(req.Type, new { Sent = true });
                        }

                    case MsgType.ADMIN_CREATE_USER_REQ:
                        {
                            if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền tạo tài khoản!");
                            string email = data["Email"]?.ToString()?.Trim() ?? "";
                            string otp = data["Otp"]?.ToString()?.Trim() ?? "";
                            if (!VerifyStoredOtp($"CREATE:{email.ToLower()}", otp, out string otpErr))
                                return Fail(req.Type, "INVALID_OTP", otpErr);

                            bool created = _userDal.CreateAccountByAdmin(
                                data["Username"]?.ToString(),
                                data["Password"]?.ToString(),
                                data["FullName"]?.ToString(),
                                data["Role"]?.ToString(),
                                email);
                            return created ? Ok(req.Type, new { Created = true }) : Fail(req.Type, "DB_ERROR", "Tạo tài khoản thất bại.");
                        }

                    case MsgType.DELETE_USER_REQ:
                        if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền xóa nhân viên!");
                        return Ok(req.Type, new { Deleted = _userDal.DeleteUser(data["DisplayOrderId"].Value<int>()) });

                    case MsgType.TOGGLE_USER_REQ:
                        if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền khóa/mở tài khoản!");
                        return Ok(req.Type, new { Toggled = _userDal.ToggleUserActiveStatus(data["DisplayOrderId"].Value<int>(), data["CurrentStatus"].Value<bool>()) });

                    case MsgType.VERIFY_MANAGER_REQ:
                        {
                            bool approved = _posDal.VerifyManagerAccount(data["Username"]?.ToString(), data["Password"]?.ToString(), out string managerName);
                            return Ok(req.Type, new ManagerVerifyResponseDTO { IsApproved = approved, ManagerFullName = managerName });
                        }

                    // ================= MENU & CATEGORY =================
                    case MsgType.GET_CATEGORIES_REQ:
                        return Ok(req.Type, _adapter.GetAllCategoriesDto());

                    case MsgType.ADD_CATEGORY_REQ:
                        if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền thêm danh mục!");
                        bool catAdded = _menuDal.AddCategory(data["CategoryName"]?.ToString());
                        if (catAdded) await BroadcastAsync(new PacketMessage { Type = MsgType.MENU_UPDATED });
                        return Ok(req.Type, new { Added = catAdded });

                    case MsgType.GET_ACTIVE_PRODUCTS_REQ:
                        return Ok(req.Type, _posDal.GetActiveProducts());

                    case MsgType.GET_ALL_PRODUCTS_REQ:
                        return Ok(req.Type, _adapter.GetProductsForManagerDto(
                            data["CategoryId"]?.Value<int>() ?? 0,
                            data["Keyword"]?.ToString() ?? ""));

                    case MsgType.INSERT_PRODUCT_REQ:
                        if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền thêm món!");
                        bool inserted = _menuDal.InsertProduct(
                            data["CategoryId"].Value<int>(),
                            data["ProductName"].ToString(),
                            data["Price"].Value<decimal>(),
                            data["ImagePath"]?.ToString() ?? "",
                            data["IsActive"].Value<bool>());
                        if (inserted) await BroadcastAsync(new PacketMessage { Type = MsgType.MENU_UPDATED });
                        return Ok(req.Type, new { Inserted = inserted });

                    case MsgType.UPDATE_PRODUCT_REQ:
                        if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền sửa món!");
                        bool updatedProd = _menuDal.UpdateProduct(
                            data["ProductId"].Value<int>(),
                            data["CategoryId"].Value<int>(),
                            data["ProductName"].ToString(),
                            data["Price"].Value<decimal>(),
                            data["ImagePath"]?.ToString() ?? "",
                            data["IsActive"].Value<bool>());
                        if (updatedProd) await BroadcastAsync(new PacketMessage { Type = MsgType.MENU_UPDATED });
                        return Ok(req.Type, new { Updated = updatedProd });

                    case MsgType.TOGGLE_PRODUCT_REQ:
                        if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền đổi trạng thái món!");
                        bool toggledProd = _menuDal.ToggleProductStatus(data["ProductId"].Value<int>(), data["CurrentStatus"].Value<bool>());
                        if (toggledProd) await BroadcastAsync(new PacketMessage { Type = MsgType.MENU_UPDATED });
                        return Ok(req.Type, new { Toggled = toggledProd });

                    case MsgType.DELETE_PRODUCT_REQ:
                        {
                            if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin có quyền xóa món!");
                            int prodId = data["ProductId"].Value<int>();
                            if (_menuDal.IsProductUsedInBills(prodId))
                                return Fail(req.Type, "PRODUCT_IN_USE", "Món đã từng xuất hiện trong hóa đơn cũ, chỉ có thể chuyển sang Ngừng bán!");

                            bool deletedProd = _menuDal.DeleteProduct(prodId);
                            if (deletedProd) await BroadcastAsync(new PacketMessage { Type = MsgType.MENU_UPDATED });
                            return Ok(req.Type, new { Deleted = deletedProd });
                        }

                    // ================= POS, BÀN, ORDER & THANH TOÁN =================
                    case MsgType.GET_TABLES_REQ:
                        return Ok(req.Type, _posDal.GetAllTables());

                    case MsgType.GET_BILL_DETAILS_REQ:
                        return Ok(req.Type, _adapter.GetBillDetailsByTableDto(data["TableId"].Value<int>()));

                    case MsgType.ADD_ITEM_REQ:
                        {
                            int tableId = data["TableId"].Value<int>();
                            if (!ExecuteWithTableLock(tableId, () => _posDal.AddItemToTable(tableId, data["ProductId"].Value<int>(), data["Quantity"].Value<int>())))
                                return Fail(MsgType.TABLE_BUSY, "TABLE_BUSY", "Bàn đang được máy khác thao tác, vui lòng thử lại!");

                            await BroadcastAsync(new PacketMessage { Type = MsgType.TABLE_STATUS_CHANGED, PayloadJson = JsonConvert.SerializeObject(new { TableId = tableId }) });
                            return Ok(req.Type, new { Added = true });
                        }

                    case MsgType.REMOVE_ITEM_REQ:
                        {
                            int tableId = data["TableId"].Value<int>();
                            int detailId = data["BillDetailId"].Value<int>();
                            if (!ExecuteWithTableLock(tableId, () => _posDal.RemoveBillDetailItem(detailId, tableId)))
                                return Fail(MsgType.TABLE_BUSY, "TABLE_BUSY", "Bàn đang được máy khác thao tác!");

                            await BroadcastAsync(new PacketMessage { Type = MsgType.TABLE_STATUS_CHANGED, PayloadJson = JsonConvert.SerializeObject(new { TableId = tableId }) });
                            await BroadcastAsync(new PacketMessage { Type = MsgType.KDS_NEW_ORDER, PayloadJson = JsonConvert.SerializeObject(new { TableId = tableId }) });
                            return Ok(req.Type, new { Removed = true });
                        }

                    case MsgType.SEND_KITCHEN_REQ:
                        {
                            int tableId = data["TableId"].Value<int>();
                            int sentCount = _posDal.SendTableOrderToKitchen(tableId);
                            if (sentCount > 0)
                            {
                                await BroadcastAsync(new PacketMessage { Type = MsgType.KDS_NEW_ORDER, PayloadJson = JsonConvert.SerializeObject(new { TableId = tableId, SentCount = sentCount }) });
                            }
                            return Ok(req.Type, new { SentCount = sentCount });
                        }

                    case MsgType.MOVE_TABLE_REQ:
                        {
                            int srcId = data["SourceTableId"].Value<int>();
                            int dstId = data["TargetTableId"].Value<int>();
                            if (srcId == dstId)
                                return Fail(req.Type, "INVALID_TABLE", "Bàn nguồn và bàn đích không được trùng nhau!");
                            if (!ExecuteWithDualTableLock(srcId, dstId, () => _posDal.MoveToEmptyTable(srcId, dstId)))
                                return Fail(MsgType.TABLE_BUSY, "TABLE_BUSY", "Một trong hai bàn đang được máy khác thao tác!");

                            await BroadcastAsync(new PacketMessage { Type = MsgType.TABLE_STATUS_CHANGED, PayloadJson = JsonConvert.SerializeObject(new { TableId = dstId }) });
                            return Ok(req.Type, new { Moved = true });
                        }

                    case MsgType.MERGE_TABLE_REQ:
                        {
                            int srcId = data["SourceTableId"].Value<int>();
                            int dstId = data["TargetTableId"].Value<int>();
                            if (srcId == dstId)
                                return Fail(req.Type, "INVALID_TABLE", "Bàn nguồn và bàn đích không được trùng nhau!");
                            if (!ExecuteWithDualTableLock(srcId, dstId, () => _posDal.MergeOccupiedTables(srcId, dstId)))
                                return Fail(MsgType.TABLE_BUSY, "TABLE_BUSY", "Một trong hai bàn đang được máy khác thao tác!");

                            await BroadcastAsync(new PacketMessage { Type = MsgType.TABLE_STATUS_CHANGED, PayloadJson = JsonConvert.SerializeObject(new { TableId = dstId }) });
                            return Ok(req.Type, new { Merged = true });
                        }

                    case MsgType.GET_VIETQR_REQ:
                        {
                            decimal amount = data["Amount"].Value<decimal>();
                            string tableName = data["TableName"]?.ToString() ?? "";
                            using (Bitmap bmp = SmartPaymentService.GetQrBitmap(amount, tableName))
                            using (MemoryStream ms = new MemoryStream())
                            {
                                bmp.Save(ms, ImageFormat.Png);
                                string b64 = Convert.ToBase64String(ms.ToArray());
                                return Ok(req.Type, new QrResponseDTO { Base64Png = b64 });
                            }
                        }

                    case MsgType.CHECKOUT_REQ:
                        {
                            int tableId = data["TableId"].Value<int>();
                            int discount = data["DiscountPercent"].Value<int>();
                            string method = data["PaymentMethod"]?.ToString() ?? "Tiền mặt";

                            bool success = false;
                            decimal finalAmt = 0;
                            if (!ExecuteWithTableLock(tableId, () =>
                            {
                                success = _posDal.CheckoutTable(tableId, discount, method, out finalAmt);
                            }))
                            {
                                return Fail(MsgType.TABLE_BUSY, "TABLE_BUSY", "Bàn đang được máy khác thanh toán!");
                            }

                            if (success)
                            {
                                await BroadcastAsync(new PacketMessage { Type = MsgType.TABLE_STATUS_CHANGED, PayloadJson = JsonConvert.SerializeObject(new { TableId = tableId }) });
                            }
                            return Ok(req.Type, new CheckoutResponseDTO { IsSuccess = success, TableId = tableId, FinalAmount = finalAmt });
                        }

                    // ================= KDS & THỐNG KÊ =================
                    case MsgType.GET_PENDING_KDS_REQ:
                        return Ok(req.Type, _adapter.GetPendingKitchenItemsDto());

                    case MsgType.KDS_COMPLETE_ITEM_REQ:
                        {
                            int detailId = data["BillDetailId"].Value<int>();
                            _posDal.MarkKitchenItemDone(detailId);
                            await BroadcastAsync(new PacketMessage { Type = MsgType.KDS_ITEM_DONE, PayloadJson = JsonConvert.SerializeObject(new { BillDetailId = detailId }) });
                            return Ok(req.Type, new { Done = true });
                        }

                    case MsgType.STATS_REVENUE_REQ:
                        {
                            if (!session.IsAdmin) return Fail(MsgType.PERMISSION_DENIED, "PERMISSION_DENIED", "Chỉ Admin được xem thống kê doanh thu!");
                            DateTime from = DateTime.Parse(data["FromDate"].ToString());
                            DateTime to = DateTime.Parse(data["ToDate"].ToString());
                            return Ok(req.Type, _adapter.GetRevenueStats(from, to));
                        }

                    default:
                        return Fail(MsgType.ERROR, "UNKNOWN_MSG", $"Không hỗ trợ gói tin {req.Type}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERR] Xử lý {req.Type}: {ex.Message}");
                return Fail(MsgType.ERROR, "SERVER_EXCEPTION", ex.Message);
            }
        }

        // Khóa đồng thời theo TableId chống 2 máy đặt/thanh toán trùng bàn
        private static bool ExecuteWithTableLock(int tableId, Action action)
        {
            object lockObj = _tableLocks.GetOrAdd(tableId, _ => new object());
            if (!Monitor.TryEnter(lockObj, 500)) return false;
            try
            {
                action();
                return true;
            }
            finally
            {
                Monitor.Exit(lockObj);
            }
        }

        // Khóa 2 bàn theo thứ tự ID tăng dần để tránh Deadlock khi chuyển/gộp bàn
        private static bool ExecuteWithDualTableLock(int id1, int id2, Action action)
        {
            int first = Math.Min(id1, id2);
            int second = Math.Max(id1, id2);
            object lock1 = _tableLocks.GetOrAdd(first, _ => new object());
            object lock2 = _tableLocks.GetOrAdd(second, _ => new object());

            if (!Monitor.TryEnter(lock1, 500)) return false;
            try
            {
                if (!Monitor.TryEnter(lock2, 500)) return false;
                try
                {
                    action();
                    return true;
                }
                finally
                {
                    Monitor.Exit(lock2);
                }
            }
            finally
            {
                Monitor.Exit(lock1);
            }
        }

        private static bool VerifyStoredOtp(string key, string inputOtp, out string error)
        {
            error = "";
            if (!_otpStore.TryGetValue(key, out var entry))
            {
                error = "Chưa yêu cầu mã OTP hoặc mã đã được sử dụng!";
                return false;
            }
            if (DateTime.Now > entry.Expiry)
            {
                _otpStore.TryRemove(key, out _);
                error = "Mã OTP đã hết hạn (quá 5 phút)!";
                return false;
            }
            if (entry.Otp != inputOtp)
            {
                error = "Mã OTP không chính xác!";
                return false;
            }
            _otpStore.TryRemove(key, out _);
            return true;
        }

        private static PacketMessage Ok(MsgType type, object payload, string msg = "") =>
            new PacketMessage { Type = type, Success = true, PayloadJson = JsonConvert.SerializeObject(payload), Message = msg };

        private static PacketMessage Fail(MsgType type, string code, string msg) =>
            new PacketMessage { Type = type, Success = false, ErrorCode = code, Message = msg };

        private static async Task SendToSessionAsync(ClientSession session, PacketMessage packet)
        {
            await session.SendLock.WaitAsync();
            try
            {
                if (session.Client.Connected)
                    await FramingHelper.SendPacketAsync(session.Stream, packet);
            }
            finally
            {
                session.SendLock.Release();
            }
        }

        private static async Task BroadcastAsync(PacketMessage packet)
        {
            foreach (var kv in _sessions)
            {
                try
                {
                    await SendToSessionAsync(kv.Value, packet);
                }
                catch
                {
                    // Bỏ qua client đã rớt mạng để không làm gián đoạn client khác
                }
            }
        }
    }
}