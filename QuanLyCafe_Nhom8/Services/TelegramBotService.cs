using System;
using System.Data;
using System.Net;
using System.Text;
using QuanLyCafe_Nhom8.DAL;

namespace QuanLyCafe_Nhom8.Services
{
    public class TelegramBotService
    {
        private readonly string botToken = "9079144131:AAH0X7MrQPP1xsoRV1KbW2L8Hmk7q0Eg7x4";
        private readonly string chatId = "8786904627";

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

        public bool GuiDoanhThu(DateTime tu, DateTime den, int soDon, decimal tong)
        {
            string msg = $"BAO CAO DOANH THU ({tu:dd/MM} - {den:dd/MM})\n"
                       + $"So hoa don: {soDon}\n"
                       + $"Tong tien: {tong:N0} VND";
            return GuiTin(msg);
        }

        public string BaoHuyMon()
        {
            try
            {
                string sql = "SELECT TOP 5 * FROM AuditLogs WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) ORDER BY 1 DESC";
                DataTable dt = DatabaseHelper.ExecuteQuery(sql);

                if (dt.Rows.Count == 0) return "Hom nay khong co huy mon.";

                string msg = "CANH BAO HUY MON:\n";
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    msg += "- " + string.Join(" | ", dt.Rows[i].ItemArray) + "\n";
                }

                return GuiTin(msg) ? "Da gui Telegram!" : "Gui that bai!";
            }
            catch (Exception ex)
            {
                return "Loi: " + ex.Message;
            }
        }
    }
}