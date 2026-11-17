using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace QuanLyCafe_Nhom8.Services
{
    public class ApiRequest
    {
        public string accountNo { get; set; }
        public string accountName { get; set; }
        public int acqId { get; set; }
        public long amount { get; set; }
        public string addInfo { get; set; }
        public string format { get; set; }
        public string template { get; set; }
    }

    public static class SmartPaymentService
    {
        public static string BankId = "970436";
        public static int AcqId = 970436;
        public static string BankShortName = "Vietcombank";
        public static string AccountNo = "9942886521";
        public static string AccountName = "NGUYEN HOANG DUY ANH";
        public static string Template = "compact2";

        public static Bitmap GetQrBitmap(decimal amount, string tableName)
        {
            long roundedAmount = Convert.ToInt64(Math.Round(amount));
            string cleanInfo = RemoveVietnameseAccents($"Thanh toan {tableName}").Trim();
            if (cleanInfo.Length > 25) cleanInfo = cleanInfo.Substring(0, 25);

            string jsonRequest = "{"
                + $"\"accountNo\":\"{AccountNo}\","
                + $"\"accountName\":\"{AccountName}\","
                + $"\"acqId\":{AcqId},"
                + $"\"amount\":{roundedAmount},"
                + $"\"addInfo\":\"{cleanInfo}\","
                + $"\"format\":\"text\","
                + $"\"template\":\"{Template}\""
                + "}";

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;

            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Headers[HttpRequestHeader.Accept] = "application/json";
                client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                string responseJson = client.UploadString("https://api.vietqr.io/v2/generate", "POST", jsonRequest);

                Match match = Regex.Match(responseJson, @"""qrDataURL""\s*:\s*""([^""]+)""");
                if (match.Success)
                {
                    string qrDataURL = match.Groups[1].Value.Replace(@"\/", "/");
                    string base64String = qrDataURL.Replace("data:image/png;base64,", "").Trim();
                    return Base64ToImage(base64String);
                }
            }

            throw new Exception("Không nhận được qrDataURL từ máy chủ api.vietqr.io");
        }

        public static Bitmap Base64ToImage(string base64String)
        {
            byte[] imageBytes = Convert.FromBase64String(base64String);
            using (MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length))
            {
                ms.Write(imageBytes, 0, imageBytes.Length);
                using (Image img = Image.FromStream(ms, true))
                {
                    return new Bitmap(img);
                }
            }
        }

        public static string BuildVietQrUrl(decimal amount, string tableName)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"vietqr_{Guid.NewGuid():N}.png");
            using (Bitmap bmp = GetQrBitmap(amount, tableName))
            {
                bmp.Save(tempFilePath, System.Drawing.Imaging.ImageFormat.Png);
            }
            return tempFilePath;
        }

        private static string RemoveVietnameseAccents(string text)
        {
            if (string.IsNullOrEmpty(text)) return "Thanh toan";
            string normalized = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in normalized)
            {
                System.Globalization.UnicodeCategory uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }

        public static void PlaySuccessSound()
        {
            SystemSounds.Asterisk.Play();
        }
    }
}