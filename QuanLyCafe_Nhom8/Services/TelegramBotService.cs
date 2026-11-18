using System;
using System.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using QuanLyCafe_Nhom8.DAL;

namespace QuanLyCafe_Nhom8.Services
{
    public class TelegramBotService
    {
        // Thông tin cấu hình Bot Token và Chat ID của nhóm
        private readonly string botToken = "9079144131:AAH0X7MrQPP1xsoRV1KbW2L8Hmk7q0Eg7x4";
        private readonly string chatId = "5939667192";

        // Biến tĩnh đảm bảo luồng lắng nghe tin nhắn 2 chiều chỉ khởi chạy 1 lần duy nhất
        private static bool _isPollingStarted = false;
        private static long _lastUpdateId = 0;

        public TelegramBotService()
        {
            StartTwoWayListener();
        }

        // Khởi chạy luồng chạy ngầm (Background Task) lắng nghe lệnh từ điện thoại của Quản lý mỗi 3 giây
        public void StartTwoWayListener()
        {
            if (_isPollingStarted) return;
            _isPollingStarted = true;

            Task.Run(() =>
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                bool isFirstSync = true;

                while (true)
                {
                    try
                    {
                        string url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={_lastUpdateId + 1}&timeout=0";
                        string json = "";

                        using (WebClient wc = new WebClient())
                        {
                            wc.Encoding = Encoding.UTF8;
                            json = wc.DownloadString(url);
                        }

                        // Tách từng tin nhắn mới gửi tới Bot bằng biểu thức chính quy (Regex)
                        MatchCollection matches = Regex.Matches(
                            json,
                            @"""update_id""\s*:\s*(\d+).*?""text""\s*:\s*""([^""]+)""",
                            RegexOptions.Singleline);

                        foreach (Match m in matches)
                        {
                            long updateId = Convert.ToInt64(m.Groups[1].Value);
                            string rawText = m.Groups[2].Value.Replace(@"\/", "/").Trim();

                            if (updateId > _lastUpdateId)
                            {
                                _lastUpdateId = updateId;

                                // Bỏ qua các tin nhắn cũ tồn đọng trước lúc mở phần mềm
                                if (!isFirstSync)
                                {
                                    ProcessManagerCommand(rawText);
                                }
                            }
                        }

                        isFirstSync = false;
                    }
                    catch
                    {
                        // Bỏ qua lỗi mất mạng tạm thời để vòng lặp tiếp tục chạy ổn định
                    }

                    Thread.Sleep(3000);
                }
            });
        }

        // Xử lý lệnh khi Quản lý gõ /homnay, /homqua, /doanhthu, /ban, /huymon trên Telegram
        private void ProcessManagerCommand(string commandText)
        {
            string cmd = commandText.ToLowerInvariant().Split(' ')[0];

            if (cmd == "/homnay" || cmd == "/doanhthu")
            {
                GuiTin(LayBaoCaoDoanhThuTheoNgay(DateTime.Today, "HÔM NAY"));
            }
            else if (cmd == "/homqua")
            {
                GuiTin(LayBaoCaoDoanhThuTheoNgay(DateTime.Today.AddDays(-1), "HÔM QUA"));
            }
            else if (cmd == "/ban")
            {
                GuiTin(LayTrangThaiBanHienTai());
            }
            else if (cmd == "/huymon")
            {
                BaoHuyMon();
            }
            else if (cmd == "/help" || cmd == "/start")
            {
                string helpMsg = "🤖 MENU TRỢ LÝ QUẢN LÝ CAFE NHÓM 8:\n"
                               + "👉 /homnay : Xem doanh thu & số đơn hôm nay\n"
                               + "👉 /homqua : Xem doanh thu & số đơn hôm qua\n"
                               + "👉 /ban : Xem danh sách bàn đang có khách\n"
                               + "👉 /huymon : Kiểm tra lịch sử hủy món hôm nay";
                GuiTin(helpMsg);
            }
        }

        // Truy vấn doanh thu theo ngày (Hôm nay hoặc Hôm qua) từ bảng Bills
        private string LayBaoCaoDoanhThuTheoNgay(DateTime targetDate, string label)
        {
            try
            {
                string sql = @"
                    SELECT COUNT(*) AS SoDon, 
                           ISNULL(SUM(TotalAmount), 0) AS TongTien
                    FROM Bills
                    WHERE Status = 1 AND CAST(CheckOutTime AS DATE) = @Ngay";

                DataTable dt = DatabaseHelper.ExecuteQuery(sql, new System.Data.SqlClient.SqlParameter[]
                {
                    new System.Data.SqlClient.SqlParameter("@Ngay", targetDate.Date)
                });

                int soDon = Convert.ToInt32(dt.Rows[0]["SoDon"]);
                decimal tongTien = Convert.ToDecimal(dt.Rows[0]["TongTien"]);

                string detailSql = @"
                    SELECT ISNULL(PaymentMethod, N'Tiền mặt') AS PhuongThuc, 
                           COUNT(*) AS SL, 
                           ISNULL(SUM(TotalAmount), 0) AS Tien
                    FROM Bills
                    WHERE Status = 1 AND CAST(CheckOutTime AS DATE) = @Ngay
                    GROUP BY ISNULL(PaymentMethod, N'Tiền mặt')";

                DataTable dtDetail = DatabaseHelper.ExecuteQuery(detailSql, new System.Data.SqlClient.SqlParameter[]
                {
                    new System.Data.SqlClient.SqlParameter("@Ngay", targetDate.Date)
                });

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"📊 BÁO CÁO DOANH THU {label} ({targetDate:dd/MM/yyyy})");
                sb.AppendLine($"- Tổng số đơn đã thu: {soDon} đơn");
                sb.AppendLine($"- Tổng doanh thu: {tongTien:N0} VNĐ");

                if (dtDetail.Rows.Count > 0)
                {
                    sb.AppendLine("--------------------------");
                    sb.AppendLine("Chi tiết theo nguồn tiền:");
                    foreach (DataRow r in dtDetail.Rows)
                    {
                        sb.AppendLine($"• {r["PhuongThuc"]}: {r["SL"]} đơn ({Convert.ToDecimal(r["Tien"]):N0} VNĐ)");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "Lỗi tra cứu doanh thu: " + ex.Message;
            }
        }

        // Truy vấn các bàn đang có khách phục vụ khi Quản lý gõ /ban
        private string LayTrangThaiBanHienTai()
        {
            try
            {
                string sql = @"
                    SELECT t.TableName, b.TotalAmount
                    FROM Tables t
                    INNER JOIN Bills b ON t.TableId = b.TableId
                    WHERE b.Status = 0";
                DataTable dt = DatabaseHelper.ExecuteQuery(sql);

                if (dt.Rows.Count == 0)
                {
                    return "🟢 Hiện tại tất cả các bàn đều đang TRỐNG.";
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"🟠 ĐANG PHỤC VỤ: {dt.Rows.Count} BÀN / ĐƠN:");
                foreach (DataRow r in dt.Rows)
                {
                    sb.AppendLine($"• {r["TableName"]}: tạm tính {Convert.ToDecimal(r["TotalAmount"]):N0} VNĐ");
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "Lỗi kiểm tra bàn: " + ex.Message;
            }
        }

        // Hàm gửi tin nhắn 1 chiều lên Telegram
        public bool GuiTin(string text)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(text)}";

                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.DownloadString(url);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Hàm gửi báo cáo doanh thu từ màn hình FrmStatistics
        public bool GuiDoanhThu(DateTime tu, DateTime den, int soDon, decimal tong)
        {
            string msg = $"📊 BÁO CÁO DOANH THU ({tu:dd/MM/yyyy} - {den:dd/MM/yyyy})\n"
                       + $"- Số hóa đơn: {soDon}\n"
                       + $"- Tổng tiền: {tong:N0} VNĐ";
            return GuiTin(msg);
        }

        // Hàm kiểm tra và gửi cảnh báo các món bị hủy trong ngày từ bảng AuditLogs
        public string BaoHuyMon()
        {
            try
            {
                string sql = @"
                    SELECT TOP 5 a.LogId, a.BillId, p.ProductName, a.DeletedQuantity, FORMAT(a.LogTime, 'HH:mm:ss') AS GioHuy
                    FROM AuditLogs a
                    LEFT JOIN Products p ON a.ProductId = p.ProductId
                    WHERE CAST(a.LogTime AS DATE) = CAST(GETDATE() AS DATE)
                    ORDER BY a.LogId DESC";
                DataTable dt = DatabaseHelper.ExecuteQuery(sql);

                if (dt.Rows.Count == 0)
                {
                    GuiTin("✅ Hôm nay quán không có thao tác hủy món nào.");
                    return "Hôm nay không có hủy món.";
                }

                string msg = "⚠️ CẢNH BÁO HỦY MÓN HÔM NAY:\n";
                foreach (DataRow row in dt.Rows)
                {
                    msg += $"- Đơn #{row["BillId"]} | Món: {row["ProductName"]} | SL hủy: {row["DeletedQuantity"]} | Lúc: {row["GioHuy"]}\n";
                }

                return GuiTin(msg) ? "Đã gửi Telegram!" : "Gửi thất bại!";
            }
            catch (Exception ex)
            {
                return "Lỗi: " + ex.Message;
            }
        }
    }
}