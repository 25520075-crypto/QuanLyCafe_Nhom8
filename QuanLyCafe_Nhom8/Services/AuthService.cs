using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace QuanLyCafe_Nhom8.Services
{
    public static class AuthService
    {
        // 1. NHẬP GMAIL CỦA BẠN VÀO ĐÂY:
        private static readonly string SenderEmail = "kaytjungle@gmail.com";

        // 2. NHẬP MÃ 16 KÝ TỰ VÀO ĐÂY (VÍ DỤ: "abcd efgh ijkl mnop"):
        private static readonly string AppPassword = "ojwy azud ahht ukzb";

        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string GenerateOTP()
        {
            Random rnd = new Random();
            return rnd.Next(100000, 999999).ToString();
        }

        public static bool SendOtpEmail(string toEmail, string otpCode, string actionTitle, out string errorMsg)
        {
            errorMsg = "";
            try
            {
                // Đoạn chặn Demo đã bị xóa hoàn toàn ở đây!
                // Mã bây giờ sẽ gửi thẳng tới máy chủ Google.

                string cleanAppPassword = AppPassword.Replace(" ", "").Trim();

                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(SenderEmail, "COFFEE FEFA - NHÓM 8");
                    mail.To.Add(toEmail.Trim());
                    mail.Subject = $"[COFFEE FEFA] Mã OTP {actionTitle}";
                    mail.IsBodyHtml = true;
                    mail.Body = $@"
                        <div style='font-family: Segoe UI, sans-serif; padding: 20px; border: 2px solid #6F4E37; border-radius: 10px; max-width: 520px;'>
                            <h2 style='color: #3E2723; margin-top: 0;'>☕ HỆ THỐNG QUẢN LÝ CAFE - NHÓM 8</h2>
                            <p>Xin chào,</p>
                            <p>Hệ thống đang thực hiện yêu cầu: <b>{actionTitle}</b> cho địa chỉ Gmail này.</p>
                            <p>Mã xác thực OTP gồm 6 chữ số của bạn là:</p>
                            <div style='font-size: 28px; font-weight: bold; color: #D35400; letter-spacing: 6px; background: #FDF2E9; padding: 14px; text-align: center; border-radius: 6px;'>
                                {otpCode}
                            </div>
                            <p style='color: #555; font-size: 13px; margin-top: 15px;'>
                                * Mã này có hiệu lực trong vòng <b>5 phút</b>. Tuyệt đối không chia sẻ mã này cho người khác.
                            </p>
                        </div>";

                    using (SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.EnableSsl = true;
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(SenderEmail, cleanAppPassword);
                        smtp.Send(mail);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                // Bắt đúng lỗi thực tế từ Google nếu mật khẩu ứng dụng bị sai
                errorMsg = ex.Message;
                return false;
            }
        }
    }
}